using UnityEngine;
using System.Collections.Generic;

namespace MicrogolfMasters
{
    public class HoleDetector : MonoBehaviour
    {
        private static HoleDetector _instance;
        public static HoleDetector Instance => _instance;
        
        [Header("Detection Settings")]
        [SerializeField] private float holeRadius = 0.3f;
        [SerializeField] private float captureVelocityThreshold = 5f;
        [SerializeField] private LayerMask ballLayerMask;
        
        [Header("Visual Settings")]
        [SerializeField] private GameObject flagObject;
        [SerializeField] private ParticleSystem holeParticles;
        [SerializeField] private Light holeLight;
        [SerializeField] private float lightPulseSpeed = 2f;
        
        [Header("Animation")]
        [SerializeField] private AnimationCurve ballDropCurve;
        [SerializeField] private float dropDuration = 0.5f;
        
        private CircleCollider2D holeCollider;
        private List<GolfBallController> capturedBalls = new List<GolfBallController>();
        private float baseLightIntensity;
        
        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            
            SetupComponents();
        }
        
        private void SetupComponents()
        {
            // Setup collider
            holeCollider = GetComponent<CircleCollider2D>();
            if (holeCollider == null)
            {
                holeCollider = gameObject.AddComponent<CircleCollider2D>();
            }
            holeCollider.radius = holeRadius;
            holeCollider.isTrigger = true;
            
            // Setup light
            if (holeLight != null)
            {
                baseLightIntensity = holeLight.intensity;
            }
            
            // Ensure proper layer
            gameObject.layer = LayerMask.NameToLayer("Hole");
            gameObject.tag = "Hole";
        }
        
        private void Update()
        {
            // Animate hole light
            if (holeLight != null)
            {
                float pulse = Mathf.Sin(Time.time * lightPulseSpeed) * 0.3f + 0.7f;
                holeLight.intensity = baseLightIntensity * pulse;
            }
            
            // Animate flag
            if (flagObject != null)
            {
                float wave = Mathf.Sin(Time.time * 2f) * 5f;
                flagObject.transform.rotation = Quaternion.Euler(0, 0, wave);
            }
        }
        
        private void OnTriggerEnter2D(Collider2D other)
        {
            if (((1 << other.gameObject.layer) & ballLayerMask) != 0)
            {
                GolfBallController ball = other.GetComponent<GolfBallController>();
                if (ball != null && !capturedBalls.Contains(ball))
                {
                    CheckBallCapture(ball);
                }
            }
        }
        
        private void OnTriggerStay2D(Collider2D other)
        {
            if (((1 << other.gameObject.layer) & ballLayerMask) != 0)
            {
                GolfBallController ball = other.GetComponent<GolfBallController>();
                if (ball != null && !capturedBalls.Contains(ball))
                {
                    CheckBallCapture(ball);
                }
            }
        }
        
        private void CheckBallCapture(GolfBallController ball)
        {
            Rigidbody2D ballRb = ball.GetComponent<Rigidbody2D>();
            if (ballRb == null) return;
            
            // Check if ball is slow enough to be captured
            if (ballRb.velocity.magnitude <= captureVelocityThreshold)
            {
                float distance = Vector2.Distance(ball.transform.position, transform.position);
                
                // Check if ball is close enough to hole center
                if (distance <= holeRadius * 0.7f)
                {
                    CaptureBall(ball);
                }
            }
        }
        
        private void CaptureBall(GolfBallController ball)
        {
            if (capturedBalls.Contains(ball)) return;
            
            capturedBalls.Add(ball);
            
            // Disable ball physics temporarily
            Rigidbody2D ballRb = ball.GetComponent<Rigidbody2D>();
            ballRb.velocity = Vector2.zero;
            ballRb.angularVelocity = 0f;
            ballRb.isKinematic = true;
            
            // Play effects
            PlayCaptureEffects(ball.transform.position);
            
            // Animate ball into hole
            StartCoroutine(AnimateBallIntoHole(ball));
        }
        
        private System.Collections.IEnumerator AnimateBallIntoHole(GolfBallController ball)
        {
            Vector3 startPos = ball.transform.position;
            Vector3 endPos = transform.position;
            float elapsed = 0f;
            
            // Disable ball controls during animation
            ball.enabled = false;
            
            while (elapsed < dropDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / dropDuration;
                
                // Animate position with curve
                float curveValue = ballDropCurve.Evaluate(t);
                ball.transform.position = Vector3.Lerp(startPos, endPos, curveValue);
                
                // Scale down ball as it drops
                float scale = Mathf.Lerp(1f, 0.3f, t);
                ball.transform.localScale = Vector3.one * scale;
                
                yield return null;
            }
            
            // Hide ball
            ball.gameObject.SetActive(false);
            
            // Notify game manager
            if (ball.isLocalPlayer)
            {
                NotifyBallInHole(ball);
            }
        }
        
        private void PlayCaptureEffects(Vector3 position)
        {
            // Particles
            if (holeParticles != null)
            {
                holeParticles.transform.position = position;
                holeParticles.Play();
            }
            
            // Sound
            AudioManager.Instance.PlaySound("BallInHole");
            
            // Camera shake
            CameraController.Instance.Shake(0.2f, 0.3f);
            
            // UI celebration
            UIManager.Instance.ShowHoleInOne();
        }
        
        private void NotifyBallInHole(GolfBallController ball)
        {
            int shotCount = ball.GetShotCount();
            GameManager.Instance.OnPlayerFinished(ball.connectionToClient, shotCount);
        }
        
        public bool IsBallInHole(Vector3 ballPosition)
        {
            float distance = Vector2.Distance(ballPosition, transform.position);
            return distance <= holeRadius;
        }
        
        public void ResetHole()
        {
            capturedBalls.Clear();
            
            // Reset any active animations
            StopAllCoroutines();
        }
        
        public Vector3 GetHolePosition() => transform.position;
        public float GetHoleRadius() => holeRadius;
        
        private void OnDrawGizmos()
        {
            // Draw hole area
            Gizmos.color = Color.black;
            Gizmos.DrawWireSphere(transform.position, holeRadius);
            
            // Draw capture area
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, holeRadius * 0.7f);
            
            // Draw flag position
            if (flagObject != null)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawLine(transform.position, flagObject.transform.position);
            }
        }
    }
}