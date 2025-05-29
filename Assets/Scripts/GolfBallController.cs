using UnityEngine;
using UnityEngine.EventSystems;
using Mirror;
using System.Collections;

namespace MicrogolfMasters
{
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(CircleCollider2D))]
    public class GolfBallController : NetworkBehaviour
    {
        [Header("Movement Settings")]
        [SerializeField] private float maxPower = 50f;
        [SerializeField] private float powerMultiplier = 2f;
        [SerializeField] private float minPower = 5f;
        [SerializeField] private float dragSensitivity = 0.1f;
        
        [Header("Physics Settings")]
        [SerializeField] private float linearDrag = 0.5f;
        [SerializeField] private float angularDrag = 1f;
        [SerializeField] private float bounceReduction = 0.8f;
        [SerializeField] private float stopVelocityThreshold = 0.1f;
        
        [Header("Visual Feedback")]
        [SerializeField] private LineRenderer trajectoryLine;
        [SerializeField] private GameObject powerIndicator;
        [SerializeField] private TrailRenderer ballTrail;
        [SerializeField] private ParticleSystem hitParticles;
        
        [Header("Ball Stats")]
        [SyncVar] private float strength = 50f;
        [SyncVar] private float accuracy = 50f;
        [SyncVar] private int ballId = 0;
        
        // State Management
        [SyncVar] private bool isAiming = false;
        [SyncVar] private bool canShoot = true;
        [SyncVar] private Vector2 lastShotPosition;
        [SyncVar] private int shotCount = 0;
        
        // Components
        private Rigidbody2D rb;
        private CircleCollider2D col;
        private Camera mainCamera;
        private SurfaceDetector surfaceDetector;
        
        // Input Management
        private Vector2 startDragPosition;
        private Vector2 currentDragPosition;
        private bool isDragging = false;
        
        // Surface Effects
        private float currentSurfaceModifier = 1f;
        private SurfaceType currentSurface = SurfaceType.Grass;
        
        // Networking
        [SyncVar(hook = nameof(OnPositionChanged))]
        private Vector2 syncPosition;
        [SyncVar(hook = nameof(OnVelocityChanged))]
        private Vector2 syncVelocity;
        
        private void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            col = GetComponent<CircleCollider2D>();
            surfaceDetector = GetComponent<SurfaceDetector>();
            mainCamera = Camera.main;
            
            // Configure physics
            rb.drag = linearDrag;
            rb.angularDrag = angularDrag;
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        }
        
        public override void OnStartLocalPlayer()
        {
            base.OnStartLocalPlayer();
            CameraController.Instance.SetTarget(transform);
            UIManager.Instance.SetLocalPlayer(this);
        }
        
        private void Update()
        {
            if (!isLocalPlayer) return;
            
            HandleInput();
            UpdateVisuals();
            
            // Check if ball has stopped
            if (rb.velocity.magnitude < stopVelocityThreshold && canShoot == false)
            {
                OnBallStopped();
            }
        }
        
        private void FixedUpdate()
        {
            if (!isServer) return;
            
            // Apply surface effects
            ApplySurfaceEffects();
            
            // Sync position and velocity
            syncPosition = rb.position;
            syncVelocity = rb.velocity;
            
            // Gradually slow down based on surface
            if (rb.velocity.magnitude > 0)
            {
                rb.velocity *= (1f - Time.fixedDeltaTime * currentSurfaceModifier * 0.1f);
            }
        }
        
        private void HandleInput()
        {
            if (!canShoot || EventSystem.current.IsPointerOverGameObject()) return;
            
            if (Input.GetMouseButtonDown(0))
            {
                StartAiming();
            }
            else if (Input.GetMouseButton(0) && isDragging)
            {
                UpdateAiming();
            }
            else if (Input.GetMouseButtonUp(0) && isDragging)
            {
                Shoot();
            }
        }
        
        private void StartAiming()
        {
            Vector3 mouseWorldPos = mainCamera.ScreenToWorldPoint(Input.mousePosition);
            mouseWorldPos.z = 0;
            
            float distance = Vector2.Distance(mouseWorldPos, transform.position);
            if (distance < 1f) // Click near ball
            {
                isDragging = true;
                isAiming = true;
                startDragPosition = mouseWorldPos;
                
                if (trajectoryLine) trajectoryLine.enabled = true;
                if (powerIndicator) powerIndicator.SetActive(true);
                
                AudioManager.Instance.PlaySound("AimStart");
            }
        }
        
        private void UpdateAiming()
        {
            Vector3 mouseWorldPos = mainCamera.ScreenToWorldPoint(Input.mousePosition);
            mouseWorldPos.z = 0;
            currentDragPosition = mouseWorldPos;
            
            // Calculate shot direction and power
            Vector2 shotDirection = (Vector2)transform.position - currentDragPosition;
            float distance = shotDirection.magnitude * dragSensitivity;
            float power = Mathf.Clamp(distance * powerMultiplier, minPower, maxPower);
            
            // Apply accuracy modifier
            float accuracyOffset = (100f - accuracy) / 100f * 0.1f;
            Vector2 accuracyModifier = new Vector2(
                Random.Range(-accuracyOffset, accuracyOffset),
                Random.Range(-accuracyOffset, accuracyOffset)
            );
            
            // Update trajectory preview
            UpdateTrajectoryPreview(shotDirection.normalized + accuracyModifier, power);
            UpdatePowerIndicator(power / maxPower);
        }
        
        private void UpdateTrajectoryPreview(Vector2 direction, float power)
        {
            if (!trajectoryLine) return;
            
            int segments = 30;
            Vector3[] points = new Vector3[segments];
            
            Vector2 startPos = transform.position;
            Vector2 velocity = direction * power * (strength / 100f);
            
            for (int i = 0; i < segments; i++)
            {
                float time = i * 0.1f;
                points[i] = startPos + velocity * time;
                velocity *= 0.95f; // Simulate drag
            }
            
            trajectoryLine.positionCount = segments;
            trajectoryLine.SetPositions(points);
        }
        
        private void UpdatePowerIndicator(float normalizedPower)
        {
            if (!powerIndicator) return;
            
            // Scale and color based on power
            float scale = 0.5f + normalizedPower * 1.5f;
            powerIndicator.transform.localScale = Vector3.one * scale;
            
            // Color gradient from green to red
            Color color = Color.Lerp(Color.green, Color.red, normalizedPower);
            powerIndicator.GetComponent<SpriteRenderer>().color = color;
        }
        
        private void Shoot()
        {
            if (!canShoot || !isDragging) return;
            
            isDragging = false;
            isAiming = false;
            
            Vector2 shotDirection = (Vector2)transform.position - currentDragPosition;
            float distance = shotDirection.magnitude * dragSensitivity;
            float power = Mathf.Clamp(distance * powerMultiplier, minPower, maxPower);
            
            // Apply accuracy
            float accuracyOffset = (100f - accuracy) / 100f * 0.1f;
            Vector2 accuracyModifier = new Vector2(
                Random.Range(-accuracyOffset, accuracyOffset),
                Random.Range(-accuracyOffset, accuracyOffset)
            );
            
            Vector2 finalDirection = shotDirection.normalized + accuracyModifier;
            float finalPower = power * (strength / 100f);
            
            // Execute shot
            CmdShoot(finalDirection, finalPower);
            
            // Hide visuals
            if (trajectoryLine) trajectoryLine.enabled = false;
            if (powerIndicator) powerIndicator.SetActive(false);
        }
        
        [Command]
        private void CmdShoot(Vector2 direction, float power)
        {
            if (!canShoot) return;
            
            canShoot = false;
            shotCount++;
            lastShotPosition = transform.position;
            
            // Apply force
            rb.velocity = Vector2.zero;
            rb.angularVelocity = 0f;
            rb.AddForce(direction * power, ForceMode2D.Impulse);
            
            // Trigger effects
            RpcOnShoot(direction, power);
            
            // Update game state
            GameManager.Instance.OnPlayerShoot(connectionToClient, shotCount);
        }
        
        [ClientRpc]
        private void RpcOnShoot(Vector2 direction, float power)
        {
            // Visual effects
            if (hitParticles)
            {
                hitParticles.transform.rotation = Quaternion.LookRotation(direction);
                hitParticles.Play();
            }
            
            if (ballTrail) ballTrail.enabled = true;
            
            AudioManager.Instance.PlaySound("BallHit", Mathf.Clamp01(power / maxPower));
        }
        
        private void OnBallStopped()
        {
            if (!isServer) return;
            
            canShoot = true;
            if (ballTrail) ballTrail.enabled = false;
            
            // Check win condition
            if (HoleDetector.Instance.IsBallInHole(transform.position))
            {
                GameManager.Instance.OnPlayerFinished(connectionToClient, shotCount);
            }
        }
        
        private void ApplySurfaceEffects()
        {
            currentSurface = surfaceDetector.GetCurrentSurface();
            
            switch (currentSurface)
            {
                case SurfaceType.Ice:
                    currentSurfaceModifier = 0.1f; // Very low friction
                    rb.drag = 0.05f;
                    break;
                case SurfaceType.Sand:
                    currentSurfaceModifier = 3f; // High friction
                    rb.drag = 2f;
                    break;
                case SurfaceType.SpeedBoost:
                    rb.velocity = rb.velocity.normalized * maxPower * 0.8f;
                    currentSurfaceModifier = 0.5f;
                    break;
                case SurfaceType.Water:
                    ResetToLastPosition();
                    break;
                default: // Grass
                    currentSurfaceModifier = 1f;
                    rb.drag = linearDrag;
                    break;
            }
        }
        
        private void ResetToLastPosition()
        {
            if (!isServer) return;
            
            rb.velocity = Vector2.zero;
            rb.angularVelocity = 0f;
            transform.position = lastShotPosition;
            canShoot = true;
            
            RpcOnWaterHazard();
        }
        
        [ClientRpc]
        private void RpcOnWaterHazard()
        {
            AudioManager.Instance.PlaySound("WaterSplash");
            ParticleManager.Instance.PlayWaterSplash(transform.position);
            UIManager.Instance.ShowMessage("Water Hazard!", 1.5f);
        }
        
        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (!isServer) return;
            
            // Wall bounce
            if (collision.gameObject.layer == LayerMask.NameToLayer("Wall"))
            {
                Vector2 incomingVector = rb.velocity.normalized;
                Vector2 normal = collision.contacts[0].normal;
                Vector2 reflectedVector = Vector2.Reflect(incomingVector, normal);
                
                rb.velocity = reflectedVector * rb.velocity.magnitude * bounceReduction;
                
                RpcOnWallBounce(collision.contacts[0].point);
            }
        }
        
        [ClientRpc]
        private void RpcOnWallBounce(Vector2 hitPoint)
        {
            AudioManager.Instance.PlaySound("WallBounce");
            ParticleManager.Instance.PlayWallHit(hitPoint);
        }
        
        // Synchronization hooks
        private void OnPositionChanged(Vector2 oldPos, Vector2 newPos)
        {
            if (isServer) return;
            transform.position = newPos;
        }
        
        private void OnVelocityChanged(Vector2 oldVel, Vector2 newVel)
        {
            if (isServer) return;
            rb.velocity = newVel;
        }
        
        // Public methods
        public void SetBallStats(BallData ballData)
        {
            ballId = ballData.id;
            strength = ballData.strength;
            accuracy = ballData.accuracy;
            
            // Update visuals
            GetComponent<SpriteRenderer>().sprite = ballData.sprite;
            if (ballTrail) ballTrail.colorGradient = ballData.trailGradient;
        }
        
        public void ResetBall(Vector2 startPosition)
        {
            transform.position = startPosition;
            rb.velocity = Vector2.zero;
            rb.angularVelocity = 0f;
            shotCount = 0;
            canShoot = true;
            lastShotPosition = startPosition;
        }
        
        public int GetShotCount() => shotCount;
        public bool IsAiming() => isAiming;
        public bool CanShoot() => canShoot;
        public float GetStrength() => strength;
        public float GetAccuracy() => accuracy;
    }
    
    public enum SurfaceType
    {
        Grass,
        Ice,
        Sand,
        Water,
        SpeedBoost
    }
}