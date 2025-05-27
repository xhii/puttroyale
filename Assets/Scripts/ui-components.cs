using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using Mirror;

namespace MicrogolfMasters
{
    // Player List Item for in-game scoreboard
    public class PlayerListItem : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private TextMeshProUGUI playerNameText;
        [SerializeField] private TextMeshProUGUI scoreText;
        [SerializeField] private Image playerIcon;
        [SerializeField] private GameObject finishedIndicator;
        [SerializeField] private GameObject currentTurnIndicator;
        [SerializeField] private Image backgroundImage;
        
        [Header("Colors")]
        [SerializeField] private Color localPlayerColor = new Color(0.2f, 0.8f, 0.2f, 0.3f);
        [SerializeField] private Color defaultColor = new Color(1f, 1f, 1f, 0.1f);
        [SerializeField] private Color finishedColor = new Color(0.5f, 0.5f, 0.5f, 0.3f);
        
        private PlayerListData playerData;
        private bool isLocalPlayer = false;
        
        public void Setup(PlayerListData data, bool isLocal = false)
        {
            playerData = data;
            isLocalPlayer = isLocal;
            
            playerNameText.text = data.playerName;
            scoreText.text = data.currentScore.ToString();
            
            finishedIndicator.SetActive(false);
            currentTurnIndicator.SetActive(false);
            
            // Set background color
            backgroundImage.color = isLocal ? localPlayerColor : defaultColor;
            
            // Animate entry
            transform.localScale = Vector3.zero;
            LeanTween.scale(gameObject, Vector3.one, 0.3f).setEase(LeanTweenType.easeOutBack);
        }
        
        public void UpdateScore(int newScore)
        {
            scoreText.text = newScore.ToString();
            
            // Pulse animation
            LeanTween.scale(scoreText.gameObject, Vector3.one * 1.2f, 0.2f)
                .setEase(LeanTweenType.easeOutQuad)
                .setOnComplete(() => {
                    LeanTween.scale(scoreText.gameObject, Vector3.one, 0.2f);
                });
        }
        
        public void ShowFinished(string resultText)
        {
            finishedIndicator.SetActive(true);
            finishedIndicator.GetComponentInChildren<TextMeshProUGUI>().text = resultText;
            backgroundImage.color = finishedColor;
            
            // Celebration animation for good scores
            if (resultText.Contains("Eagle") || resultText.Contains("Birdie"))
            {
                ParticleManager.Instance.PlayButtonClick(transform.position);
            }
        }
        
        public void SetCurrentTurn(bool isCurrent)
        {
            currentTurnIndicator.SetActive(isCurrent);
            
            if (isCurrent)
            {
                // Pulse effect
                LeanTween.alpha(currentTurnIndicator, 0.5f, 0.5f)
                    .setLoopPingPong();
            }
        }
    }
    
    // Ball Shop Item
    public class BallShopItem : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private Image ballIcon;
        [SerializeField] private TextMeshProUGUI ballNameText;
        [SerializeField] private TextMeshProUGUI priceText;
        [SerializeField] private Image currencyIcon;
        [SerializeField] private GameObject lockedOverlay;
        [SerializeField] private TextMeshProUGUI unlockLevelText;
        [SerializeField] private Image rarityFrame;
        [SerializeField] private GameObject ownedIndicator;
        [SerializeField] private Button selectButton;
        
        [Header("Currency Icons")]
        [SerializeField] private Sprite coinsIcon;
        [SerializeField] private Sprite gemsIcon;
        
        private BallData ballData;
        private Action<BallData> onSelectCallback;
        
        public void Setup(BallData data, Action<BallData> onSelect, bool isOwned = false)
        {
            ballData = data;
            onSelectCallback = onSelect;
            
            // Set basic info
            ballIcon.sprite = data.shopIcon ?? data.sprite;
            ballNameText.text = data.ballName;
            
            // Set rarity frame color
            rarityFrame.color = data.GetRarityColor();
            
            // Set price
            if (data.purchaseType == CurrencyType.Coins)
            {
                priceText.text = data.price.ToString();
                currencyIcon.sprite = coinsIcon;
            }
            else
            {
                priceText.text = data.price.ToString();
                currencyIcon.sprite = gemsIcon;
            }
            
            // Check if locked
            PlayerProfile profile = PlayerDataManager.Instance.GetPlayerProfile();
            bool isLocked = profile.level < data.unlockLevel;
            
            lockedOverlay.SetActive(isLocked);
            if (isLocked)
            {
                unlockLevelText.text = $"Unlock at\nLevel {data.unlockLevel}";
            }
            
            // Show owned indicator
            ownedIndicator.SetActive(isOwned);
            
            // Setup button
            selectButton.onClick.RemoveAllListeners();
            selectButton.onClick.AddListener(() => onSelectCallback?.Invoke(ballData));
            selectButton.interactable = !isLocked && !isOwned;
        }
        
        public void ShowPurchaseEffect()
        {
            // Celebration effect
            ParticleManager.Instance.PlayPurchaseEffect(transform.position);
            
            // Scale animation
            LeanTween.scale(gameObject, Vector3.one * 1.1f, 0.2f)
                .setEase(LeanTweenType.easeOutQuad)
                .setOnComplete(() => {
                    LeanTween.scale(gameObject, Vector3.one, 0.2f);
                });
        }
    }
    
    // Ball Inventory Item
    public class BallInventoryItem : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private Image ballIcon;
        [SerializeField] private TextMeshProUGUI ballNameText;
        [SerializeField] private Slider durabilityBar;
        [SerializeField] private TextMeshProUGUI durabilityText;
        [SerializeField] private GameObject repairIndicator;
        [SerializeField] private TextMeshProUGUI repairTimeText;
        [SerializeField] private GameObject selectedIndicator;
        [SerializeField] private Image rarityBorder;
        [SerializeField] private Button selectButton;
        
        [Header("Stats Display")]
        [SerializeField] private TextMeshProUGUI strengthText;
        [SerializeField] private TextMeshProUGUI accuracyText;
        
        private OwnedBall ownedBall;
        private Action onSelectCallback;
        
        public void Setup(OwnedBall ball, Action onSelect)
        {
            ownedBall = ball;
            onSelectCallback = onSelect;
            
            // Basic info
            ballIcon.sprite = ball.ballData.sprite;
            ballNameText.text = ball.ballData.ballName;
            rarityBorder.color = ball.ballData.GetRarityColor();
            
            // Stats
            strengthText.text = ball.ballData.strength.ToString();
            accuracyText.text = ball.ballData.accuracy.ToString();
            
            // Durability
            float durabilityPercent = (float)ball.durability / ball.maxDurability;
            durabilityBar.value = durabilityPercent;
            durabilityText.text = $"{ball.durability}/{ball.maxDurability}";
            
            // Color durability bar
            if (durabilityPercent > 0.5f)
                durabilityBar.fillRect.GetComponent<Image>().color = Color.green;
            else if (durabilityPercent > 0.2f)
                durabilityBar.fillRect.GetComponent<Image>().color = Color.yellow;
            else
                durabilityBar.fillRect.GetComponent<Image>().color = Color.red;
            
            // Repair status
            UpdateRepairStatus();
            
            // Button
            selectButton.onClick.RemoveAllListeners();
            selectButton.onClick.AddListener(() => onSelectCallback?.Invoke());
            selectButton.interactable = !ball.isRepairing && ball.durability > 0;
            
            // Start repair timer if needed
            if (ball.isRepairing)
            {
                InvokeRepeating(nameof(UpdateRepairStatus), 0f, 1f);
            }
        }
        
        private void UpdateRepairStatus()
        {
            if (ownedBall.isRepairing)
            {
                repairIndicator.SetActive(true);
                TimeSpan timeRemaining = ownedBall.repairEndTime - DateTime.Now;
                
                if (timeRemaining.TotalSeconds > 0)
                {
                    repairTimeText.text = $"{timeRemaining.Hours:00}:{timeRemaining.Minutes:00}:{timeRemaining.Seconds:00}";
                }
                else
                {
                    // Repair complete
                    ownedBall.isRepairing = false;
                    ownedBall.durability = ownedBall.maxDurability;
                    repairIndicator.SetActive(false);
                    CancelInvoke(nameof(UpdateRepairStatus));
                    
                    // Refresh display
                    Setup(ownedBall, onSelectCallback);
                }
            }
            else
            {
                repairIndicator.SetActive(false);
            }
        }
        
        public void SetSelected(bool selected)
        {
            selectedIndicator.SetActive(selected);
        }
        
        private void OnDestroy()
        {
            CancelInvoke(nameof(UpdateRepairStatus));
        }
    }
    
    // Result List Item for match results
    public class ResultListItem : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private TextMeshProUGUI positionText;
        [SerializeField] private TextMeshProUGUI playerNameText;
        [SerializeField] private TextMeshProUGUI scoreText;
        [SerializeField] private Image backgroundImage;
        [SerializeField] private GameObject crownIcon;
        
        [Header("Position Colors")]
        [SerializeField] private Color firstPlaceColor = Color.yellow;
        [SerializeField] private Color secondPlaceColor = Color.gray;
        [SerializeField] private Color thirdPlaceColor = new Color(0.8f, 0.5f, 0.2f);
        [SerializeField] private Color defaultColor = Color.white;
        
        public void Setup(int position, string playerName, int score)
        {
            positionText.text = GetPositionText(position);
            playerNameText.text = playerName;
            scoreText.text = score.ToString();
            
            // Show crown for winner
            crownIcon.SetActive(position == 1);
            
            // Set background color based on position
            switch (position)
            {
                case 1:
                    backgroundImage.color = firstPlaceColor * 0.3f;
                    break;
                case 2:
                    backgroundImage.color = secondPlaceColor * 0.3f;
                    break;
                case 3:
                    backgroundImage.color = thirdPlaceColor * 0.3f;
                    break;
                default:
                    backgroundImage.color = defaultColor * 0.1f;
                    break;
            }
            
            // Animate entry
            transform.localScale = new Vector3(0f, 1f, 1f);
            LeanTween.scaleX(gameObject, 1f, 0.3f)
                .setDelay(position * 0.1f)
                .setEase(LeanTweenType.easeOutBack);
        }
        
        private string GetPositionText(int position)
        {
            switch (position)
            {
                case 1: return "1st";
                case 2: return "2nd";
                case 3: return "3rd";
                default: return $"{position}th";
            }
        }
    }
    
    // Tab Group for shop categories
    public class TabGroup : MonoBehaviour
    {
        [Header("Tab Configuration")]
        [SerializeField] private List<TabButton> tabButtons;
        [SerializeField] private List<GameObject> tabPanels;
        [SerializeField] private Color activeTabColor = Color.white;
        [SerializeField] private Color inactiveTabColor = Color.gray;
        
        private TabButton selectedTab;
        
        private void Start()
        {
            // Initialize tabs
            for (int i = 0; i < tabButtons.Count; i++)
            {
                int index = i; // Capture for closure
                tabButtons[i].Initialize(this, index);
            }
            
            // Select first tab by default
            if (tabButtons.Count > 0)
            {
                OnTabSelected(tabButtons[0]);
            }
        }
        
        public void OnTabSelected(TabButton tab)
        {
            if (selectedTab != null)
            {
                selectedTab.Deselect();
            }
            
            selectedTab = tab;
            selectedTab.Select();
            
            int tabIndex = tabButtons.IndexOf(tab);
            
            // Show corresponding panel
            for (int i = 0; i < tabPanels.Count; i++)
            {
                tabPanels[i].SetActive(i == tabIndex);
            }
        }
        
        public void OnTabEnter(TabButton tab)
        {
            if (selectedTab != tab)
            {
                tab.SetColor(Color.Lerp(inactiveTabColor, activeTabColor, 0.5f));
            }
        }
        
        public void OnTabExit(TabButton tab)
        {
            if (selectedTab != tab)
            {
                tab.SetColor(inactiveTabColor);
            }
        }
        
        public Color GetActiveColor() => activeTabColor;
        public Color GetInactiveColor() => inactiveTabColor;
    }
    
    // Tab Button
    public class TabButton : MonoBehaviour
    {
        [Header("Components")]
        [SerializeField] private Image backgroundImage;
        [SerializeField] private TextMeshProUGUI tabText;
        
        private TabGroup tabGroup;
        private int tabIndex;
        private Button button;
        
        public void Initialize(TabGroup group, int index)
        {
            tabGroup = group;
            tabIndex = index;
            
            button = GetComponent<Button>();
            button.onClick.AddListener(() => tabGroup.OnTabSelected(this));
            
            // Set initial state
            Deselect();
        }
        
        public void Select()
        {
            SetColor(tabGroup.GetActiveColor());
            transform.localScale = Vector3.one * 1.1f;
        }
        
        public void Deselect()
        {
            SetColor(tabGroup.GetInactiveColor());
            transform.localScale = Vector3.one;
        }
        
        public void SetColor(Color color)
        {
            if (backgroundImage != null)
                backgroundImage.color = color;
            
            if (tabText != null)
                tabText.color = color;
        }
        
        private void OnPointerEnter()
        {
            tabGroup.OnTabEnter(this);
        }
        
        private void OnPointerExit()
        {
            tabGroup.OnTabExit(this);
        }
    }
}