using UnityEngine;
using Mirror;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.Events;

namespace MicrogolfMasters
{
    public class NetworkMatchmaker : NetworkBehaviour
    {
        private static NetworkMatchmaker _instance;
        public static NetworkMatchmaker Instance => _instance;
        
        [Header("Matchmaking Configuration")]
        [SerializeField] private float matchmakingTimeout = 30f;
        [SerializeField] private int minPlayersFor1v1 = 2;
        [SerializeField] private int minPlayersFor4Player = 4;
        [SerializeField] private int minPlayersForTournament = 8;
        [SerializeField] private bool enableSkillBasedMatchmaking = true;
        
        [Header("Server Configuration")]
        [SerializeField] private string masterServerAddress = "localhost";
        [SerializeField] private int masterServerPort = 7777;
        [SerializeField] private int maxMatchServers = 100;
        
        [Header("Events")]
        public UnityEvent<int> OnPlayersFound;
        public UnityEvent<string> OnMatchFound;
        public UnityEvent OnMatchmakingCancelled;
        public UnityEvent<string> OnMatchmakingError;
        
        // Matchmaking state
        private bool isSearching = false;
        private GameMode currentSearchMode;
        private float searchStartTime;
        private List<PlayerMatchmakingData> matchmakingQueue = new List<PlayerMatchmakingData>();
        private Dictionary<string, MatchData> activeMatches = new Dictionary<string, MatchData>();
        
        // Player data
        private PlayerMatchmakingData localPlayerData;
        private Coroutine matchmakingCoroutine;
        
        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
        }
        
        #region Client Methods
        
        [Client]
        public void StartMatchmaking(GameMode mode)
        {
            if (isSearching) return;
            
            isSearching = true;
            currentSearchMode = mode;
            searchStartTime = Time.time;
            
            // Create local player data
            localPlayerData = new PlayerMatchmakingData
            {
                playerId = PlayerDataManager.Instance.GetLocalPlayerId(),
                playerName = PlayerDataManager.Instance.GetPlayerName(),
                league = PlayerDataManager.Instance.GetPlayerLeague(),
                level = PlayerDataManager.Instance.GetPlayerLevel(),
                elo = PlayerDataManager.Instance.GetPlayerElo(),
                selectedBallId = PlayerDataManager.Instance.GetSelectedBallId(),
                connectionId = NetworkClient.connection.connectionId
            };
            
            // Send matchmaking request to server
            CmdRequestMatchmaking(localPlayerData, mode);
            
            // Start timeout coroutine
            matchmakingCoroutine = StartCoroutine(MatchmakingTimeout());
        }
        
        [Client]
        public void CancelMatchmaking()
        {
            if (!isSearching) return;
            
            isSearching = false;
            
            if (matchmakingCoroutine != null)
            {
                StopCoroutine(matchmakingCoroutine);
            }
            
            CmdCancelMatchmaking(localPlayerData.playerId);
            OnMatchmakingCancelled?.Invoke();
        }
        
        private IEnumerator MatchmakingTimeout()
        {
            yield return new WaitForSeconds(matchmakingTimeout);
            
            if (isSearching)
            {
                CancelMatchmaking();
                OnMatchmakingError?.Invoke("Matchmaking timeout - no players found");
            }
        }
        
        #endregion
        
        #region Server Methods
        
        [Command(requiresAuthority = false)]
        private void CmdRequestMatchmaking(PlayerMatchmakingData playerData, GameMode mode)
        {
            if (!isServer) return;
            
            // Add player to queue
            playerData.requestTime = Time.time;
            playerData.gameMode = mode;
            matchmakingQueue.Add(playerData);
            
            // Try to create matches
            TryCreateMatches();
            
            // Update all clients about queue size
            UpdateQueueStatus();
        }
        
        [Command(requiresAuthority = false)]
        private void CmdCancelMatchmaking(string playerId)
        {
            if (!isServer) return;
            
            matchmakingQueue.RemoveAll(p => p.playerId == playerId);
            UpdateQueueStatus();
        }
        
        [Server]
        private void TryCreateMatches()
        {
            // Try to create tournaments first (highest player requirement)
            TryCreateTournamentMatches();
            
            // Then 4-player matches
            TryCreate4PlayerMatches();
            
            // Finally 1v1 matches
            TryCreate1v1Matches();
        }
        
        [Server]
        private void TryCreateTournamentMatches()
        {
            var tournamentPlayers = matchmakingQueue
                .Where(p => p.gameMode == GameMode.Tournament)
                .OrderBy(p => p.requestTime)
                .ToList();
            
            while (tournamentPlayers.Count >= minPlayersForTournament)
            {
                var matchPlayers = tournamentPlayers.Take(minPlayersForTournament).ToList();
                CreateMatch(matchPlayers, GameMode.Tournament);
                
                // Remove from both lists
                foreach (var player in matchPlayers)
                {
                    tournamentPlayers.Remove(player);
                    matchmakingQueue.Remove(player);
                }
            }
        }
        
        [Server]
        private void TryCreate4PlayerMatches()
        {
            var fourPlayerQueue = matchmakingQueue
                .Where(p => p.gameMode == GameMode.Multiplayer4Player)
                .ToList();
            
            if (enableSkillBasedMatchmaking)
            {
                // Group by similar skill
                var skillGroups = GroupBySkill(fourPlayerQueue, 200); // 200 ELO range
                
                foreach (var group in skillGroups)
                {
                    while (group.Count >= minPlayersFor4Player)
                    {
                        var matchPlayers = group.Take(minPlayersFor4Player).ToList();
                        CreateMatch(matchPlayers, GameMode.Multiplayer4Player);
                        
                        foreach (var player in matchPlayers)
                        {
                            group.Remove(player);
                            matchmakingQueue.Remove(player);
                        }
                    }
                }
            }
            else
            {
                // Simple FIFO matching
                while (fourPlayerQueue.Count >= minPlayersFor4Player)
                {
                    var matchPlayers = fourPlayerQueue.Take(minPlayersFor4Player).ToList();
                    CreateMatch(matchPlayers, GameMode.Multiplayer4Player);
                    
                    foreach (var player in matchPlayers)
                    {
                        fourPlayerQueue.Remove(player);
                        matchmakingQueue.Remove(player);
                    }
                }
            }
        }
        
        [Server]
        private void TryCreate1v1Matches()
        {
            var oneVOneQueue = matchmakingQueue
                .Where(p => p.gameMode == GameMode.Multiplayer1v1)
                .ToList();
            
            if (enableSkillBasedMatchmaking)
            {
                // Sort by ELO
                oneVOneQueue = oneVOneQueue.OrderBy(p => p.elo).ToList();
                
                // Match adjacent players
                while (oneVOneQueue.Count >= minPlayersFor1v1)
                {
                    // Find best match for first player
                    var player1 = oneVOneQueue[0];
                    var player2 = FindBestMatch(player1, oneVOneQueue.Skip(1).ToList());
                    
                    if (player2 != null)
                    {
                        CreateMatch(new List<PlayerMatchmakingData> { player1, player2 }, GameMode.Multiplayer1v1);
                        
                        oneVOneQueue.Remove(player1);
                        oneVOneQueue.Remove(player2);
                        matchmakingQueue.Remove(player1);
                        matchmakingQueue.Remove(player2);
                    }
                    else
                    {
                        // No suitable match found, wait
                        break;
                    }
                }
            }
            else
            {
                // Simple pairing
                while (oneVOneQueue.Count >= minPlayersFor1v1)
                {
                    var matchPlayers = oneVOneQueue.Take(minPlayersFor1v1).ToList();
                    CreateMatch(matchPlayers, GameMode.Multiplayer1v1);
                    
                    foreach (var player in matchPlayers)
                    {
                        oneVOneQueue.Remove(player);
                        matchmakingQueue.Remove(player);
                    }
                }
            }
        }
        
        [Server]
        private PlayerMatchmakingData FindBestMatch(PlayerMatchmakingData player, List<PlayerMatchmakingData> candidates)
        {
            // Start with tight ELO range and expand over time
            float waitTime = Time.time - player.requestTime;
            int eloRange = 50 + (int)(waitTime * 10); // Expand by 10 ELO per second
            
            var validCandidates = candidates
                .Where(c => Mathf.Abs(c.elo - player.elo) <= eloRange)
                .OrderBy(c => Mathf.Abs(c.elo - player.elo))
                .ToList();
            
            return validCandidates.FirstOrDefault();
        }
        
        [Server]
        private List<List<PlayerMatchmakingData>> GroupBySkill(List<PlayerMatchmakingData> players, int eloRange)
        {
            var groups = new List<List<PlayerMatchmakingData>>();
            var sortedPlayers = players.OrderBy(p => p.elo).ToList();
            
            while (sortedPlayers.Count > 0)
            {
                var group = new List<PlayerMatchmakingData>();
                var baseElo = sortedPlayers[0].elo;
                
                // Add all players within ELO range
                var playersInRange = sortedPlayers
                    .Where(p => Mathf.Abs(p.elo - baseElo) <= eloRange)
                    .ToList();
                
                group.AddRange(playersInRange);
                
                // Remove from sorted list
                foreach (var player in playersInRange)
                {
                    sortedPlayers.Remove(player);
                }
                
                groups.Add(group);
            }
            
            return groups;
        }
        
        [Server]
        private void CreateMatch(List<PlayerMatchmakingData> players, GameMode mode)
        {
            // Generate match ID
            string matchId = System.Guid.NewGuid().ToString();
            
            // Create match data
            MatchData match = new MatchData
            {
                matchId = matchId,
                gameMode = mode,
                players = players,
                courseId = SelectCourse(players),
                matchServer = AllocateMatchServer(),
                createdTime = Time.time
            };
            
            activeMatches.Add(matchId, match);
            
            // Notify all players
            foreach (var player in players)
            {
                var conn = NetworkServer.connections.Values
                    .FirstOrDefault(c => c.connectionId == player.connectionId);
                
                if (conn != null)
                {
                    TargetMatchFound(conn, matchId, match.matchServer);
                }
            }
            
            // Start match on dedicated server
            StartMatchOnServer(match);
        }
        
        [Server]
        private int SelectCourse(List<PlayerMatchmakingData> players)
        {
            // Select course based on average player league
            float avgLeague = players.Average(p => p.league);
            
            // Map league to course difficulty
            if (avgLeague >= 15) return 0; // Easy course
            if (avgLeague >= 10) return 1; // Medium course
            if (avgLeague >= 5) return 2;  // Hard course
            return 3; // Expert course
        }
        
        [Server]
        private string AllocateMatchServer()
        {
            // In production, this would allocate a dedicated game server
            // For now, return the main server address
            return $"{masterServerAddress}:{masterServerPort}";
        }
        
        [Server]
        private void StartMatchOnServer(MatchData match)
        {
            // In production, this would communicate with the match server
            // to start the game instance
            
            // For now, just log
            Debug.Log($"Starting match {match.matchId} on server {match.matchServer}");
        }
        
        [Server]
        private void UpdateQueueStatus()
        {
            // Update all clients about queue sizes
            Dictionary<GameMode, int> queueSizes = new Dictionary<GameMode, int>
            {
                { GameMode.Multiplayer1v1, matchmakingQueue.Count(p => p.gameMode == GameMode.Multiplayer1v1) },
                { GameMode.Multiplayer4Player, matchmakingQueue.Count(p => p.gameMode == GameMode.Multiplayer4Player) },
                { GameMode.Tournament, matchmakingQueue.Count(p => p.gameMode == GameMode.Tournament) }
            };
            
            RpcUpdateQueueStatus(queueSizes);
        }
        
        #endregion
        
        #region Client RPCs
        
        [TargetRpc]
        private void TargetMatchFound(NetworkConnection target, string matchId, string serverAddress)
        {
            isSearching = false;
            
            if (matchmakingCoroutine != null)
            {
                StopCoroutine(matchmakingCoroutine);
            }
            
            OnMatchFound?.Invoke(matchId);
            
            // Connect to match server
            ConnectToMatchServer(serverAddress, matchId);
        }
        
        [ClientRpc]
        private void RpcUpdateQueueStatus(Dictionary<GameMode, int> queueSizes)
        {
            if (isSearching && queueSizes.ContainsKey(currentSearchMode))
            {
                OnPlayersFound?.Invoke(queueSizes[currentSearchMode]);
            }
        }
        
        #endregion
        
        #region Match Connection
        
        [Client]
        private void ConnectToMatchServer(string serverAddress, string matchId)
        {
            // Store match ID for use after connection
            PlayerPrefs.SetString("CurrentMatchId", matchId);
            
            // If already on the correct server, just join the match
            if (NetworkClient.isConnected && NetworkClient.serverIp == masterServerAddress)
            {
                CmdJoinMatch(matchId);
            }
            else
            {
                // Disconnect from matchmaking server and connect to match server
                NetworkClient.Disconnect();
                
                // Parse server address
                string[] parts = serverAddress.Split(':');
                string ip = parts[0];
                int port = int.Parse(parts[1]);
                
                // Connect to match server
                NetworkClient.Connect(ip, port);
            }
        }
        
        [Command(requiresAuthority = false)]
        private void CmdJoinMatch(string matchId)
        {
            if (!activeMatches.ContainsKey(matchId)) return;
            
            MatchData match = activeMatches[matchId];
            
            // Set up game manager for this match
            GameManager.Instance.SetupMatch(match);
            
            // Remove match from active list (it's now being handled by GameManager)
            activeMatches.Remove(matchId);
        }
        
        #endregion
        
        #region Utility Methods
        
        public bool IsSearching() => isSearching;
        public float GetSearchTime() => isSearching ? Time.time - searchStartTime : 0f;
        public GameMode GetCurrentSearchMode() => currentSearchMode;
        
        #endregion
    }
    
    #region Data Classes
    
    [System.Serializable]
    public class PlayerMatchmakingData
    {
        public string playerId;
        public string playerName;
        public int league;
        public int level;
        public int elo;
        public int selectedBallId;
        public int connectionId;
        public GameMode gameMode;
        public float requestTime;
    }
    
    [System.Serializable]
    public class MatchData
    {
        public string matchId;
        public GameMode gameMode;
        public List<PlayerMatchmakingData> players;
        public int courseId;
        public string matchServer;
        public float createdTime;
    }
    
    #endregion
}