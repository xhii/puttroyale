using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using System.Linq;
using Mirror;

namespace MicrogolfMasters
{
    public class UIManager : MonoBehaviour
    {
        private static UIManager _instance;
        public static UIManager Instance => _instance;
        
        [Header("Screens")]
        [SerializeField] private GameObject mainMenuScreen;
        [SerializeField] private GameObject gameplayScreen;
        [SerializeField] private GameObject matchmakingScreen;
        [SerializeField] private GameObject resultsScreen;
        [SerializeField] private GameObject shopScreen;
        [SerializeField] private GameObject profileScreen;
        [SerializeField] private GameObject settingsScreen;
        [SerializeField] private GameObject ballSelectionScreen;
        
        [Header("Main Menu")]
        [SerializeField] private Button play1v1Button;
        [SerializeField] private Button play4PlayerButton;
        [SerializeField] private Button tournamentButton;
        [SerializeField] private TextMeshProUGUI playerNameText;
        [SerializeField] private TextMeshProUGUI levelText;
        [SerializeField] private TextMeshProUGUI leagueText;
        [SerializeField] private TextMeshProUGUI coinsText;
        [SerializeField] private TextMeshProUGUI gemsText;
        [SerializeField] private TextMeshProUGUI energyText;
        [SerializeField] private Slider energyTimerSlider;
        
        [Header("Gameplay HUD")]
        [SerializeField] private GameObject powerMeter;
        [SerializeField] private Slider powerSlider;
        [SerializeField] private TextMeshProUGUI shotCountText;
        [SerializeField] private TextMeshProUGUI holeInfoText;
        [SerializeField] private TextMeshProUGUI parText;
        [SerializeField] private Transform playerListContainer;
        [SerializeField] private GameObject playerListItemPrefab;
        [SerializeField] private TextMeshProUGUI timerText;
        [SerializeField] private GameObject chatPanel;
        [SerializeField] private Button[] emojiButtons;
        
        [Header("Matchmaking")]
        [SerializeField] private TextMeshProUGUI searchingText;
        [SerializeField] private TextMeshProUGUI playersFoundText;
        [SerializeField] private Button cancelSearchButton;
        [SerializeField] private GameObject[] playerSlots;
        
        [Header("Results")]
        [SerializeField] private Transform resultsListContainer;
        [SerializeField] private GameObject resultsItemPrefab;
        [SerializeField] private TextMeshProUGUI winnerText;
        [SerializeField] private TextMeshProUGUI rewardCoinsText;
        [SerializeField] private TextMeshProUGUI rewardExpText;
        [SerializeField] private TextMeshProUGUI rewardCardsText;
        [SerializeField] private Button continueButton;
        
        [Header("Shop")]
        [SerializeField] private Transform ballGridContainer;
        [SerializeField] private GameObject ballShopItemPrefab;
        [SerializeField] private TabGroup shopTabGroup;
        [SerializeField] private Button purchaseButton;
        [SerializeField] private TextMeshProUGUI selectedBallName;
        [SerializeField] private TextMeshProUGUI selectedBallStats;
        [SerializeField] private TextMeshProUGUI selectedBallPrice;
        
        [Header("Ball Selection")]
        [SerializeField] private Transform ballInventoryContainer;
        [SerializeField] private GameObject ballInventoryItemPrefab;
        [SerializeField] private TextMeshProUGUI selectedBallInfoText;
        [SerializeField] private Button selectBallButton;
        [SerializeField] private Button repairBallButton;
        [SerializeField] private GameObject durabilityBar;
        
        [Header("Profile")]
        [SerializeField] private TextMeshProUGUI profileStatsText;
        [SerializeField] private TextMeshProUGUI winRateText;
        [SerializeField] private TextMeshProUGUI averageShotsText;
        [SerializeField] private TextMeshProUGUI bestScoreText;
        [SerializeField] private Button upgradeStrengthButton;
        [SerializeField] private Button upgradeAccuracyButton;
        [SerializeField] private TextMeshProUGUI strengthLevelText;
        [SerializeField] private TextMeshProUGUI accuracyLevelText;
        
        [Header("Popups")]
        [SerializeField] private GameObject messagePopup;
        [SerializeField] private TextMeshProUGUI messageText;
        [SerializeField] private GameObject rewardPopup;
        [SerializeField] private GameObject countdownPopup;
        [SerializeField] private TextMeshProUGUI countdownText;
        [SerializeField] private GameObject dailyRewardPopup;
        
        [Header("Effects")]
        [SerializeField] private GameObject holeInOneEffect;
        [SerializeField] private GameObject perfectShotEffect;
        [SerializeField] private ParticleSystem coinCollectEffect;
        
        // State
        private Dictionary<NetworkConnection, PlayerUIData> playerUIData = new Dictionary<NetworkConnection, PlayerUIData>();
        private PlayerProfile localPlayerProfile;
        private GolfBallController localBallController;
        private Coroutine energyTimerCoroutine;
        
        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            
            InitializeUI();
        }
        
        private void InitializeUI()
        {
            // Set up button listeners
            play1v1Button.onClick.AddListener(() => OnPlayModeSelected(GameMode.Multiplayer1v1));
            play4PlayerButton.onClick.AddListener(() => OnPlayModeSelected(GameMode.Multiplayer4Player));
            tournamentButton.onClick.AddListener(() => OnPlayModeSelected(GameMode.Tournament));
            cancelSearchButton.onClick.AddListener(OnCancelMatchmaking);
            continueButton.onClick.AddListener(OnContinueFromResults);
            
            // Initialize all screens
            ShowMainMenu();
            
            // Start energy timer
            energyTimerCoroutine = StartCoroutine(UpdateEnergyTimer());
        }
        
        #region Screen Management
        
        public void ShowMainMenu()
        {
            HideAllScreens();
            mainMenuScreen.SetActive(true);
            UpdateMainMenuUI();
        }
        
        public void ShowGameplay()
        {
            HideAllScreens();
            gameplayScreen.SetActive(true);
            ResetGameplayUI();
        }
        
        public void ShowMatchmaking()
        {
            HideAllScreens();
            matchmakingScreen.SetActive(true);
            searchingText.text = "Searching for players...";
        }
        
        public void ShowResults()
        {
            HideAllScreens();
            resultsScreen.SetActive(true);
        }
        
        public void ShowShop()
        {
            HideAllScreens();
            shopScreen.SetActive(true);
            PopulateShop();
        }
        
        public void ShowBallSelection()
        {
            HideAllScreens();
            ballSelectionScreen.SetActive(true);
            PopulateBallInventory();
        }
        
        public void ShowProfile()
        {
            HideAllScreens();
            profileScreen.SetActive(true);
            UpdateProfileUI();
        }
        
        private void HideAllScreens()
        {
            mainMenuScreen.SetActive(false);
            gameplayScreen.SetActive(false);
            matchmakingScreen.SetActive(false);
            resultsScreen.SetActive(false);
            shopScreen.SetActive(false);
            profileScreen.SetActive(false);
            settingsScreen.SetActive(false);
            ballSelectionScreen.SetActive(false);
        }
        
        #endregion
        
        #region Main Menu
        
        private void UpdateMainMenuUI()
        {
            if (localPlayerProfile == null) return;
            
            playerNameText.text = localPlayerProfile.playerName;
            levelText.text = $"Level {localPlayerProfile.level}";
            leagueText.text = localPlayerProfile.GetLeagueName();
            UpdateCurrencyUI();
            UpdateEnergyUI();
        }
        
        private void UpdateCurrencyUI()
        {
            if (localPlayerProfile == null) return;
            
            coinsText.text = localPlayerProfile.coins.ToString();
            gemsText.text = localPlayerProfile.gems.ToString();
        }
        
        private void UpdateEnergyUI()
        {
            if (localPlayerProfile == null) return;
            
            energyText.text = $"{localPlayerProfile.currentEnergy}/{localPlayerProfile.maxEnergy}";
            
            // Update energy timer
            if (localPlayerProfile.currentEnergy < localPlayerProfile.maxEnergy)
            {
                energyTimerSlider.gameObject.SetActive(true);
                var timeUntilNext = localPlayerProfile.GetTimeUntilNextEnergy();
                energyTimerSlider.value = 1f - (float)(timeUntilNext.TotalMinutes / 20f);
            }
            else
            {
                energyTimerSlider.gameObject.SetActive(false);
            }
        }
        
        private IEnumerator UpdateEnergyTimer()
        {
            while (true)
            {
                UpdateEnergyUI();
                yield return new WaitForSeconds(1f);
            }
        }
        
        private void OnPlayModeSelected(GameMode mode)
        {
            int energyCost = mode == GameMode.Tournament ? 2 : 1;
            
            if (localPlayerProfile.UseEnergy(energyCost))
            {
                NetworkMatchmaker.Instance.StartMatchmaking(mode);
                ShowMatchmaking();
            }
            else
            {
                ShowMessage("Not enough energy!", 2f);
                // Show energy refill popup
                ShowEnergyRefillPopup();
            }
        }
        
        #endregion
        
        #region Gameplay UI
        
        public void SetLocalPlayer(GolfBallController ballController)
        {
            localBallController = ballController;
        }
        
        private void ResetGameplayUI()
        {
            shotCountText.text = "Shots: 0";
            powerMeter.SetActive(false);
            powerSlider.value = 0f;
            
            // Clear player list
            foreach (Transform child in playerListContainer)
            {
                Destroy(child.gameObject);
            }
            playerUIData.Clear();
        }
        
        public void UpdatePowerMeter(float power)
        {
            powerMeter.SetActive(power > 0);
            powerSlider.value = power;
            
            // Color gradient
            Color meterColor = Color.Lerp(Color.green, Color.red, power);
            powerSlider.fillRect.GetComponent<Image>().color = meterColor;
        }
        
        public void UpdateShotCount(int shots)
        {
            shotCountText.text = $"Shots: {shots}";
        }
        
        public void UpdateHoleInfo(int holeNumber, int par)
        {
            holeInfoText.text = $"Hole {holeNumber}";
            parText.text = $"Par {par}";
        }
        
        public void UpdateMatchTimer(float timeRemaining)
        {
            int minutes = Mathf.FloorToInt(timeRemaining / 60f);
            int seconds = Mathf.FloorToInt(timeRemaining % 60f);
            timerText.text = $"{minutes:00}:{seconds:00}";
            
            // Flash timer when low
            if (timeRemaining < 30f)
            {
                timerText.color = Color.Lerp(Color.white, Color.red, Mathf.PingPong(Time.time * 2f, 1f));
            }
        }
        
        #endregion
        
        #region Player List
        
        public void UpdatePlayerList(PlayerListData[] players)
        {
            // Clear existing
            foreach (Transform child in playerListContainer)
            {
                Destroy(child.gameObject);
            }
            playerUIData.Clear();
            
            // Create new items
            foreach (var playerData in players)
            {
                GameObject item = Instantiate(playerListItemPrefab, playerListContainer);
                PlayerListItem listItem = item.GetComponent<PlayerListItem>();
                listItem.Setup(playerData);
                
                // Store reference
                playerUIData[playerData.connection] = new PlayerUIData
                {
                    listItem = listItem,
                    playerData = playerData
                };
            }
        }
        
        public void UpdatePlayerScore(NetworkConnection conn, int shots)
        {
            if (playerUIData.ContainsKey(conn))
            {
                playerUIData[conn].listItem.UpdateScore(shots);
            }
        }
        
        #endregion
        
        #region Match States
        
        public void UpdateMatchState(MatchState state)
        {
            switch (state)
            {
                case MatchState.WaitingForPlayers:
                    ShowMessage("Waiting for players...", 0f);
                    break;
                case MatchState.Countdown:
                    // Handled by countdown popup
                    break;
                case MatchState.InProgress:
                    HideMessage();
                    break;
                case MatchState.MatchEnded:
                    // Handled by results screen
                    break;
            }
        }
        
        public void ShowCountdown(int seconds)
        {
            countdownPopup.SetActive(true);
            countdownText.text = seconds.ToString();
            
            // Animate countdown
            LeanTween.scale(countdownText.gameObject, Vector3.one * 1.2f, 0.5f)
                .setEase(LeanTweenType.easeOutBack)
                .setOnComplete(() => {
                    LeanTween.scale(countdownText.gameObject, Vector3.one, 0.3f);
                });
        }
        
        public void ShowMatchStarted()
        {
            countdownPopup.SetActive(false);
            ShowMessage("GO!", 1f);
            AudioManager.Instance.PlaySound("MatchStart");
        }
        
        #endregion
        
        #region Match Results
        
        public void ShowMatchResults(List<KeyValuePair<NetworkConnection, int>> finalScores)
        {
            ShowResults();
            
            // Clear previous results
            foreach (Transform child in resultsListContainer)
            {
                Destroy(child.gameObject);
            }
            
            // Display sorted results
            int position = 1;
            foreach (var score in finalScores)
            {
                GameObject resultItem = Instantiate(resultsItemPrefab, resultsListContainer);
                ResultListItem item = resultItem.GetComponent<ResultListItem>();
                
                if (playerUIData.ContainsKey(score.Key))
                {
                    item.Setup(position, playerUIData[score.Key].playerData.playerName, score.Value);
                }
                
                position++;
            }
            
            // Show winner
            if (finalScores.Count > 0 && playerUIData.ContainsKey(finalScores[0].Key))
            {
                winnerText.text = $"{playerUIData[finalScores[0].Key].playerData.playerName} Wins!";
            }
        }
        
        public void ShowRewardPopup(int coins, int exp, int cards = 0)
        {
            rewardPopup.SetActive(true);
            rewardCoinsText.text = $"+{coins}";
            rewardExpText.text = $"+{exp} XP";
            if (cards > 0)
            {
                rewardCardsText.text = $"+{cards} Cards";
            }
            
            // Animate rewards
            LeanTween.scale(rewardPopup, Vector3.one, 0.5f).setEase(LeanTweenType.easeOutBack);
            
            // Play collect effect
            if (coinCollectEffect != null)
            {
                coinCollectEffect.Play();
            }
        }
        
        #endregion
        
        #region Shop
        
        private void PopulateShop()
        {
            // Clear existing items
            foreach (Transform child in ballGridContainer)
            {
                Destroy(child.gameObject);
            }
            
            // Get available balls
            List<BallData> shopBalls = BallDatabase.Instance.GetPurchasableBalls(
                localPlayerProfile.level,
                localPlayerProfile.coins,
                localPlayerProfile.gems
            );
            
            // Create shop items
            foreach (var ball in shopBalls)
            {
                GameObject shopItem = Instantiate(ballShopItemPrefab, ballGridContainer);
                BallShopItem item = shopItem.GetComponent<BallShopItem>();
                item.Setup(ball, OnBallShopItemSelected);
            }
        }
        
        private void OnBallShopItemSelected(BallData ball)
        {
            selectedBallName.text = ball.ballName;
            selectedBallStats.text = $"Strength: {ball.strength}\nAccuracy: {ball.accuracy}";
            
            if (ball.purchaseType == CurrencyType.Coins)
            {
                selectedBallPrice.text = $"{ball.price} Coins";
            }
            else
            {
                selectedBallPrice.text = $"{ball.price} Gems";
            }
            
            purchaseButton.interactable = true;
            purchaseButton.onClick.RemoveAllListeners();
            purchaseButton.onClick.AddListener(() => OnPurchaseBall(ball));
        }
        
        private void OnPurchaseBall(BallData ball)
        {
            if (localPlayerProfile.PurchaseBall(ball.id))
            {
                ShowMessage($"{ball.ballName} purchased!", 2f);
                UpdateCurrencyUI();
                PopulateShop(); // Refresh
            }
            else
            {
                ShowMessage("Not enough currency!", 2f);
            }
        }
        
        #endregion
        
        #region Ball Selection
        
        private void PopulateBallInventory()
        {
            // Clear existing items
            foreach (Transform child in ballInventoryContainer)
            {
                Destroy(child.gameObject);
            }
            
            // Create inventory items
            for (int i = 0; i < localPlayerProfile.ownedBalls.Count; i++)
            {
                OwnedBall ownedBall = localPlayerProfile.ownedBalls[i];
                GameObject invItem = Instantiate(ballInventoryItemPrefab, ballInventoryContainer);
                BallInventoryItem item = invItem.GetComponent<BallInventoryItem>();
                
                int index = i; // Capture for closure
                item.Setup(ownedBall, () => OnBallInventoryItemSelected(index));
            }
        }
        
        private void OnBallInventoryItemSelected(int index)
        {
            OwnedBall ball = localPlayerProfile.ownedBalls[index];
            
            selectedBallInfoText.text = $"{ball.ballData.ballName}\n" +
                                      $"Durability: {ball.durability}/{ball.maxDurability}\n" +
                                      $"Strength: {ball.ballData.strength}\n" +
                                      $"Accuracy: {ball.ballData.accuracy}";
            
            selectBallButton.interactable = !ball.isRepairing && ball.durability > 0;
            selectBallButton.onClick.RemoveAllListeners();
            selectBallButton.onClick.AddListener(() => {
                localPlayerProfile.SelectBall(index);
                ShowMessage($"{ball.ballData.ballName} selected!", 1f);
            });
            
            repairBallButton.gameObject.SetActive(ball.durability < ball.maxDurability);
            repairBallButton.onClick.RemoveAllListeners();
            repairBallButton.onClick.AddListener(() => OnRepairBall(index));
        }
        
        private void OnRepairBall(int index)
        {
            if (localPlayerProfile.InstantRepairBall(index))
            {
                ShowMessage("Ball repaired!", 1f);
                UpdateCurrencyUI();
                PopulateBallInventory(); // Refresh
            }
            else
            {
                ShowMessage("Not enough gems!", 2f);
            }
        }
        
        #endregion
        
        #region Profile
        
        private void UpdateProfileUI()
        {
            profileStatsText.text = $"Matches Played: {localPlayerProfile.matchesPlayed}\n" +
                                   $"Wins: {localPlayerProfile.wins}\n" +
                                   $"Holes in One: {localPlayerProfile.holesInOne}";
            
            winRateText.text = $"Win Rate: {localPlayerProfile.GetWinRate():P}";
            averageShotsText.text = $"Avg Shots: {localPlayerProfile.averageShots:F1}";
            bestScoreText.text = $"Best Score: {localPlayerProfile.bestScore}";
            
            strengthLevelText.text = $"Strength Lv.{localPlayerProfile.strengthLevel}";
            accuracyLevelText.text = $"Accuracy Lv.{localPlayerProfile.accuracyLevel}";
            
            // Update upgrade buttons
            UpdateUpgradeButtons();
        }
        
        private void UpdateUpgradeButtons()
        {
            // Strength upgrade
            int strengthCost = 100 * localPlayerProfile.strengthLevel * localPlayerProfile.strengthLevel;
            int strengthCards = 10 + localPlayerProfile.strengthLevel * 5;
            
            upgradeStrengthButton.GetComponentInChildren<TextMeshProUGUI>().text = 
                $"Upgrade\n{strengthCost} Coins\n{strengthCards} Cards";
            
            upgradeStrengthButton.interactable = 
                localPlayerProfile.coins >= strengthCost && 
                localPlayerProfile.cards >= strengthCards;
            
            // Accuracy upgrade
            int accuracyCost = 100 * localPlayerProfile.accuracyLevel * localPlayerProfile.accuracyLevel;
            int accuracyCards = 10 + localPlayerProfile.accuracyLevel * 5;
            
            upgradeAccuracyButton.GetComponentInChildren<TextMeshProUGUI>().text = 
                $"Upgrade\n{accuracyCost} Coins\n{accuracyCards} Cards";
            
            upgradeAccuracyButton.interactable = 
                localPlayerProfile.coins >= accuracyCost && 
                localPlayerProfile.cards >= accuracyCards;
        }
        
        #endregion
        
        #region Messages and Effects
        
        public void ShowMessage(string message, float duration = 0f)
        {
            messagePopup.SetActive(true);
            messageText.text = message;
            
            if (duration > 0)
            {
                StartCoroutine(HideMessageAfterDelay(duration));
            }
        }
        
        private IEnumerator HideMessageAfterDelay(float delay)
        {
            yield return new WaitForSeconds(delay);
            HideMessage();
        }
        
        public void HideMessage()
        {
            messagePopup.SetActive(false);
        }
        
        public void ShowHoleInOne()
        {
            holeInOneEffect.SetActive(true);
            ShowMessage("HOLE IN ONE!", 3f);
            AudioManager.Instance.PlaySound("HoleInOne");
            
            // Disable after animation
            StartCoroutine(DisableEffectAfterDelay(holeInOneEffect, 3f));
        }
        
        public void ShowPlayerFinished(NetworkConnection conn, int shots, string scoreText)
        {
            if (playerUIData.ContainsKey(conn))
            {
                playerUIData[conn].listItem.ShowFinished(scoreText);
            }
        }
        
        private IEnumerator DisableEffectAfterDelay(GameObject effect, float delay)
        {
            yield return new WaitForSeconds(delay);
            effect.SetActive(false);
        }
        
        #endregion
        
        #region Utility
        
        public void SetPlayerProfile(PlayerProfile profile)
        {
            localPlayerProfile = profile;
            UpdateMainMenuUI();
        }
        
        private void OnCancelMatchmaking()
        {
            NetworkMatchmaker.Instance.CancelMatchmaking();
            ShowMainMenu();
        }
        
        private void OnContinueFromResults()
        {
            ShowMainMenu();
        }
        
        public void ResetMatchUI()
        {
            playerUIData.Clear();
            ResetGameplayUI();
            ShowMainMenu();
        }
        
        private void ShowEnergyRefillPopup()
        {
            // Implementation for energy refill popup
            // Options: Wait, Watch Ad, Use Gems
        }
        
        #endregion
    }
    
    #region Helper Classes
    
    [System.Serializable]
    public class PlayerUIData
    {
        public PlayerListItem listItem;
        public PlayerListData playerData;
    }
    
    #endregion
}