using UnityEngine;

namespace MicrogolfMasters
{
    public class SurfaceProperties : MonoBehaviour
    {
        [Header("Surface Configuration")]
        public SurfaceType surfaceType = SurfaceType.Grass;
        
        [Header("Physics Properties")]
        [Range(0f, 1f)] public float friction = 0.3f;
        [Range(0f, 3f)] public float speedMultiplier = 1f;
        [Range(0f, 1f)] public float bounceReduction = 0.8f;
        
        [Header("Visual Effects")]
        [SerializeField] private bool enableParticles = true;
        [SerializeField] private ParticleSystem surfaceParticles;
        [SerializeField] private Color particleColor = Color.white;
        
        [Header("Audio")]
        [SerializeField] private string rollingSoundName = "BallRolling";
        [SerializeField] private float audioVolumeMultiplier = 1f;
        
        [Header("Special Effects")]
        [SerializeField] private bool isHazard = false;
        [SerializeField] private bool resetBallOnContact = false;
        [SerializeField] private bool applyConstantForce = false;
        [SerializeField] private Vector2 constantForceDirection = Vector2.zero;
        [SerializeField] private float constantForceMagnitude = 0f;
        
        private void Start()
        {
            SetupVisuals();
            SetupCollider();
        }
        
        private void SetupVisuals()
        {
            // Configure surface renderer
            SpriteRenderer renderer = GetComponent<SpriteRenderer>();
            if (renderer != null)
            {
                switch (surfaceType)
                {
                    case SurfaceType.Ice:
                        renderer.color = new Color(0.7f, 0.9f, 1f, 0.8f);
                        break;
                    case SurfaceType.Sand:
                        renderer.color = new Color(0.96f, 0.87f, 0.7f, 1f);
                        break;
                    case SurfaceType.Water:
                        renderer.color = new Color(0.2f, 0.6f, 0.9f, 0.9f);
                        break;
                    case SurfaceType.SpeedBoost:
                        renderer.color = new Color(1f, 0.8f, 0.2f, 1f);
                        break;
                }
            }
            
            // Setup particle system
            if (enableParticles && surfaceParticles != null)
            {
                var main = surfaceParticles.main;
                main.startColor = particleColor;
            }
        }
        
        private void SetupCollider()
        {
            // Ensure collider is set as trigger for surface detection
            Collider2D col = GetComponent<Collider2D>();
            if (col != null)
            {
                col.isTrigger = true;
                gameObject.layer = LayerMask.NameToLayer("Surface");
            }
        }
        
        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other.CompareTag("GolfBall"))
            {
                OnBallEnterSurface(other.gameObject);
            }
        }
        
        private void OnTriggerStay2D(Collider2D other)
        {
            if (other.CompareTag("GolfBall") && applyConstantForce)
            {
                ApplyConstantForceTooBall(other.gameObject);
            }
        }
        
        private void OnTriggerExit2D(Collider2D other)
        {
            if (other.CompareTag("GolfBall"))
            {
                OnBallExitSurface(other.gameObject);
            }
        }
        
        private void OnBallEnterSurface(GameObject ball)
        {
            // Play enter effect
            if (enableParticles && surfaceParticles != null)
            {
                surfaceParticles.Play();
            }
            
            // Special surface effects
            switch (surfaceType)
            {
                case SurfaceType.Water:
                    if (resetBallOnContact)
                    {
                        AudioManager.Instance.PlaySound("WaterSplash");
                        ParticleManager.Instance.PlayWaterSplash(ball.transform.position);
                    }
                    break;
                    
                case SurfaceType.SpeedBoost:
                    AudioManager.Instance.PlaySound("SpeedBoost");
                    ParticleManager.Instance.PlaySpeedBoost(ball.transform.position);
                    break;
                    
                case SurfaceType.Ice:
                    AudioManager.Instance.PlaySound("IceSlide");
                    break;
                    
                case SurfaceType.Sand:
                    AudioManager.Instance.PlaySound("SandImpact");
                    ParticleManager.Instance.PlaySandPuff(ball.transform.position);
                    break;
            }
        }
        
        private void ApplyConstantForceTooBall(GameObject ball)
        {
            if (!applyConstantForce) return;
            
            Rigidbody2D ballRb = ball.GetComponent<Rigidbody2D>();
            if (ballRb != null)
            {
                ballRb.AddForce(constantForceDirection.normalized * constantForceMagnitude);
            }
        }
        
        private void OnBallExitSurface(GameObject ball)
        {
            if (enableParticles && surfaceParticles != null)
            {
                surfaceParticles.Stop();
            }
        }
        
        public float GetFriction() => friction;
        public float GetSpeedMultiplier() => speedMultiplier;
        public float GetBounceReduction() => bounceReduction;
        public bool IsHazard() => isHazard;
        public bool ShouldResetBall() => resetBallOnContact;
        
        private void OnDrawGizmos()
        {
            // Draw surface type indicator
            Gizmos.color = GetSurfaceGizmoColor();
            
            Bounds bounds = GetComponent<Collider2D>()?.bounds ?? new Bounds(transform.position, Vector3.one);
            Gizmos.DrawWireCube(bounds.center, bounds.size);
            
            // Draw force direction if applicable
            if (applyConstantForce && constantForceMagnitude > 0)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawRay(transform.position, constantForceDirection.normalized * 2f);
            }
        }
        
        private Color GetSurfaceGizmoColor()
        {
            switch (surfaceType)
            {
                case SurfaceType.Ice: return Color.cyan;
                case SurfaceType.Sand: return Color.yellow;
                case SurfaceType.Water: return Color.blue;
                case SurfaceType.SpeedBoost: return new Color(1f, 0.5f, 0f);
                default: return Color.green;
            }
        }
    }
}