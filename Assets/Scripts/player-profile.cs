using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System;

namespace MicrogolfMasters
{
    [System.Serializable]
    public class PlayerProfile
    {
        [Header("Basic Info")]
        public string playerName = "Player";
        public string playerId;
        public int level = 1;
        public int experience = 0;
        public int league = 18; // Start at League 18
        public int crowns = 0;
        
        [Header("Currency")]
        public int coins = 100;
        public int gems = 10;
        public int cards = 0;
        
        [Header("Statistics")]
        public int matchesPlayed = 0;
        public int wins = 0;
        public int holesInOne = 0;
        public int totalShots = 0;
        public float averageShots = 0f;
        public int bestScore = 999;
        
        [Header("Ball Collection")]
        public List<OwnedBall> ownedBalls = new List<OwnedBall>();
        public BallData selectedBall;
        public int selectedBallIndex = 0;
        
        [Header("Upgrades")]
        public int baseStrength = 50;
        public int baseAccuracy = 50;
        public int strengthLevel = 1;
        public int accuracyLevel = 1;
        
        [Header("Energy")]
        public int currentEnergy = 5;
        public int maxEnergy = 5;
        public DateTime lastEnergyRefill = DateTime.Now;
        
        [Header("Daily Rewards")]
        public DateTime lastDailyReward = DateTime.MinValue;
        public int dailyRewardStreak = 0;
        
        [Header("Settings")]
        public float soundVolume = 1f;
        public float musicVolume = 1f;
        public bool notifications = true;
        public bool vibration = true;
        
        public PlayerProfile()
        {
            playerId = Guid.NewGuid().ToString();
            InitializeStarterBalls();
        }
        
        private void InitializeStarterBalls()
        {
            // Give player the basic ball
            OwnedBall starterBall = new OwnedBall
            {
                ballData = BallDatabase.Instance.GetBall(0), // Basic ball
                durability = 100,
                maxDurability = 100,
                repairEndTime = DateTime.MinValue,
                isRepairing = false
            };
            
            ownedBalls.Add(starterBall);
            selectedBall = starterBall.ballData;
        }
        
        #region Experience and Leveling
        
        public void AddExperience(int amount)
        {
            experience += amount;
            
            while (experience >= GetExperienceForNextLevel())
            {
                LevelUp();
            }
        }
        
        private void LevelUp()
        {
            level++;
            experience -= GetExperienceForLevel(level - 1);
            
            // Level up rewards
            coins += 100;
            gems += 5;
            
            // Unlock new content at certain levels
            CheckLevelUnlocks();
        }
        
        public int GetExperienceForNextLevel()
        {
            return GetExperienceForLevel(level);
        }
        
        private int GetExperienceForLevel(int lvl)
        {
            // Progressive exp requirement
            return 100 + (lvl * 50);
        }
        
        public float GetLevelProgress()
        {
            int currentLevelExp = GetExperienceForLevel(level - 1);
            int nextLevelExp = GetExperienceForLevel(level);
            int expInCurrentLevel = experience;
            
            return (float)expInCurrentLevel / (nextLevelExp - currentLevelExp);
        }
        
        private void CheckLevelUnlocks()
        {
            // Unlock specific balls at certain levels
            switch (level)
            {
                case 5:
                    UnlockBall(5); // Premium ball
                    break;
                case 10:
                    UnlockBall(10); // Rare ball
                    break;
                case 20:
                    UnlockBall(20); // Legendary ball
                    break;
            }
        }
        
        #endregion
        
        #region League System
        
        public void UpdateLeague(int newCrowns)
        {
            crowns = newCrowns;
            
            // Check for promotion
            if (crowns >= GetCrownsForPromotion())
            {
                PromoteLeague();
            }
            // Check for demotion
            else if (crowns <= 0 && league < 18)
            {
                DemoteLeague();
            }
        }
        
        private void PromoteLeague()
        {
            if (league > 1)
            {
                league--;
                crowns = GetStartingCrownsForLeague(league);
                
                // Promotion rewards
                coins += 200;
                cards += 10;
            }
            else if (league == 1)
            {
                // Promote to Masters
                league = 0; // 0 represents Masters
                crowns = 1000; // Starting ELO for Masters
            }
        }
        
        private void DemoteLeague()
        {
            if (league == 0)
            {
                // Demote from Masters
                league = 1;
                crowns = GetStartingCrownsForLeague(1);
            }
            else if (league < 18)
            {
                league++;
                crowns = GetCrownsForPromotion() / 2;
            }
        }
        
        private int GetCrownsForPromotion()
        {
            // Higher leagues require more crowns
            return 100 + (18 - league) * 20;
        }
        
        private int GetStartingCrownsForLeague(int leagueNum)
        {
            return GetCrownsForPromotion() / 3;
        }
        
        public string GetLeagueName()
        {
            if (league == 0) return "Masters";
            return $"League {league}";
        }
        
        #endregion
        
        #region Ball Management
        
        public bool UnlockBall(int ballId)
        {
            BallData ballToUnlock = BallDatabase.Instance.GetBall(ballId);
            if (ballToUnlock == null) return false;
            
            // Check if already owned
            if (ownedBalls.Any(b => b.ballData.id == ballId)) return false;
            
            OwnedBall newBall = new OwnedBall
            {
                ballData = ballToUnlock,
                durability = ballToUnlock.maxDurability,
                maxDurability = ballToUnlock.maxDurability,
                repairEndTime = DateTime.MinValue,
                isRepairing = false
            };
            
            ownedBalls.Add(newBall);
            return true;
        }
        
        public bool PurchaseBall(int ballId)
        {
            BallData ballToPurchase = BallDatabase.Instance.GetBall(ballId);
            if (ballToPurchase == null) return false;
            
            // Check if can afford
            bool canAfford = false;
            if (ballToPurchase.purchaseType == CurrencyType.Coins && coins >= ballToPurchase.price)
            {
                coins -= ballToPurchase.price;
                canAfford = true;
            }
            else if (ballToPurchase.purchaseType == CurrencyType.Gems && gems >= ballToPurchase.price)
            {
                gems -= ballToPurchase.price;
                canAfford = true;
            }
            
            if (canAfford)
            {
                return UnlockBall(ballId);
            }
            
            return false;
        }
        
        public void SelectBall(int index)
        {
            if (index >= 0 && index < ownedBalls.Count)
            {
                OwnedBall ball = ownedBalls[index];
                if (!ball.isRepairing && ball.durability > 0)
                {
                    selectedBallIndex = index;
                    selectedBall = ball.ballData;
                    
                    // Apply upgrades to ball stats
                    selectedBall.strength = ball.ballData.strength + (strengthLevel - 1) * 5;
                    selectedBall.accuracy = ball.ballData.accuracy + (accuracyLevel - 1) * 5;
                }
            }
        }
        
        public void UseBall()
        {
            if (selectedBallIndex >= 0 && selectedBallIndex < ownedBalls.Count)
            {
                OwnedBall ball = ownedBalls[selectedBallIndex];
                ball.durability = Mathf.Max(0, ball.durability - 10); // Lose 10 durability per match
                
                if (ball.durability <= 0)
                {
                    StartBallRepair(selectedBallIndex);
                    
                    // Switch to basic ball
                    SelectBall(0);
                }
            }
        }
        
        public void StartBallRepair(int ballIndex)
        {
            if (ballIndex >= 0 && ballIndex < ownedBalls.Count)
            {
                OwnedBall ball = ownedBalls[ballIndex];
                ball.isRepairing = true;
                
                // Repair time based on rarity
                float repairHours = 3f + (int)ball.ballData.rarity * 1f;
                ball.repairEndTime = DateTime.Now.AddHours(repairHours);
            }
        }
        
        public bool InstantRepairBall(int ballIndex)
        {
            if (ballIndex >= 0 && ballIndex < ownedBalls.Count)
            {
                OwnedBall ball = ownedBalls[ballIndex];
                int gemCost = 30 + (int)ball.ballData.rarity * 10;
                
                if (gems >= gemCost)
                {
                    gems -= gemCost;
                    ball.durability = ball.maxDurability;
                    ball.isRepairing = false;
                    ball.repairEndTime = DateTime.MinValue;
                    return true;
                }
            }
            return false;
        }
        
        public void CheckBallRepairs()
        {
            DateTime now = DateTime.Now;
            
            foreach (var ball in ownedBalls)
            {
                if (ball.isRepairing && now >= ball.repairEndTime)
                {
                    ball.durability = ball.maxDurability;
                    ball.isRepairing = false;
                    ball.repairEndTime = DateTime.MinValue;
                }
            }
        }
        
        #endregion
        
        #region Upgrades
        
        public bool UpgradeStrength()
        {
            int cost = GetUpgradeCost(strengthLevel);
            int cardsRequired = GetUpgradeCards(strengthLevel);
            
            if (coins >= cost && cards >= cardsRequired)
            {
                coins -= cost;
                cards -= cardsRequired;
                strengthLevel++;
                baseStrength += 5;
                return true;
            }
            return false;
        }
        
        public bool UpgradeAccuracy()
        {
            int cost = GetUpgradeCost(accuracyLevel);
            int cardsRequired = GetUpgradeCards(accuracyLevel);
            
            if (coins >= cost && cards >= cardsRequired)
            {
                coins -= cost;
                cards -= cardsRequired;
                accuracyLevel++;
                baseAccuracy += 5;
                return true;
            }
            return false;
        }
        
        private int GetUpgradeCost(int currentLevel)
        {
            return 100 * currentLevel * currentLevel;
        }
        
        private int GetUpgradeCards(int currentLevel)
        {
            return 10 + currentLevel * 5;
        }
        
        #endregion
        
        #region Energy System
        
        public bool UseEnergy(int amount)
        {
            RefillEnergy(); // Check for time-based refill first
            
            if (currentEnergy >= amount)
            {
                currentEnergy -= amount;
                return true;
            }
            return false;
        }
        
        public void RefillEnergy()
        {
            DateTime now = DateTime.Now;
            TimeSpan timeSinceLastRefill = now - lastEnergyRefill;
            
            // Refill 1 energy every 20 minutes
            int energyToAdd = (int)(timeSinceLastRefill.TotalMinutes / 20);
            
            if (energyToAdd > 0)
            {
                currentEnergy = Mathf.Min(currentEnergy + energyToAdd, maxEnergy);
                lastEnergyRefill = now;
            }
        }
        
        public bool RefillEnergyWithGems()
        {
            int gemCost = 20;
            if (gems >= gemCost)
            {
                gems -= gemCost;
                currentEnergy = maxEnergy;
                lastEnergyRefill = DateTime.Now;
                return true;
            }
            return false;
        }
        
        public TimeSpan GetTimeUntilNextEnergy()
        {
            if (currentEnergy >= maxEnergy) return TimeSpan.Zero;
            
            DateTime nextRefill = lastEnergyRefill.AddMinutes(20);
            TimeSpan timeRemaining = nextRefill - DateTime.Now;
            
            return timeRemaining.TotalSeconds > 0 ? timeRemaining : TimeSpan.Zero;
        }
        
        #endregion
        
        #region Daily Rewards
        
        public bool CanClaimDailyReward()
        {
            return DateTime.Now.Date > lastDailyReward.Date;
        }
        
        public DailyReward ClaimDailyReward()
        {
            if (!CanClaimDailyReward()) return null;
            
            // Check if streak continues
            if ((DateTime.Now.Date - lastDailyReward.Date).Days == 1)
            {
                dailyRewardStreak++;
            }
            else
            {
                dailyRewardStreak = 1;
            }
            
            lastDailyReward = DateTime.Now;
            
            // Calculate rewards based on streak
            DailyReward reward = new DailyReward
            {
                day = dailyRewardStreak,
                coins = 50 + (dailyRewardStreak * 10),
                gems = dailyRewardStreak % 7 == 0 ? 10 : 0,
                cards = dailyRewardStreak * 2,
                energy = 2
            };
            
            // Apply rewards
            coins += reward.coins;
            gems += reward.gems;
            cards += reward.cards;
            currentEnergy = Mathf.Min(currentEnergy + reward.energy, maxEnergy);
            
            return reward;
        }
        
        #endregion
        
        #region Statistics
        
        public void UpdateStats(int shots, bool won)
        {
            matchesPlayed++;
            totalShots += shots;
            averageShots = (float)totalShots / matchesPlayed;
            
            if (won) wins++;
            if (shots < bestScore) bestScore = shots;
        }
        
        public float GetWinRate()
        {
            return matchesPlayed > 0 ? (float)wins / matchesPlayed : 0f;
        }
        
        #endregion
    }
    
    #region Supporting Classes
    
    [System.Serializable]
    public class OwnedBall
    {
        public BallData ballData;
        public int durability;
        public int maxDurability;
        public DateTime repairEndTime;
        public bool isRepairing;
    }
    
    [System.Serializable]
    public class DailyReward
    {
        public int day;
        public int coins;
        public int gems;
        public int cards;
        public int energy;
    }
    
    #endregion
}