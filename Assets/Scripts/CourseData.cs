using UnityEngine;
using System.Collections.Generic;

namespace MicrogolfMasters
{
    [CreateAssetMenu(fileName = "CourseData", menuName = "MicrogolfMasters/Course Data")]
    public class CourseData : ScriptableObject
    {
        [Header("Course Info")]
        public string courseName = "Green Hills";
        public string courseDescription = "A beginner-friendly course";
        public int courseId;
        public CourseTheme theme = CourseTheme.Grass;
        public CourseDifficulty difficulty = CourseDifficulty.Easy;
        
        [Header("Visual Settings")]
        public Sprite coursePreview;
        public Color primaryColor = Color.green;
        public Color secondaryColor = Color.blue;
        public GameObject coursePrefab;
        
        [Header("Holes")]
        public List<CourseHoleData> holes = new List<CourseHoleData>(9);
        
        [Header("Requirements")]
        public int requiredLeague = 18; // League 18 (beginner)
        public int requiredLevel = 1;
        public bool isLocked = false;
        
        [Header("Environment")]
        public bool hasWind = false;
        public Vector2 windDirection = Vector2.right;
        public float windStrength = 0f;
        public WeatherType weather = WeatherType.Sunny;
        
        public int GetTotalPar()
        {
            int totalPar = 0;
            foreach (var hole in holes)
            {
                totalPar += hole.par;
            }
            return totalPar;
        }
        
        public float GetAverageDifficulty()
        {
            if (holes.Count == 0) return 0f;
            
            float totalDifficulty = 0f;
            foreach (var hole in holes)
            {
                totalDifficulty += hole.difficultyRating;
            }
            return totalDifficulty / holes.Count;
        }
        
        public bool HasHazardType(SurfaceType hazardType)
        {
            foreach (var hole in holes)
            {
                if (hole.surfaceTypes.Contains(hazardType))
                    return true;
            }
            return false;
        }
    }
    
    [System.Serializable]
    public class CourseHoleData
    {
        [Header("Basic Info")]
        public int holeNumber = 1;
        public int par = 4;
        public float holeLength = 10f; // In units
        
        [Header("Positions")]
        public Vector3 holePosition;
        public Vector3[] spawnPoints = new Vector3[4];
        public Bounds cameraBounds;
        
        [Header("Difficulty")]
        [Range(1f, 10f)] public float difficultyRating = 5f;
        public List<SurfaceType> surfaceTypes = new List<SurfaceType>();
        public List<ObstacleType> obstacles = new List<ObstacleType>();
        
        [Header("Layout")]
        public HoleShape shape = HoleShape.Straight;
        public bool hasElevationChanges = false;
        public float maxElevation = 0f;
        public float minElevation = 0f;
        
        [Header("Special Features")]
        public bool hasMovingObstacles = false;
        public bool hasMultiplePaths = false;
        public bool hasShortcut = false;
        public Vector3 shortcutPosition;
        
        [Header("Scoring")]
        public int eagleScore = 2; // Shots for eagle
        public int birdieScore = 3; // Shots for birdie
        public int maxShots = 8; // Before automatic completion
        
        public float GetDifficultyMultiplier()
        {
            return 0.5f + (difficultyRating / 10f);
        }
        
        public int GetRewardMultiplier()
        {
            // Higher difficulty = better rewards
            return Mathf.CeilToInt(difficultyRating / 3f);
        }
    }
    
    public enum CourseTheme
    {
        Grass,
        Desert,
        Snow,
        Beach,
        Forest,
        Mountain,
        Urban,
        Space,
        Underwater,
        Volcanic
    }
    
    public enum CourseDifficulty
    {
        Easy,
        Medium,
        Hard,
        Expert,
        Master
    }
    
    public enum WeatherType
    {
        Sunny,
        Cloudy,
        Rainy,
        Windy,
        Snowy,
        Foggy
    }
    
    public enum HoleShape
    {
        Straight,
        DoglegLeft,
        DoglegRight,
        SCurve,
        Island,
        Spiral,
        Maze,
        Loop
    }
    
    public enum ObstacleType
    {
        Wall,
        MovingWall,
        Windmill,
        Pendulum,
        RotatingBar,
        Bumper,
        Portal,
        Conveyor,
        Fan,
        Crusher
    }
}