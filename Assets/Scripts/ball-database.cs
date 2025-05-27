using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace MicrogolfMasters
{
    [CreateAssetMenu(fileName = "BallDatabase", menuName = "MicrogolfMasters/Ball Database")]
    public class BallDatabase : ScriptableObject
    {
        private static BallDatabase _instance;
        public static BallDatabase Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = Resources.Load<BallDatabase>("BallDatabase");
                }
                return _instance;
            }
        }
        
        [Header("Ball Collection")]
        [SerializeField] private List<BallData> allBalls = new List<BallData>();
        
        [Header("Categories")]
        [SerializeField] private List<BallData> standardBalls = new List<BallData>();
        [SerializeField] private List<BallData> countryBalls = new List<BallData>();
        [SerializeField] private List<BallData> seasonalBalls = new List<BallData>();
        [SerializeField] private List<BallData> specialBalls = new List<BallData>();
        
        private void OnEnable()
        {
            RefreshCategories();
        }
        
        private void RefreshCategories()
        {
            standardBalls.Clear();
            countryBalls.Clear();
            seasonalBalls.Clear();
            specialBalls.Clear();
            
            foreach (var ball in allBalls)
            {
                switch (ball.category)
                {
                    case BallCategory.Standard:
                        standardBalls.Add(ball);
                        break;
                    case BallCategory.Country:
                        countryBalls.Add(ball);
                        break;
                    case BallCategory.Seasonal:
                        seasonalBalls.Add(ball);
                        break;
                    case BallCategory.Special:
                    case BallCategory.Tournament:
                        specialBalls.Add(ball);
                        break;
                }
            }
        }
        
        public BallData GetBall(int id)
        {
            return allBalls.FirstOrDefault(b => b.id == id);
        }
        
        public BallData GetBallByName(string name)
        {
            return allBalls.FirstOrDefault(b => b.ballName == name);
        }
        
        public List<BallData> GetAllBalls()
        {
            return new List<BallData>(allBalls);
        }
        
        public List<BallData> GetBallsByCategory(BallCategory category)
        {
            switch (category)
            {
                case BallCategory.Standard:
                    return new List<BallData>(standardBalls);
                case BallCategory.Country:
                    return new List<BallData>(countryBalls);
                case BallCategory.Seasonal:
                    return new List<BallData>(seasonalBalls);
                case BallCategory.Special:
                case BallCategory.Tournament:
                    return new List<BallData>(specialBalls);
                default:
                    return new List<BallData>();
            }
        }
        
        public List<BallData> GetBallsByRarity(BallRarity rarity)
        {
            return allBalls.Where(b => b.rarity == rarity).ToList();
        }
        
        public List<BallData> GetPurchasableBalls(int playerLevel, int playerCoins, int playerGems)
        {
            return allBalls.Where(b => 
                b.isPurchasable && 
                b.unlockLevel <= playerLevel &&
                ((b.purchaseType == CurrencyType.Coins && b.price <= playerCoins) ||
                 (b.purchaseType == CurrencyType.Gems && b.price <= playerGems))
            ).ToList();
        }
        
        public List<BallData> GetCountryBalls()
        {
            return allBalls.Where(b => b.isCountryBall).ToList();
        }
        
        public List<BallData> GetSeasonalBalls(string season = "")
        {
            if (string.IsNullOrEmpty(season))
            {
                return allBalls.Where(b => b.isSeasonalBall).ToList();
            }
            else
            {
                return allBalls.Where(b => b.isSeasonalBall && b.seasonalEvent == season).ToList();
            }
        }
        
        public List<BallData> GetBallsWithAbility(BallAbility ability)
        {
            return allBalls.Where(b => b.hasSpecialAbility && b.specialAbility == ability).ToList();
        }
        
        public BallData GetRandomBallByRarity(BallRarity rarity)
        {
            List<BallData> rarityBalls = GetBallsByRarity(rarity);
            if (rarityBalls.Count > 0)
            {
                return rarityBalls[Random.Range(0, rarityBalls.Count)];
            }
            return null;
        }
        
        public BallData GetRandomBallFromLootBox(LootBoxType boxType)
        {
            // Define rarity chances for each loot box type
            float[] rarityChances = GetLootBoxRarityChances(boxType);
            
            // Roll for rarity
            float roll = Random.value;
            BallRarity selectedRarity = BallRarity.Common;
            
            float cumulative = 0f;
            for (int i = 0; i < rarityChances.Length; i++)
            {
                cumulative += rarityChances[i];
                if (roll <= cumulative)
                {
                    selectedRarity = (BallRarity)i;
                    break;
                }
            }
            
            // Get random ball of selected rarity
            return GetRandomBallByRarity(selectedRarity);
        }
        
        private float[] GetLootBoxRarityChances(LootBoxType boxType)
        {
            switch (boxType)
            {
                case LootBoxType.Basic:
                    return new float[] { 0.7f, 0.25f, 0.05f, 0f, 0f }; // 70% Common, 25% Premium, 5% Rare
                    
                case LootBoxType.Premium:
                    return new float[] { 0.3f, 0.5f, 0.18f, 0.02f, 0f }; // 30% Common, 50% Premium, 18% Rare, 2% Legendary
                    
                case LootBoxType.Legendary:
                    return new float[] { 0f, 0.3f, 0.5f, 0.18f, 0.02f }; // 30% Premium, 50% Rare, 18% Legendary, 2% Extreme
                    
                default:
                    return new float[] { 1f, 0f, 0f, 0f, 0f };
            }
        }
        
        public void InitializeDefaultBalls()
        {
            // This method would be called in editor to create default balls
            // Example balls that would be created:
            
            // Basic Ball (ID: 0)
            // Common rarity, balanced stats, free starter ball
            
            // Power Ball (ID: 1)
            // Premium rarity, high strength, lower accuracy
            
            // Precision Ball (ID: 2)
            // Premium rarity, high accuracy, lower strength
            
            // Country Balls (IDs: 10-89)
            // Various country-themed balls with unique designs
            
            // Seasonal Balls (IDs: 90-99)
            // Halloween, Christmas, Easter, Summer themed
            
            // Special Event Balls (IDs: 100+)
            // Tournament rewards, limited edition balls
        }
        
        #if UNITY_EDITOR
        [ContextMenu("Validate Ball IDs")]
        private void ValidateBallIDs()
        {
            // Ensure all balls have unique IDs
            HashSet<int> usedIds = new HashSet<int>();
            
            foreach (var ball in allBalls)
            {
                if (usedIds.Contains(ball.id))
                {
                    Debug.LogError($"Duplicate ball ID found: {ball.id} on {ball.ballName}");
                }
                usedIds.Add(ball.id);
            }
            
            Debug.Log($"Validated {allBalls.Count} balls. {usedIds.Count} unique IDs found.");
        }
        
        [ContextMenu("Sort Balls By ID")]
        private void SortBallsByID()
        {
            allBalls = allBalls.OrderBy(b => b.id).ToList();
            RefreshCategories();
            UnityEditor.EditorUtility.SetDirty(this);
        }
        #endif
    }
    
    public enum LootBoxType
    {
        Basic,
        Premium,
        Legendary,
        Event
    }
}