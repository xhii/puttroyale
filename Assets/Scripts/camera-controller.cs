using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace MicrogolfMasters
{
    public class CameraController : MonoBehaviour
    {
        private static CameraController _instance;
        public static CameraController Instance => _instance;
        
        [Header("Camera Settings")]
        [SerializeField] private float smoothSpeed = 5f;
        [SerializeField] private Vector3 offset = new Vector3(0f, 10f, -10f);
        [SerializeField] private float minZoom = 5f;
        [SerializeField] private float maxZoom = 15f;
        [SerializeField] private float zoomSpeed = 2f;
        
        [Header("Bounds")]
        [SerializeField] private bool useBounds = true;
        [SerializeField] private Bounds courseBounds;
        [SerializeField] private float boundsPadding = 2f;
        
        [Header("Shake Settings")]
        [SerializeField] private float shakeDecay = 0.5f;
        [SerializeField] private float shakeIntensityMultiplier = 1f;
        
        [Header("Follow Settings")]
        [SerializeField] private bool autoFollowBall = true;
        [SerializeField] private float followDelay = 0.5f;
        [SerializeField] private AnimationCurve followCurve;
        
        [Header("Cinematic")]
        [SerializeField] private float cinematicMoveSpeed = 3f;
        [SerializeField] private List<Transform> cinematicPoints = new List<Transform>();
        
        // Components
        private Camera mainCamera;
        private Transform currentTarget;
        
        // State
        private Vector3 targetPosition;
        private float targetZoom;
        private bool isFollowing = true;
        private bool isCinematicMode = false;
        
        // Shake
        private float shakeIntensity = 0f;
        private Vector3 shakeOffset;
        
        // Pan and zoom
        private Vector2 lastTouchPos0;
        private Vector2 lastTouchPos1;
        private bool isPanning = false;
        
        // Effects
        private Coroutine flashCoroutine;
        private Coroutine cinematicCoroutine;
        
        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            
            mainCamera = GetComponent<Camera>();
            if (mainCamera == null)
            {
                mainCamera = Camera.main;
            }
            
            targetZoom = mainCamera.orthographicSize;
            targetPosition = transform.position;
        }
        
        private void LateUpdate()
        {
            if (isCinematicMode) return;
            
            HandleInput();
            UpdateCameraPosition();
            UpdateCameraZoom();
            UpdateShake();
        }
        
        #region Camera Movement
        
        private void UpdateCameraPosition()
        {
            if (currentTarget != null && isFollowing)
            {
                // Calculate target position
                Vector3 desiredPosition = currentTarget.position + offset;
                
                // Apply bounds if enabled
                if (useBounds)
                {
                    desiredPosition = ClampPositionToBounds(desiredPosition);
                }
                
                targetPosition = desiredPosition;
            }
            
            // Smoothly move to target position
            Vector3 smoothedPosition = Vector3.Lerp(transform.position, targetPosition, smoothSpeed * Time.deltaTime);
            
            // Apply shake offset
            transform.position = smoothedPosition + shakeOffset;
        }
        
        private Vector3 ClampPositionToBounds(Vector3 position)
        {
            float halfHeight = mainCamera.orthographicSize;
            float halfWidth = halfHeight * mainCamera.aspect;
            
            float minX = courseBounds.min.x + halfWidth + boundsPadding;
            float maxX = courseBounds.max.x - halfWidth - boundsPadding;
            float minZ = courseBounds.min.z + halfHeight + boundsPadding;
            float maxZ = courseBounds.max.z - halfHeight - boundsPadding;
            
            position.x = Mathf.Clamp(position.x, minX, maxX);
            position.z = Mathf.Clamp(position.z, minZ, maxZ);
            
            return position;
        }
        
        public void SetTarget(Transform target)
        {
            currentTarget = target;
            
            if (autoFollowBall)
            {
                StartCoroutine(DelayedFollow());
            }
        }
        
        private IEnumerator DelayedFollow()
        {
            isFollowing = false;
            yield return new WaitForSeconds(followDelay);
            isFollowing = true;
        }
        
        public void SetPosition(Vector3 position)
        {
            targetPosition = position;
            transform.position = position;
        }
        
        public void SetCourseBounds(Bounds bounds)
        {
            courseBounds = bounds;
        }
        
        #endregion
        
        #region Camera Zoom
        
        private void UpdateCameraZoom()
        {
            if (Mathf.Abs(mainCamera.orthographicSize - targetZoom) > 0.01f)
            {
                mainCamera.orthographicSize = Mathf.Lerp(mainCamera.orthographicSize, targetZoom, zoomSpeed * Time.deltaTime);
            }
        }
        
        public void SetZoom(float zoom)
        {
            targetZoom = Mathf.Clamp(zoom, minZoom, maxZoom);
        }
        
        public void ZoomIn()
        {
            targetZoom = Mathf.Max(targetZoom - 1f, minZoom);
        }
        
        public void ZoomOut()
        {
            targetZoom = Mathf.Min(targetZoom + 1f, maxZoom);
        }
        
        #endregion
        
        #region Input Handling
        
        private void HandleInput()
        {
            // Handle touch input for mobile
            if (Input.touchCount > 0)
            {
                HandleTouchInput();
            }
            // Handle mouse input for editor/desktop
            else if (Application.isEditor || Application.platform == RuntimePlatform.WindowsPlayer)
            {
                HandleMouseInput();
            }
        }
        
        private void HandleTouchInput()
        {
            if (Input.touchCount == 1)
            {
                Touch touch = Input.GetTouch(0);
                
                if (touch.phase == TouchPhase.Began)
                {
                    lastTouchPos0 = touch.position;
                    isPanning = true;
                    isFollowing = false;
                }
                else if (touch.phase == TouchPhase.Moved && isPanning)
                {
                    Vector2 delta = touch.position - lastTouchPos0;
                    PanCamera(delta);
                    lastTouchPos0 = touch.position;
                }
                else if (touch.phase == TouchPhase.Ended)
                {
                    isPanning = false;
                    
                    // Resume following after a delay
                    if (autoFollowBall && currentTarget != null)
                    {
                        StartCoroutine(DelayedFollow());
                    }
                }
            }
            else if (Input.touchCount == 2)
            {
                // Pinch to zoom
                Touch touch0 = Input.GetTouch(0);
                Touch touch1 = Input.GetTouch(1);
                
                if (touch0.phase == TouchPhase.Began || touch1.phase == TouchPhase.Began)
                {
                    lastTouchPos0 = touch0.position;
                    lastTouchPos1 = touch1.position;
                }
                else if (touch0.phase == TouchPhase.Moved || touch1.phase == TouchPhase.Moved)
                {
                    float prevDistance = Vector2.Distance(lastTouchPos0, lastTouchPos1);
                    float currentDistance = Vector2.Distance(touch0.position, touch1.position);
                    
                    float delta = prevDistance - currentDistance;
                    targetZoom += delta * 0.01f;
                    targetZoom = Mathf.Clamp(targetZoom, minZoom, maxZoom);
                    
                    lastTouchPos0 = touch0.position;
                    lastTouchPos1 = touch1.position;
                }
            }
        }
        
        private void HandleMouseInput()
        {
            // Pan with middle mouse button
            if (Input.GetMouseButtonDown(2))
            {
                isPanning = true;
                isFollowing = false;
            }
            else if (Input.GetMouseButton(2))
            {
                float x = Input.GetAxis("Mouse X");
                float y = Input.GetAxis("Mouse Y");
                PanCamera(new Vector2(-x, -y) * 10f);
            }
            else if (Input.GetMouseButtonUp(2))
            {
                isPanning = false;
                
                if (autoFollowBall && currentTarget != null)
                {
                    StartCoroutine(DelayedFollow());
                }
            }
            
            // Zoom with scroll wheel
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (scroll != 0f)
            {
                targetZoom -= scroll * 5f;
                targetZoom = Mathf.Clamp(targetZoom, minZoom, maxZoom);
            }
        }
        
        private void PanCamera(Vector2 delta)
        {
            float sensitivity = mainCamera.orthographicSize * 0.001f;
            Vector3 movement = new Vector3(delta.x * sensitivity, 0, delta.y * sensitivity);
            targetPosition -= movement;
            
            if (useBounds)
            {
                targetPosition = ClampPositionToBounds(targetPosition);
            }
        }
        
        #endregion
        
        #region Camera Shake
        
        public void Shake(float intensity, float duration)
        {
            StartCoroutine(ShakeCoroutine(intensity, duration));
        }
        
        private IEnumerator ShakeCoroutine(float intensity, float duration)
        {
            shakeIntensity = intensity * shakeIntensityMultiplier;
            float elapsed = 0f;
            
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                shakeIntensity = Mathf.Lerp(intensity * shakeIntensityMultiplier, 0f, elapsed / duration);
                yield return null;
            }
            
            shakeIntensity = 0f;
            shakeOffset = Vector3.zero;
        }
        
        private void UpdateShake()
        {
            if (shakeIntensity > 0f)
            {
                shakeOffset = Random.insideUnitSphere * shakeIntensity;
                shakeOffset.y = 0f; // Keep shake horizontal
                shakeIntensity *= (1f - shakeDecay * Time.deltaTime);
                
                if (shakeIntensity < 0.01f)
                {
                    shakeIntensity = 0f;
                    shakeOffset = Vector3.zero;
                }
            }
        }
        
        #endregion
        
        #region Camera Effects
        
        public void FlashColor(Color color, float duration)
        {
            if (flashCoroutine != null)
            {
                StopCoroutine(flashCoroutine);
            }
            
            flashCoroutine = StartCoroutine(FlashCoroutine(color, duration));
        }
        
        private IEnumerator FlashCoroutine(Color color, float duration)
        {
            // This would use a full-screen overlay
            // For now, we'll use camera background color
            Color originalColor = mainCamera.backgroundColor;
            mainCamera.backgroundColor = color;
            
            yield return new WaitForSeconds(duration);
            
            mainCamera.backgroundColor = originalColor;
            flashCoroutine = null;
        }
        
        public void FocusOnPoint(Vector3 point, float zoomLevel, float duration)
        {
            StartCoroutine(FocusCoroutine(point, zoomLevel, duration));
        }
        
        private IEnumerator FocusCoroutine(Vector3 point, float zoomLevel, float duration)
        {
            Vector3 startPos = transform.position;
            float startZoom = mainCamera.orthographicSize;
            
            isFollowing = false;
            float elapsed = 0f;
            
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = followCurve.Evaluate(elapsed / duration);
                
                targetPosition = Vector3.Lerp(startPos, point + offset, t);
                targetZoom = Mathf.Lerp(startZoom, zoomLevel, t);
                
                yield return null;
            }
            
            // Resume following
            if (autoFollowBall && currentTarget != null)
            {
                isFollowing = true;
            }
        }
        
        #endregion
        
        #region Cinematic Mode
        
        public void StartCinematic(List<CinematicPoint> points)
        {
            if (cinematicCoroutine != null)
            {
                StopCoroutine(cinematicCoroutine);
            }
            
            cinematicCoroutine = StartCoroutine(CinematicSequence(points));
        }
        
        private IEnumerator CinematicSequence(List<CinematicPoint> points)
        {
            isCinematicMode = true;
            isFollowing = false;
            
            foreach (var point in points)
            {
                yield return StartCoroutine(MoveToPoint(point));
                
                if (point.waitTime > 0f)
                {
                    yield return new WaitForSeconds(point.waitTime);
                }
            }
            
            isCinematicMode = false;
            isFollowing = autoFollowBall;
            cinematicCoroutine = null;
        }
        
        private IEnumerator MoveToPoint(CinematicPoint point)
        {
            Vector3 startPos = transform.position;
            Quaternion startRot = transform.rotation;
            float startZoom = mainCamera.orthographicSize;
            
            float elapsed = 0f;
            
            while (elapsed < point.moveTime)
            {
                elapsed += Time.deltaTime;
                float t = point.moveCurve.Evaluate(elapsed / point.moveTime);
                
                transform.position = Vector3.Lerp(startPos, point.position, t);
                transform.rotation = Quaternion.Lerp(startRot, point.rotation, t);
                mainCamera.orthographicSize = Mathf.Lerp(startZoom, point.zoom, t);
                
                yield return null;
            }
        }
        
        public void StopCinematic()
        {
            if (cinematicCoroutine != null)
            {
                StopCoroutine(cinematicCoroutine);
                cinematicCoroutine = null;
            }
            
            isCinematicMode = false;
            isFollowing = autoFollowBall;
        }
        
        #endregion
        
        #region Utility
        
        public Vector3 GetTargetPosition() => targetPosition;
        public float GetCurrentZoom() => mainCamera.orthographicSize;
        public bool IsFollowing() => isFollowing;
        public void SetFollowing(bool follow) => isFollowing = follow;
        
        public Vector3 ScreenToWorldPoint(Vector3 screenPoint)
        {
            return mainCamera.ScreenToWorldPoint(screenPoint);
        }
        
        public Vector3 WorldToScreenPoint(Vector3 worldPoint)
        {
            return mainCamera.WorldToScreenPoint(worldPoint);
        }
        
        #endregion
    }
    
    [System.Serializable]
    public class CinematicPoint
    {
        public Vector3 position;
        public Quaternion rotation;
        public float zoom = 10f;
        public float moveTime = 2f;
        public float waitTime = 0f;
        public AnimationCurve moveCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    }
}