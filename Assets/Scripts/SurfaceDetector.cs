using UnityEngine;
using System.Collections.Generic;

namespace MicrogolfMasters
{
    public class SurfaceDetector : MonoBehaviour
    {
        [Header("Detection Settings")]
        [SerializeField] private float detectionRadius = 0.2f;
        [SerializeField] private LayerMask surfaceLayerMask;
        [SerializeField] private int maxOverlapResults = 10;
        
        [Header("Surface Priorities")]
        [SerializeField] private Dictionary<SurfaceType, int> surfacePriorities = new Dictionary<SurfaceType, int>
        {
            { SurfaceType.Water, 5 },
            { SurfaceType.SpeedBoost, 4 },
            { SurfaceType.Ice, 3 },
            { SurfaceType.Sand, 2 },
            { SurfaceType.Grass, 1 }
        };
        
        private Collider2D[] overlapResults;
        private SurfaceType currentSurface = SurfaceType.Grass;
        private List<SurfaceType> detectedSurfaces = new List<SurfaceType>();
        
        private void Awake()
        {
            overlapResults = new Collider2D[maxOverlapResults];
        }
        
        private void FixedUpdate()
        {
            DetectSurfaces();
        }
        
        private void DetectSurfaces()
        {
            detectedSurfaces.Clear();
            
            // Check for overlapping surface colliders
            int numOverlaps = Physics2D.OverlapCircleNonAlloc(
                transform.position, 
                detectionRadius, 
                overlapResults, 
                surfaceLayerMask
            );
            
            // Collect all detected surfaces
            for (int i = 0; i < numOverlaps; i++)
            {
                SurfaceProperties surface = overlapResults[i].GetComponent<SurfaceProperties>();
                if (surface != null)
                {
                    detectedSurfaces.Add(surface.surfaceType);
                }
            }
            
            // Determine current surface based on priority
            currentSurface = GetHighestPrioritySurface();
        }
        
        private SurfaceType GetHighestPrioritySurface()
        {
            if (detectedSurfaces.Count == 0)
                return SurfaceType.Grass;
            
            SurfaceType highestPriority = SurfaceType.Grass;
            int maxPriority = 0;
            
            foreach (SurfaceType surface in detectedSurfaces)
            {
                if (surfacePriorities.TryGetValue(surface, out int priority))
                {
                    if (priority > maxPriority)
                    {
                        maxPriority = priority;
                        highestPriority = surface;
                    }
                }
            }
            
            return highestPriority;
        }
        
        public SurfaceType GetCurrentSurface()
        {
            return currentSurface;
        }
        
        public bool IsOnSurface(SurfaceType surfaceType)
        {
            return detectedSurfaces.Contains(surfaceType);
        }
        
        public float GetSurfaceFriction()
        {
            switch (currentSurface)
            {
                case SurfaceType.Ice:
                    return 0.05f;
                case SurfaceType.Sand:
                    return 0.8f;
                case SurfaceType.Water:
                    return 1f;
                case SurfaceType.SpeedBoost:
                    return 0.2f;
                default:
                    return 0.3f;
            }
        }
        
        public float GetSurfaceSpeedMultiplier()
        {
            switch (currentSurface)
            {
                case SurfaceType.Ice:
                    return 1.8f;
                case SurfaceType.Sand:
                    return 0.5f;
                case SurfaceType.SpeedBoost:
                    return 2.5f;
                default:
                    return 1f;
            }
        }
        
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, detectionRadius);
        }
    }
}