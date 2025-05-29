using UnityEngine;

namespace MicrogolfMasters
{
    [CreateAssetMenu(fileName = "BallData", menuName = "MicrogolfMasters/Ball Data")]
    public class BallData : ScriptableObject
    {
        [Header("Basic Info")]
        public int id;
        public string ballName = "Golf Ball";
        public string description = "A standard golf ball";
        public BallRarity rarity = BallRarity.Common;
        public BallCategory category = BallCategory.Standard;
        
        [Header("Visuals")]
        public Sprite sprite;
        public Sprite shopIcon;
        public Color primaryColor = Color.white;
        public Color secondaryColor = Color.gray;
        public Gradient trailGradient;
        public GameObject visualEffectPrefab;
        
        [Header("Stats")]
        [Range(0, 100)] public int strength = 50;
        [Range(0, 100)] public int accuracy = 50;
        [Range(0, 100)] public int spin = 50;
        [Range(0, 100)] public int bounce = 50;
        
        [Header("Special Abilities")]
        public bool hasSpecialAbility = false;
        public BallAbility specialAbility = BallAbility.None;
        public float abilityPower = 1f;
        public string abilityDescription = "";
        
        [Header("Purchase Info")]
        public bool isPurchasable = true;
        public CurrencyType purchaseType = CurrencyType.Coins;
        public int price = 100;
        public int unlockLevel = 1;
        
        [Header("Durability")]
        public int maxDurability = 100;
        public float durabilityLossRate = 10f; // Per match
        
        [Header("Country/Theme")]
        public bool isCountryBall = false;
        public string countryCode = "";
        public bool isSeasonalBall = false;
        public string seasonalEvent = "";
        
        public float GetTotalStats()
        {
            return (strength + accuracy + spin + bounce) / 4f;
        }
        
        public Color GetRarityColor()
        {
            switch (rarity)
            {
                case BallRarity.Common:
                    return Color.gray;
                case BallRarity.Premium:
                    return Color.green;
                case BallRarity.Rare:
                    return new Color(0.3f, 0.5f, 1f); // Blue
                case BallRarity.Legendary:
                    return new Color(1f, 0.5f, 0f); // Orange
                case BallRarity.Extreme:
                    return new Color(1f, 0f, 0.5f); // Pink
                default:
                    return Color.white;
            }
        }
        
        public string GetRarityName()
        {
            return rarity.ToString();
        }
        
        public int GetRepairTime()
        {
            // Base repair time in hours based on rarity
            switch (rarity)
            {
                case BallRarity.Common:
                    return 3;
                case BallRarity.Premium:
                    return 4;
                case BallRarity.Rare:
                    return 5;
                case BallRarity.Legendary:
                    return 6;
                case BallRarity.Extreme:
                    return 7;
                default:
                    return 3;
            }
        }
        
        public int GetInstantRepairCost()
        {
            // Gem cost for instant repair
            switch (rarity)
            {
                case BallRarity.Common:
                    return 30;
                case BallRarity.Premium:
                    return 40;
                case BallRarity.Rare:
                    return 50;
                case BallRarity.Legendary:
                    return 60;
                case BallRarity.Extreme:
                    return 75;
                default:
                    return 30;
            }
        }
        
        public void ApplyAbility(GolfBallController ball)
        {
            if (!hasSpecialAbility) return;
            
            switch (specialAbility)
            {
                case BallAbility.PowerBoost:
                    // Increase shot power
                    ball.GetComponent<Rigidbody2D>().mass *= (1f - abilityPower * 0.2f);
                    break;
                    
                case BallAbility.PerfectAccuracy:
                    // Reduce accuracy randomness
                    // Handled in GolfBallController shot calculation
                    break;
                    
                case BallAbility.SuperBounce:
                    // Increase bounce efficiency
                    PhysicsMaterial2D mat = new PhysicsMaterial2D("SuperBounce");
                    mat.bounciness = 0.9f + abilityPower * 0.1f;
                    ball.GetComponent<Collider2D>().sharedMaterial = mat;
                    break;
                    
                case BallAbility.IceResistance:
                    // Reduce ice surface effect
                    // Handled in surface detection
                    break;
                    
                case BallAbility.WindResistance:
                    // Reduce wind effect
                    ball.GetComponent<Rigidbody2D>().drag *= (1f + abilityPower * 0.3f);
                    break;
                    
                case BallAbility.LuckyBounce:
                    // Chance for favorable bounces
                    // Handled in collision detection
                    break;
                    
                case BallAbility.SpeedControl:
                    // Better speed control on all surfaces
                    // Handled in surface effects
                    break;
            }
        }
    }
    
    public enum BallRarity
    {
        Common,
        Premium,
        Rare,
        Legendary,
        Extreme
    }
    
    public enum BallCategory
    {
        Standard,
        Country,
        Seasonal,
        Special,
        Tournament
    }
    
    public enum BallAbility
    {
        None,
        PowerBoost,
        PerfectAccuracy,
        SuperBounce,
        IceResistance,
        WindResistance,
        LuckyBounce,
        SpeedControl
    }
    
    public enum CurrencyType
    {
        Coins,
        Gems,
        Cards,
        Special
    }
}