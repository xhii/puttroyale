using UnityEngine;
using Mirror;
using System.Collections.Generic;
using System.Linq;
using System.Collections;

namespace MicrogolfMasters
{
    public class TournamentManager : NetworkBehaviour
    {
        private static TournamentManager _instance;
        public static TournamentManager Instance => _instance;
        
        [Header("Tournament Configuration")]
        [SerializeField] private int playersPerMatch = 8;
        [SerializeField] private int winnersPerMatch = 4; // Top 4 advance
        [SerializeField] private float matchTimeLimit = 300f; // 5 minutes
        [SerializeField] private int holesPerRound = 3;
        
        [Header("Rewards")]
        [SerializeField] private int[] positionCoins = { 750, 500, 300, 200, 100, 50, 25, 10 };
        [SerializeField] private int[] positionCards = { 5, 4, 3, 2, 1, 1, 0, 0 };
        [SerializeField] private int[] positionGems = { 10, 5, 2, 0, 0, 0, 0, 0 };
        
        // Tournament state
        [SyncVar] private TournamentState currentState = TournamentState.Waiting;
        [SyncVar] private int currentRound = 0;
        [SyncVar] private int totalRounds = 0;
        
        // Player management
        private Dictionary<NetworkConnection, TournamentPlayer> tournamentPlayers = new Dictionary<NetworkConnection, TournamentPlayer>();
        private List<TournamentBracket> brackets = new List<TournamentBracket>();
        private TournamentBracket currentBracket;
        
        // Events
        public event System.Action<TournamentState> OnStateChanged;
        public event System.Action<int> OnRoundChanged;
        public event System.Action<List<TournamentPlayer>> OnBracketUpdated;
        public event System.Action<TournamentPlayer> OnPlayerEliminated;
        public event System.Action<TournamentPlayer> OnTournamentWinner;
        
        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
        }
        
        #region Tournament Setup
        
        [Server]
        public void InitializeTournament(List<PlayerMatchmakingData> players)
        {
            if (players.Count < playersPerMatch)
            {
                Debug.LogError("Not enough players for tournament");
                return;
            }
            
            // Clear previous tournament data
            tournamentPlayers.Clear();
            brackets.Clear();
            
            // Create tournament players
            foreach (var playerData in players)
            {
                TournamentPlayer tournamentPlayer = new TournamentPlayer
                {
                    playerId = playerData.playerId,
                    playerName = playerData.playerName,
                    connection = NetworkServer.connections.Values.FirstOrDefault(c => c.connectionId == playerData.connectionId),
                    totalScore = 0,
                    currentRoundScore = 0,
                    isEliminated = false,
                    finalPosition = 0
                };
                
                if (tournamentPlayer.connection != null)
                {
                    tournamentPlayers.Add(tournamentPlayer.connection, tournamentPlayer);
                }
            }
            
            // Calculate tournament structure
            CalculateTournamentStructure(tournamentPlayers.Count);
            
            // Start first round
            StartNextRound();
        }
        
        [Server]
        private void CalculateTournamentStructure(int totalPlayers)
        {
            totalRounds = 0;
            int remainingPlayers = totalPlayers;
            
            while (remainingPlayers > 1)
            {
                totalRounds++;
                int matchesThisRound = Mathf.CeilToInt((float)remainingPlayers / playersPerMatch);
                remainingPlayers = matchesThisRound * winnersPerMatch;
                
                if (remainingPlayers <= playersPerMatch)
                {
                    totalRounds++; // Final round
                    break;
                }
            }
            
            RpcUpdateTournamentInfo(totalRounds, totalPlayers);
        }
        
        #endregion
        
        #region Round Management
        
        [Server]
        private void StartNextRound()
        {
            currentRound++;
            currentState = TournamentState.RoundInProgress;
            
            // Get active players
            var activePlayers = tournamentPlayers.Values.Where(p => !p.isEliminated).ToList();
            
            if (activePlayers.Count <= playersPerMatch)
            {
                // Final round
                StartFinalRound(activePlayers);
            }
            else
            {
                // Create brackets for this round
                CreateRoundBrackets(activePlayers);
                
                // Start all bracket matches
                foreach (var bracket in brackets)
                {
                    StartBracketMatch(bracket);
                }
            }
            
            RpcRoundStarted(currentRound, totalRounds);
        }
        
        [Server]
        private void CreateRoundBrackets(List<TournamentPlayer> players)
        {
            brackets.Clear();
            
            // Shuffle players for random brackets
            players = players.OrderBy(x => Random.value).ToList();
            
            // Create brackets
            for (int i = 0; i < players.Count; i += playersPerMatch)
            {
                TournamentBracket bracket = new TournamentBracket
                {
                    bracketId = System.Guid.NewGuid().ToString(),
                    roundNumber = currentRound,
                    players = new List<TournamentPlayer>()
                };
                
                // Add players to bracket
                for (int j = 0; j < playersPerMatch && i + j < players.Count; j++)
                {
                    bracket.players.Add(players[i + j]);
                }
                
                brackets.Add(bracket);
            }
        }
        
        [Server]
        private void StartBracketMatch(TournamentBracket bracket)
        {
            // Create a sub-match for this bracket
            List<NetworkConnection> connections = bracket.players
                .Select(p => p.connection)
                .Where(c => c != null)
                .ToList();
            
            // Configure game manager for tournament rules
            GameManager.Instance.ConfigureTournamentMatch(connections, holesPerRound, matchTimeLimit);
            
            // Notify players in bracket
            foreach (var player in bracket.players)
            {
                if (player.connection != null)
                {
                    TargetBracketMatchStarted(player.connection, bracket);
                }
            }
        }
        
        [Server]
        private void StartFinalRound(List<TournamentPlayer> finalists)
        {
            currentState = TournamentState.FinalRound;
            
            // Create final bracket
            TournamentBracket finalBracket = new TournamentBracket
            {
                bracketId = "FINAL",
                roundNumber = currentRound,
                players = finalists,
                isFinal = true
            };
            
            brackets.Clear();
            brackets.Add(finalBracket);
            currentBracket = finalBracket;
            
            // Start final match with special rules
            List<NetworkConnection> connections = finalists
                .Select(p => p.connection)
                .Where(c => c != null)
                .ToList();
            
            GameManager.Instance.ConfigureTournamentMatch(connections, holesPerRound * 2, matchTimeLimit * 1.5f);
            
            RpcFinalRoundStarted(finalists.Select(p => p.playerName).ToArray());
        }
        
        #endregion
        
        #region Match Results
        
        [Server]
        public void ProcessBracketResults(string bracketId, Dictionary<NetworkConnection, int> scores)
        {
            TournamentBracket bracket = brackets.FirstOrDefault(b => b.bracketId == bracketId);
            if (bracket == null) return;
            
            // Update player scores
            foreach (var kvp in scores)
            {
                if (tournamentPlayers.ContainsKey(kvp.Key))
                {
                    tournamentPlayers[kvp.Key].currentRoundScore = kvp.Value;
                    tournamentPlayers[kvp.Key].totalScore += kvp.Value;
                }
            }
            
            // Sort players by score (lowest is best in golf)
            var sortedPlayers = bracket.players.OrderBy(p => p.currentRoundScore).ToList();
            
            if (bracket.isFinal)
            {
                // Process final results
                ProcessFinalResults(sortedPlayers);
            }
            else
            {
                // Eliminate players who didn't make top X
                for (int i = winnersPerMatch; i < sortedPlayers.Count; i++)
                {
                    sortedPlayers[i].isEliminated = true;
                    sortedPlayers[i].finalPosition = GetCurrentPosition();
                    
                    RpcPlayerEliminated(sortedPlayers[i].playerName, sortedPlayers[i].finalPosition);
                }
                
                // Mark bracket as complete
                bracket.isComplete = true;
                
                // Check if all brackets complete
                if (brackets.All(b => b.isComplete))
                {
                    EndRound();
                }
            }
        }
        
        [Server]
        private void ProcessFinalResults(List<TournamentPlayer> finalStandings)
        {
            currentState = TournamentState.Completed;
            
            // Assign final positions
            for (int i = 0; i < finalStandings.Count; i++)
            {
                finalStandings[i].finalPosition = i + 1;
                
                // Award prizes
                AwardTournamentPrizes(finalStandings[i], i);
            }
            
            // Announce winner
            TournamentPlayer winner = finalStandings[0];
            RpcTournamentCompleted(winner.playerName, finalStandings);
            
            OnTournamentWinner?.Invoke(winner);
            
            // End tournament after delay
            StartCoroutine(EndTournamentAfterDelay());
        }
        
        [Server]
        private void EndRound()
        {
            currentState = TournamentState.RoundComplete;
            
            // Show round results
            var roundResults = tournamentPlayers.Values
                .OrderBy(p => p.isEliminated ? 1 : 0)
                .ThenBy(p => p.totalScore)
                .ToList();
            
            RpcShowRoundResults(currentRound, roundResults);
            
            // Start next round after delay
            StartCoroutine(StartNextRoundAfterDelay());
        }
        
        #endregion
        
        #region Rewards
        
        [Server]
        private void AwardTournamentPrizes(TournamentPlayer player, int position)
        {
            if (player.connection == null) return;
            
            // Get player profile
            PlayerProfile profile = PlayerDataManager.Instance.GetPlayerProfile();
            if (profile == null) return;
            
            // Award coins
            if (position < positionCoins.Length)
            {
                profile.coins += positionCoins[position];
            }
            
            // Award cards
            if (position < positionCards.Length)
            {
                profile.cards += positionCards[position];
            }
            
            // Award gems
            if (position < positionGems.Length)
            {
                profile.gems += positionGems[position];
            }
            
            // Special rewards for winner
            if (position == 0)
            {
                // Unlock special tournament ball
                profile.UnlockBall(100); // Tournament champion ball
                
                // Achievement
                profile.wins++;
            }
            
            // Experience based on position
            int exp = 100 - (position * 10);
            profile.AddExperience(Mathf.Max(exp, 10));
            
            // Update stats
            profile.matchesPlayed++;
            
            // Send reward notification
            TargetReceiveTournamentRewards(player.connection, position, 
                positionCoins[position], positionCards[position], positionGems[position]);
        }
        
        #endregion
        
        #region Client RPCs
        
        [ClientRpc]
        private void RpcUpdateTournamentInfo(int rounds, int players)
        {
            UIManager.Instance.ShowTournamentInfo(rounds, players);
        }
        
        [ClientRpc]
        private void RpcRoundStarted(int round, int totalRounds)
        {
            OnRoundChanged?.Invoke(round);
            UIManager.Instance.ShowRoundStart(round, totalRounds);
        }
        
        [ClientRpc]
        private void RpcFinalRoundStarted(string[] finalistNames)
        {
            UIManager.Instance.ShowFinalRound(finalistNames);
            AudioManager.Instance.PlaySound("TournamentFinal");
        }
        
        [ClientRpc]
        private void RpcPlayerEliminated(string playerName, int position)
        {
            UIManager.Instance.ShowPlayerEliminated(playerName, position);
        }
        
        [ClientRpc]
        private void RpcShowRoundResults(int round, List<TournamentPlayer> standings)
        {
            UIManager.Instance.ShowTournamentStandings(round, standings);
        }
        
        [ClientRpc]
        private void RpcTournamentCompleted(string winnerName, List<TournamentPlayer> finalStandings)
        {
            UIManager.Instance.ShowTournamentResults(winnerName, finalStandings);
            AudioManager.Instance.PlaySound("TournamentVictory");
            ParticleManager.Instance.PlayVictoryCelebration(Vector3.zero);
        }
        
        [TargetRpc]
        private void TargetBracketMatchStarted(NetworkConnection target, TournamentBracket bracket)
        {
            UIManager.Instance.ShowBracketInfo(bracket);
        }
        
        [TargetRpc]
        private void TargetReceiveTournamentRewards(NetworkConnection target, int position, int coins, int cards, int gems)
        {
            UIManager.Instance.ShowTournamentRewards(position, coins, cards, gems);
        }
        
        #endregion
        
        #region Utility
        
        private IEnumerator StartNextRoundAfterDelay()
        {
            yield return new WaitForSeconds(5f);
            
            if (tournamentPlayers.Values.Count(p => !p.isEliminated) > 1)
            {
                StartNextRound();
            }
        }
        
        private IEnumerator EndTournamentAfterDelay()
        {
            yield return new WaitForSeconds(10f);
            
            // Return all players to main menu
            foreach (var player in tournamentPlayers.Values)
            {
                if (player.connection != null)
                {
                    TargetReturnToMenu(player.connection);
                }
            }
            
            // Reset tournament
            ResetTournament();
        }
        
        [Server]
        private void ResetTournament()
        {
            currentState = TournamentState.Waiting;
            currentRound = 0;
            totalRounds = 0;
            tournamentPlayers.Clear();
            brackets.Clear();
            currentBracket = null;
        }
        
        private int GetCurrentPosition()
        {
            // Calculate position based on remaining players
            return tournamentPlayers.Values.Count(p => !p.isEliminated) + 1;
        }
        
        [TargetRpc]
        private void TargetReturnToMenu(NetworkConnection target)
        {
            UIManager.Instance.ShowMainMenu();
        }
        
        public TournamentState GetCurrentState() => currentState;
        public int GetCurrentRound() => currentRound;
        public int GetTotalRounds() => totalRounds;
        
        #endregion
    }
    
    #region Data Classes
    
    [System.Serializable]
    public class TournamentPlayer
    {
        public string playerId;
        public string playerName;
        public NetworkConnection connection;
        public int totalScore;
        public int currentRoundScore;
        public bool isEliminated;
        public int finalPosition;
    }
    
    [System.Serializable]
    public class TournamentBracket
    {
        public string bracketId;
        public int roundNumber;
        public List<TournamentPlayer> players;
        public bool isComplete;
        public bool isFinal;
    }
    
    public enum TournamentState
    {
        Waiting,
        RoundInProgress,
        RoundComplete,
        FinalRound,
        Completed
    }
    
    #endregion
}