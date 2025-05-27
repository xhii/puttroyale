using UnityEngine;
using Mirror;
using System.Collections.Generic;
using System.Linq;
using System.Collections;

namespace MicrogolfMasters
{
    public class GameManager : NetworkBehaviour
    {
        private static GameManager _instance;
        public static GameManager Instance => _instance;
        
        [Header("Game Configuration")]
        [SerializeField] private GameMode currentGameMode = GameMode.Multiplayer1v1;
        [SerializeField] private int maxPlayers = 4;
        [SerializeField] private float matchTimeLimit = 180f; // 3 minutes
        [SerializeField] private int parScore = 4;
        
        [Header("Course Settings")]
        [SerializeField] private CourseData currentCourse;
        [SerializeField] private Transform[] spawnPoints;
        [SerializeField] private GameObject ballPrefab;
        [SerializeField] private GameObject holePrefab;
        
        [Header("Match State")]
        [SyncVar] private MatchState currentState = MatchState.WaitingForPlayers;
        [SyncVar] private float matchTimer = 0f;
        [SyncVar] private int currentHole = 1;
        [SyncVar] private int totalHoles = 9;
        
        // Player Management
        private Dictionary<NetworkConnection, PlayerMatchData> playerData = new Dictionary<NetworkConnection, PlayerMatchData>();
        private List<NetworkConnection> finishedPlayers = new List<NetworkConnection>();
        
        // Events
        public System.Action<MatchState> OnMatchStateChanged;
        public System.Action<NetworkConnection, int> OnPlayerScoreUpdated;
        public System.Action<NetworkConnection> OnPlayerFinished;
        public System.Action<int> OnHoleChanged;
        
        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
        }
        
        public override void OnStartServer()
        {
            base.OnStartServer();
            NetworkServer.RegisterHandler<PlayerReadyMessage>(OnPlayerReady);
            NetworkServer.RegisterHandler<PlayerShotMessage>(OnPlayerShot);
        }
        
        #region Player Management
        
        [Server]
        public void RegisterPlayer(NetworkConnection conn, PlayerProfile profile)
        {
            if (!playerData.ContainsKey(conn))
            {
                PlayerMatchData data = new PlayerMatchData
                {
                    connection = conn,
                    profile = profile,
                    scores = new int[totalHoles],
                    currentHoleShots = 0,
                    totalShots = 0,
                    isReady = false,
                    ballInstance = null
                };
                
                playerData.Add(conn, data);
                
                // Spawn ball for player
                SpawnPlayerBall(conn);
                
                // Update UI
                RpcUpdatePlayerList(GetPlayerList());
                
                // Check if we can start
                CheckStartConditions();
            }
        }
        
        [Server]
        private void SpawnPlayerBall(NetworkConnection conn)
        {
            if (!playerData.ContainsKey(conn)) return;
            
            int playerIndex = playerData.Keys.ToList().IndexOf(conn);
            Vector3 spawnPos = spawnPoints[playerIndex % spawnPoints.Length].position;
            
            GameObject ball = Instantiate(ballPrefab, spawnPos, Quaternion.identity);
            NetworkServer.Spawn(ball, conn);
            
            // Configure ball
            GolfBallController ballController = ball.GetComponent<GolfBallController>();
            BallData ballData = playerData[conn].profile.selectedBall;
            ballController.SetBallStats(ballData);
            
            playerData[conn].ballInstance = ball;
        }
        
        [Server]
        public void UnregisterPlayer(NetworkConnection conn)
        {
            if (playerData.ContainsKey(conn))
            {
                // Destroy ball
                if (playerData[conn].ballInstance != null)
                {
                    NetworkServer.Destroy(playerData[conn].ballInstance);
                }
                
                playerData.Remove(conn);
                finishedPlayers.Remove(conn);
                
                RpcUpdatePlayerList(GetPlayerList());
                
                // Check if match should end
                if (currentState == MatchState.InProgress && playerData.Count < 2)
                {
                    EndMatch();
                }
            }
        }
        
        #endregion
        
        #region Match Flow
        
        [Server]
        private void CheckStartConditions()
        {
            if (currentState != MatchState.WaitingForPlayers) return;
            
            int readyPlayers = playerData.Count(p => p.Value.isReady);
            
            if (currentGameMode == GameMode.Multiplayer1v1 && readyPlayers >= 2)
            {
                StartCountdown();
            }
            else if (currentGameMode == GameMode.Multiplayer4Player && readyPlayers >= 4)
            {
                StartCountdown();
            }
            else if (currentGameMode == GameMode.Tournament && readyPlayers >= 8)
            {
                StartCountdown();
            }
        }
        
        [Server]
        private void StartCountdown()
        {
            currentState = MatchState.Countdown;
            RpcUpdateMatchState(currentState);
            StartCoroutine(CountdownCoroutine());
        }
        
        private IEnumerator CountdownCoroutine()
        {
            float countdown = 5f;
            
            while (countdown > 0)
            {
                RpcUpdateCountdown((int)countdown);
                yield return new WaitForSeconds(1f);
                countdown--;
            }
            
            StartMatch();
        }
        
        [Server]
        private void StartMatch()
        {
            currentState = MatchState.InProgress;
            matchTimer = matchTimeLimit;
            currentHole = 1;
            finishedPlayers.Clear();
            
            // Reset all players
            foreach (var player in playerData.Values)
            {
                player.currentHoleShots = 0;
                player.scores = new int[totalHoles];
                
                if (player.ballInstance != null)
                {
                    GolfBallController ball = player.ballInstance.GetComponent<GolfBallController>();
                    ball.ResetBall(spawnPoints[0].position);
                }
            }
            
            RpcStartMatch();
            LoadHole(currentHole);
        }
        
        [Server]
        private void LoadHole(int holeNumber)
        {
            // Load course data for current hole
            CourseHoleData holeData = currentCourse.holes[holeNumber - 1];
            
            // Update hole position
            GameObject hole = GameObject.FindGameObjectWithTag("Hole");
            if (hole != null)
            {
                hole.transform.position = holeData.holePosition;
            }
            
            // Reset player positions
            int index = 0;
            foreach (var player in playerData.Values)
            {
                if (player.ballInstance != null)
                {
                    Vector3 spawnPos = holeData.spawnPoints[index % holeData.spawnPoints.Length];
                    player.ballInstance.GetComponent<GolfBallController>().ResetBall(spawnPos);
                }
                index++;
            }
            
            parScore = holeData.par;
            
            RpcLoadHole(holeNumber, holeData);
        }
        
        private void Update()
        {
            if (!isServer) return;
            
            if (currentState == MatchState.InProgress)
            {
                UpdateMatchTimer();
                CheckWinConditions();
            }
        }
        
        [Server]
        private void UpdateMatchTimer()
        {
            matchTimer -= Time.deltaTime;
            
            if (matchTimer <= 0)
            {
                matchTimer = 0;
                EndMatch();
            }
        }
        
        [Server]
        private void CheckWinConditions()
        {
            // Check if all players finished current hole
            if (finishedPlayers.Count >= playerData.Count)
            {
                NextHole();
            }
        }
        
        [Server]
        private void NextHole()
        {
            currentHole++;
            finishedPlayers.Clear();
            
            if (currentHole > totalHoles)
            {
                EndMatch();
            }
            else
            {
                foreach (var player in playerData.Values)
                {
                    player.currentHoleShots = 0;
                }
                
                LoadHole(currentHole);
            }
        }
        
        [Server]
        private void EndMatch()
        {
            currentState = MatchState.MatchEnded;
            
            // Calculate final scores
            Dictionary<NetworkConnection, int> finalScores = new Dictionary<NetworkConnection, int>();
            foreach (var player in playerData)
            {
                int totalScore = player.Value.scores.Sum();
                finalScores.Add(player.Key, totalScore);
            }
            
            // Sort by score (lowest is best in golf)
            var sortedScores = finalScores.OrderBy(x => x.Value).ToList();
            
            RpcEndMatch(sortedScores);
            
            // Award rewards
            AwardMatchRewards(sortedScores);
            
            // Reset after delay
            StartCoroutine(ResetMatchAfterDelay());
        }
        
        private IEnumerator ResetMatchAfterDelay()
        {
            yield return new WaitForSeconds(10f);
            ResetMatch();
        }
        
        [Server]
        private void ResetMatch()
        {
            currentState = MatchState.WaitingForPlayers;
            currentHole = 1;
            matchTimer = matchTimeLimit;
            finishedPlayers.Clear();
            
            foreach (var player in playerData.Values)
            {
                player.isReady = false;
                player.currentHoleShots = 0;
                player.totalShots = 0;
                player.scores = new int[totalHoles];
            }
            
            RpcResetMatch();
        }
        
        #endregion
        
        #region Player Actions
        
        public void OnPlayerShoot(NetworkConnection conn, int shotCount)
        {
            if (!playerData.ContainsKey(conn)) return;
            
            playerData[conn].currentHoleShots = shotCount;
            playerData[conn].totalShots++;
            
            RpcUpdatePlayerShots(conn, shotCount);
        }
        
        public void OnPlayerFinished(NetworkConnection conn, int finalShotCount)
        {
            if (!playerData.ContainsKey(conn) || finishedPlayers.Contains(conn)) return;
            
            // Record score for current hole
            playerData[conn].scores[currentHole - 1] = finalShotCount;
            finishedPlayers.Add(conn);
            
            // Calculate score relative to par
            int scoreRelativeToPar = finalShotCount - parScore;
            string scoreText = GetScoreText(scoreRelativeToPar);
            
            RpcPlayerFinishedHole(conn, finalShotCount, scoreText);
            
            // Award hole completion rewards
            int coins = CalculateHoleReward(finalShotCount);
            playerData[conn].profile.coins += coins;
            
            OnPlayerFinished?.Invoke(conn);
        }
        
        private string GetScoreText(int scoreRelativeToPar)
        {
            switch (scoreRelativeToPar)
            {
                case -3: return "Albatross!";
                case -2: return "Eagle!";
                case -1: return "Birdie!";
                case 0: return "Par";
                case 1: return "Bogey";
                case 2: return "Double Bogey";
                default: return scoreRelativeToPar > 0 ? $"+{scoreRelativeToPar}" : scoreRelativeToPar.ToString();
            }
        }
        
        private int CalculateHoleReward(int shots)
        {
            int baseReward = 50;
            int parBonus = Mathf.Max(0, (parScore - shots) * 20);
            return baseReward + parBonus;
        }
        
        #endregion
        
        #region Rewards
        
        [Server]
        private void AwardMatchRewards(List<KeyValuePair<NetworkConnection, int>> sortedScores)
        {
            for (int i = 0; i < sortedScores.Count; i++)
            {
                var player = sortedScores[i];
                PlayerMatchData data = playerData[player.Key];
                
                // Position rewards
                int positionReward = GetPositionReward(i + 1);
                data.profile.coins += positionReward;
                
                // Experience
                int exp = GetExperienceReward(i + 1);
                data.profile.experience += exp;
                
                // Tournament specific rewards
                if (currentGameMode == GameMode.Tournament)
                {
                    if (i == 0) // Winner
                    {
                        data.profile.coins += 750;
                        data.profile.cards += 5;
                    }
                }
                
                // Update player stats
                data.profile.matchesPlayed++;
                if (i == 0) data.profile.wins++;
                
                RpcAwardRewards(player.Key, positionReward, exp);
            }
        }
        
        private int GetPositionReward(int position)
        {
            switch (position)
            {
                case 1: return 100;
                case 2: return 50;
                case 3: return 25;
                default: return 10;
            }
        }
        
        private int GetExperienceReward(int position)
        {
            switch (position)
            {
                case 1: return 50;
                case 2: return 30;
                case 3: return 20;
                default: return 10;
            }
        }
        
        #endregion
        
        #region Network Messages
        
        private void OnPlayerReady(NetworkConnection conn, PlayerReadyMessage msg)
        {
            if (playerData.ContainsKey(conn))
            {
                playerData[conn].isReady = true;
                CheckStartConditions();
            }
        }
        
        private void OnPlayerShot(NetworkConnection conn, PlayerShotMessage msg)
        {
            OnPlayerShoot(conn, msg.shotCount);
        }
        
        #endregion
        
        #region Client RPCs
        
        [ClientRpc]
        private void RpcUpdateMatchState(MatchState newState)
        {
            currentState = newState;
            OnMatchStateChanged?.Invoke(newState);
            UIManager.Instance.UpdateMatchState(newState);
        }
        
        [ClientRpc]
        private void RpcUpdateCountdown(int seconds)
        {
            UIManager.Instance.ShowCountdown(seconds);
            AudioManager.Instance.PlaySound("Countdown");
        }
        
        [ClientRpc]
        private void RpcStartMatch()
        {
            UIManager.Instance.ShowMatchStarted();
            AudioManager.Instance.PlaySound("MatchStart");
        }
        
        [ClientRpc]
        private void RpcLoadHole(int holeNumber, CourseHoleData holeData)
        {
            UIManager.Instance.UpdateHoleInfo(holeNumber, holeData.par);
            CameraController.Instance.SetCourseBounds(holeData.cameraBounds);
        }
        
        [ClientRpc]
        private void RpcUpdatePlayerList(PlayerListData[] players)
        {
            UIManager.Instance.UpdatePlayerList(players);
        }
        
        [ClientRpc]
        private void RpcUpdatePlayerShots(NetworkConnection conn, int shots)
        {
            UIManager.Instance.UpdatePlayerScore(conn, shots);
        }
        
        [ClientRpc]
        private void RpcPlayerFinishedHole(NetworkConnection conn, int shots, string scoreText)
        {
            UIManager.Instance.ShowPlayerFinished(conn, shots, scoreText);
            AudioManager.Instance.PlaySound("HoleComplete");
        }
        
        [ClientRpc]
        private void RpcEndMatch(List<KeyValuePair<NetworkConnection, int>> finalScores)
        {
            UIManager.Instance.ShowMatchResults(finalScores);
            AudioManager.Instance.PlaySound("MatchEnd");
        }
        
        [ClientRpc]
        private void RpcAwardRewards(NetworkConnection conn, int coins, int exp)
        {
            if (NetworkClient.connection == conn)
            {
                UIManager.Instance.ShowRewardPopup(coins, exp);
            }
        }
        
        [ClientRpc]
        private void RpcResetMatch()
        {
            UIManager.Instance.ResetMatchUI();
        }
        
        #endregion
        
        #region Utility Methods
        
        private PlayerListData[] GetPlayerList()
        {
            return playerData.Select(p => new PlayerListData
            {
                playerName = p.Value.profile.playerName,
                level = p.Value.profile.level,
                isReady = p.Value.isReady,
                currentScore = p.Value.currentHoleShots
            }).ToArray();
        }
        
        public PlayerMatchData GetPlayerData(NetworkConnection conn)
        {
            return playerData.ContainsKey(conn) ? playerData[conn] : null;
        }
        
        public MatchState GetMatchState() => currentState;
        public float GetMatchTimer() => matchTimer;
        public int GetCurrentHole() => currentHole;
        public GameMode GetGameMode() => currentGameMode;
        
        #endregion
    }
    
    #region Data Classes
    
    [System.Serializable]
    public class PlayerMatchData
    {
        public NetworkConnection connection;
        public PlayerProfile profile;
        public int[] scores;
        public int currentHoleShots;
        public int totalShots;
        public bool isReady;
        public GameObject ballInstance;
    }
    
    [System.Serializable]
    public struct PlayerListData
    {
        public string playerName;
        public int level;
        public bool isReady;
        public int currentScore;
    }
    
    public enum MatchState
    {
        WaitingForPlayers,
        Countdown,
        InProgress,
        MatchEnded
    }
    
    public enum GameMode
    {
        Multiplayer1v1,
        Multiplayer4Player,
        Tournament,
        Practice
    }
    
    #endregion
    
    #region Network Messages
    
    public struct PlayerReadyMessage : NetworkMessage
    {
        public bool isReady;
    }
    
    public struct PlayerShotMessage : NetworkMessage
    {
        public int shotCount;
    }
    
    #endregion
}