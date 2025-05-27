using UnityEngine;
using System.Collections.Generic;
using System.Collections;

namespace MicrogolfMasters
{
    public class ParticleManager : MonoBehaviour
    {
        private static ParticleManager _instance;
        public static ParticleManager Instance => _instance;
        
        [Header("Particle Effects")]
        [SerializeField] private ParticleSystem ballHitEffect;
        [SerializeField] private ParticleSystem waterSplashEffect;
        [SerializeField] private ParticleSystem sandPuffEffect;
        [SerializeField] private ParticleSystem sparkEffect;
        [SerializeField] private ParticleSystem speedBoostEffect;
        [SerializeField] private ParticleSystem wallHitEffect;
        [SerializeField] private ParticleSystem holeSuccessEffect;
        [SerializeField] private ParticleSystem powerChargeEffect;
        
        [Header("Surface Trail Effects")]
        [SerializeField] private ParticleSystem grassTrailEffect;
        [SerializeField] private ParticleSystem sandTrailEffect;
        [SerializeField] private ParticleSystem iceTrailEffect;
        
        [Header("Celebration Effects")]
        [SerializeField] private ParticleSystem fireworksEffect;
        [SerializeField] private ParticleSystem confettiEffect;
        [SerializeField] private ParticleSystem starBurstEffect;
        [SerializeField] private ParticleSystem coinShowerEffect;
        
        [Header("UI Effects")]
        [SerializeField] private ParticleSystem buttonClickEffect;
        [SerializeField] private ParticleSystem levelUpEffect;
        [SerializeField] private ParticleSystem purchaseEffect;
        
        [Header("Pooling Settings")]
        [SerializeField] private int poolSizePerEffect = 5;
        
        // Effect pools
        private Dictionary<string, Queue<ParticleSystem>> effectPools = new Dictionary<string, Queue<ParticleSystem>>();
        private Dictionary<string, ParticleSystem> effectPrefabs = new Dictionary<string, ParticleSystem>();
        
        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            
            InitializeEffectPools();
        }
        
        private void InitializeEffectPools()
        {
            // Register all effects
            RegisterEffect("BallHit", ballHitEffect);
            RegisterEffect("WaterSplash", waterSplashEffect);
            RegisterEffect("SandPuff", sandPuffEffect);
            RegisterEffect("Spark", sparkEffect);
            RegisterEffect("SpeedBoost", speedBoostEffect);
            RegisterEffect("WallHit", wallHitEffect);
            RegisterEffect("HoleSuccess", holeSuccessEffect);
            RegisterEffect("PowerCharge", powerChargeEffect);
            RegisterEffect("GrassTrail", grassTrailEffect);
            RegisterEffect("SandTrail", sandTrailEffect);
            RegisterEffect("IceTrail", iceTrailEffect);
            RegisterEffect("Fireworks", fireworksEffect);
            RegisterEffect("Confetti", confettiEffect);
            RegisterEffect("StarBurst", starBurstEffect);
            RegisterEffect("CoinShower", coinShowerEffect);
            RegisterEffect("ButtonClick", buttonClickEffect);
            RegisterEffect("LevelUp", levelUpEffect);
            RegisterEffect("Purchase", purchaseEffect);
            
            // Create pools
            foreach (var kvp in effectPrefabs)
            {
                CreatePool(kvp.Key, kvp.Value);
            }
        }
        
        private void RegisterEffect(string name, ParticleSystem prefab)
        {
            if (prefab != null)
            {
                effectPrefabs[name] = prefab;
            }
        }
        
        private void CreatePool(string effectName, ParticleSystem prefab)
        {
            Queue<ParticleSystem> pool = new Queue<ParticleSystem>();
            
            for (int i = 0; i < poolSizePerEffect; i++)
            {
                GameObject obj = Instantiate(prefab.gameObject, transform);
                obj.name = $"{effectName}_Pooled_{i}";
                obj.SetActive(false);
                
                ParticleSystem ps = obj.GetComponent<ParticleSystem>();
                pool.Enqueue(ps);
            }
            
            effectPools[effectName] = pool;
        }
        
        #region Play Methods
        
        public void PlayBallHit(Vector3 position, float power)
        {
            ParticleSystem effect = GetPooledEffect("BallHit");
            if (effect != null)
            {
                effect.transform.position = position;
                
                // Scale based on power
                var main = effect.main;
                main.startSpeed = 5f + power * 10f;
                main.startSize = 0.5f + power * 0.5f;
                
                effect.gameObject.SetActive(true);
                effect.Play();
                
                StartCoroutine(ReturnToPool(effect, "BallHit"));
            }
        }
        
        public void PlayWaterSplash(Vector3 position)
        {
            PlayEffect("WaterSplash", position, Quaternion.identity);
            
            // Additional water ripple effect
            CreateWaterRipple(position);
        }
        
        public void PlaySandPuff(Vector3 position)
        {
            ParticleSystem effect = GetPooledEffect("SandPuff");
            if (effect != null)
            {
                effect.transform.position = position;
                effect.gameObject.SetActive(true);
                effect.Play();
                
                StartCoroutine(ReturnToPool(effect, "SandPuff"));
            }
        }
        
        public void PlaySpeedBoost(Vector3 position)
        {
            PlayEffect("SpeedBoost", position, Quaternion.identity);
            
            // Add screen flash effect
            CameraController.Instance.FlashColor(new Color(1f, 0.8f, 0f, 0.3f), 0.2f);
        }
        
        public void PlayWallHit(Vector3 position)
        {
            PlayEffect("WallHit", position, Quaternion.identity);
            PlayEffect("Spark", position, Quaternion.identity);
        }
        
        public void PlayHoleSuccess(Vector3 position)
        {
            PlayEffect("HoleSuccess", position, Quaternion.identity);
            PlayEffect("StarBurst", position, Quaternion.identity);
            
            // Delayed confetti
            StartCoroutine(DelayedEffect("Confetti", position, 0.5f));
        }
        
        #endregion
        
        #region Trail Effects
        
        public ParticleSystem CreateSurfaceTrail(SurfaceType surface, Transform parent)
        {
            string effectName = GetTrailEffectName(surface);
            if (string.IsNullOrEmpty(effectName)) return null;
            
            ParticleSystem trail = GetPooledEffect(effectName);
            if (trail != null)
            {
                trail.transform.SetParent(parent);
                trail.transform.localPosition = Vector3.zero;
                trail.gameObject.SetActive(true);
                trail.Play();
            }
            
            return trail;
        }
        
        public void StopSurfaceTrail(ParticleSystem trail)
        {
            if (trail != null)
            {
                trail.Stop();
                StartCoroutine(ReturnTrailToPool(trail));
            }
        }
        
        private string GetTrailEffectName(SurfaceType surface)
        {
            switch (surface)
            {
                case SurfaceType.Grass:
                    return "GrassTrail";
                case SurfaceType.Sand:
                    return "SandTrail";
                case SurfaceType.Ice:
                    return "IceTrail";
                default:
                    return null;
            }
        }
        
        private IEnumerator ReturnTrailToPool(ParticleSystem trail)
        {
            // Wait for particles to fade
            yield return new WaitForSeconds(2f);
            
            trail.transform.SetParent(transform);
            trail.gameObject.SetActive(false);
            
            // Return to appropriate pool
            string effectName = trail.name.Split('_')[0];
            if (effectPools.ContainsKey(effectName))
            {
                effectPools[effectName].Enqueue(trail);
            }
        }
        
        #endregion
        
        #region Celebration Effects
        
        public void PlayVictoryCelebration(Vector3 position)
        {
            // Multiple effects for big celebration
            PlayEffect("Fireworks", position + Vector3.up * 5f, Quaternion.identity);
            PlayEffect("Confetti", position + Vector3.up * 3f, Quaternion.identity);
            PlayEffect("CoinShower", position + Vector3.up * 4f, Quaternion.identity);
            
            // Staggered fireworks
            StartCoroutine(StaggeredFireworks(position));
        }
        
        private IEnumerator StaggeredFireworks(Vector3 basePosition)
        {
            for (int i = 0; i < 3; i++)
            {
                yield return new WaitForSeconds(0.5f);
                
                Vector3 offset = new Vector3(
                    Random.Range(-3f, 3f),
                    Random.Range(4f, 6f),
                    Random.Range(-3f, 3f)
                );
                
                PlayEffect("Fireworks", basePosition + offset, Quaternion.identity);
            }
        }
        
        public void PlayLevelUp(Vector3 position)
        {
            PlayEffect("LevelUp", position, Quaternion.identity);
            PlayEffect("StarBurst", position, Quaternion.identity);
        }
        
        #endregion
        
        #region UI Effects
        
        public void PlayButtonClick(Vector3 screenPosition)
        {
            Vector3 worldPos = Camera.main.ScreenToWorldPoint(new Vector3(screenPosition.x, screenPosition.y, 10f));
            PlayEffect("ButtonClick", worldPos, Quaternion.identity);
        }
        
        public void PlayPurchaseEffect(Vector3 screenPosition)
        {
            Vector3 worldPos = Camera.main.ScreenToWorldPoint(new Vector3(screenPosition.x, screenPosition.y, 10f));
            PlayEffect("Purchase", worldPos, Quaternion.identity);
            PlayEffect("CoinShower", worldPos, Quaternion.identity);
        }
        
        #endregion
        
        #region Utility Methods
        
        private ParticleSystem GetPooledEffect(string effectName)
        {
            if (!effectPools.ContainsKey(effectName)) return null;
            
            Queue<ParticleSystem> pool = effectPools[effectName];
            
            if (pool.Count > 0)
            {
                return pool.Dequeue();
            }
            else
            {
                // Create new instance if pool is empty
                if (effectPrefabs.ContainsKey(effectName))
                {
                    GameObject obj = Instantiate(effectPrefabs[effectName].gameObject, transform);
                    obj.name = $"{effectName}_Pooled_Extra";
                    return obj.GetComponent<ParticleSystem>();
                }
            }
            
            return null;
        }
        
        private void PlayEffect(string effectName, Vector3 position, Quaternion rotation)
        {
            ParticleSystem effect = GetPooledEffect(effectName);
            if (effect != null)
            {
                effect.transform.position = position;
                effect.transform.rotation = rotation;
                effect.gameObject.SetActive(true);
                effect.Play();
                
                StartCoroutine(ReturnToPool(effect, effectName));
            }
        }
        
        private IEnumerator ReturnToPool(ParticleSystem effect, string effectName)
        {
            // Wait for effect to complete
            yield return new WaitForSeconds(effect.main.duration + effect.main.startLifetime.constantMax);
            
            effect.gameObject.SetActive(false);
            
            if (effectPools.ContainsKey(effectName))
            {
                effectPools[effectName].Enqueue(effect);
            }
        }
        
        private IEnumerator DelayedEffect(string effectName, Vector3 position, float delay)
        {
            yield return new WaitForSeconds(delay);
            PlayEffect(effectName, position, Quaternion.identity);
        }
        
        private void CreateWaterRipple(Vector3 position)
        {
            // This would create a water ripple effect using a custom shader
            // For now, just use particle effect
            ParticleSystem ripple = GetPooledEffect("WaterSplash");
            if (ripple != null)
            {
                ripple.transform.position = position;
                ripple.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);
                
                var main = ripple.main;
                main.startSpeed = 0.5f;
                main.startSize = 2f;
                
                ripple.gameObject.SetActive(true);
                ripple.Play();
                
                StartCoroutine(ReturnToPool(ripple, "WaterSplash"));
            }
        }
        
        #endregion
        
        #region Settings
        
        public void SetEffectQuality(QualityLevel quality)
        {
            foreach (var pool in effectPools.Values)
            {
                foreach (var effect in pool)
                {
                    var main = effect.main;
                    
                    switch (quality)
                    {
                        case QualityLevel.Low:
                            main.maxParticles = 50;
                            break;
                        case QualityLevel.Medium:
                            main.maxParticles = 100;
                            break;
                        case QualityLevel.High:
                            main.maxParticles = 200;
                            break;
                    }
                }
            }
        }
        
        #endregion
    }
    
    public enum QualityLevel
    {
        Low,
        Medium,
        High
    }
}