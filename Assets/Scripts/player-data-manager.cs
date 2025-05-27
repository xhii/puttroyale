using UnityEngine;
using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using UnityEngine.Networking;

namespace MicrogolfMasters
{
    public class PlayerDataManager : MonoBehaviour
    {
        private static PlayerDataManager _instance;
        public static PlayerDataManager Instance => _instance;
        
        [Header("Save Settings")]
        [SerializeField] private bool useCloudSave = true;
        [SerializeField] private bool useLocalBackup = true;
        [SerializeField] private float autoSaveInterval = 30f;
        [SerializeField] private string cloudSaveUrl = "https://api.microgolfmasters.com/save";
        
        [Header("Encryption")]
        [SerializeField] private bool encryptSaveData = true;
        [SerializeField] private string encryptionKey = "MicrogolfMasters2024SecretKey!";
        
        // File paths
        private string localSavePath;
        private string backupSavePath;
        private string settingsPath;
        
        // Player data
        private PlayerProfile currentProfile;
        private GameSettings gameSettings;
        private bool isDataLoaded = false;
        private bool isDirty = false;
        
        // Auto save
        private Coroutine autoSaveCoroutine;
        
        // Events
        public event Action<PlayerProfile> OnDataLoaded;
        public event Action<bool> OnDataSaved;
        public event Action<string> OnSaveError;
        
        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);
            
            InitializePaths();
            LoadData();
        }
        
        private void InitializePaths()
        {
            string dataPath = Application.persistentDataPath;
            
            localSavePath = Path.Combine(dataPath, "player_save.dat");
            backupSavePath = Path.Combine(dataPath, "player_backup.dat");
            settingsPath = Path.Combine(dataPath, "settings.json");
            
            // Ensure directory exists
            Directory.CreateDirectory(dataPath);
        }
        
        #region Save/Load
        
        public void SaveData()
        {
            if (currentProfile == null) return;
            
            StartCoroutine(SaveDataCoroutine());
        }
        
        private IEnumerator SaveDataCoroutine()
        {
            // Create save data object
            SaveData saveData = new SaveData
            {
                profile = currentProfile,
                saveTime = DateTime.Now,
                version = Application.version,
                platform = Application.platform.ToString()
            };
            
            // Serialize to JSON
            string jsonData = JsonUtility.ToJson(saveData, true);
            
            // Encrypt if enabled
            if (encryptSaveData)
            {
                jsonData = Encrypt(jsonData);
            }
            
            // Save locally
            bool localSaveSuccess = SaveToFile(localSavePath, jsonData);
            
            if (localSaveSuccess && useLocalBackup)
            {
                // Create backup
                SaveToFile(backupSavePath, jsonData);
            }
            
            // Save to cloud if enabled
            if (useCloudSave && !string.IsNullOrEmpty(currentProfile.playerId))
            {
                yield return StartCoroutine(SaveToCloud(jsonData));
            }
            
            isDirty = false;
            OnDataSaved?.Invoke(localSaveSuccess);
        }
        
        public void LoadData()
        {
            StartCoroutine(LoadDataCoroutine());
        }
        
        private IEnumerator LoadDataCoroutine()
        {
            SaveData loadedData = null;
            
            // Try loading from cloud first
            if (useCloudSave)
            {
                yield return StartCoroutine(LoadFromCloud((data) => loadedData = data));
            }
            
            // If cloud load failed, try local
            if (loadedData == null)
            {
                loadedData = LoadFromFile(localSavePath);
            }
            
            // If local failed, try backup
            if (loadedData == null && useLocalBackup)
            {
                loadedData = LoadFromFile(backupSavePath);
            }
            
            // If all failed, create new profile
            if (loadedData == null)
            {
                CreateNewProfile();
            }
            else
            {
                currentProfile = loadedData.profile;
                
                // Update profile with latest data
                currentProfile.CheckBallRepairs();
                currentProfile.RefillEnergy();
                
                isDataLoaded = true;
                OnDataLoaded?.Invoke(currentProfile);
            }
            
            // Load settings
            LoadSettings();
            
            // Start auto save
            if (autoSaveInterval > 0)
            {
                autoSaveCoroutine = StartCoroutine(AutoSaveCoroutine());
            }
        }
        
        private bool SaveToFile(string path, string data)
        {
            try
            {
                File.WriteAllText(path, data);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to save to {path}: {e.Message}");
                OnSaveError?.Invoke(e.Message);
                return false;
            }
        }
        
        private SaveData LoadFromFile(string path)
        {
            try
            {
                if (!File.Exists(path)) return null;
                
                string data = File.ReadAllText(path);
                
                // Decrypt if needed
                if (encryptSaveData)
                {
                    data = Decrypt(data);
                }
                
                return JsonUtility.FromJson<SaveData>(data);
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to load from {path}: {e.Message}");
                return null;
            }
        }
        
        #endregion
        
        #region Cloud Save
        
        private IEnumerator SaveToCloud(string data)
        {
            if (string.IsNullOrEmpty(currentProfile.playerId)) yield break;
            
            // Create form data
            WWWForm form = new WWWForm();
            form.AddField("playerId", currentProfile.playerId);
            form.AddField("data", data);
            form.AddField("checksum", GetChecksum(data));
            
            using (UnityWebRequest request = UnityWebRequest.Post(cloudSaveUrl, form))
            {
                request.SetRequestHeader("Authorization", GetAuthToken());
                
                yield return request.SendWebRequest();
                
                if (request.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogError($"Cloud save failed: {request.error}");
                    OnSaveError?.Invoke($"Cloud save failed: {request.error}");
                }
            }
        }
        
        private IEnumerator LoadFromCloud(Action<SaveData> callback)
        {
            string playerId = PlayerPrefs.GetString("LastPlayerId", "");
            if (string.IsNullOrEmpty(playerId))
            {
                callback?.Invoke(null);
                yield break;
            }
            
            string url = $"{cloudSaveUrl}?playerId={playerId}";
            
            using (UnityWebRequest request = UnityWebRequest.Get(url))
            {
                request.SetRequestHeader("Authorization", GetAuthToken());
                
                yield return request.SendWebRequest();
                
                if (request.result == UnityWebRequest.Result.Success)
                {
                    try
                    {
                        string data = request.downloadHandler.text;
                        
                        // Decrypt if needed
                        if (encryptSaveData)
                        {
                            data = Decrypt(data);
                        }
                        
                        SaveData saveData = JsonUtility.FromJson<SaveData>(data);
                        callback?.Invoke(saveData);
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"Failed to parse cloud save: {e.Message}");
                        callback?.Invoke(null);
                    }
                }
                else
                {
                    Debug.LogError($"Cloud load failed: {request.error}");
                    callback?.Invoke(null);
                }
            }
        }
        
        private string GetAuthToken()
        {
            // In production, this would be a proper authentication token
            return Convert.ToBase64String(Encoding.UTF8.GetBytes($"{currentProfile.playerId}:{DateTime.Now.Ticks}"));
        }
        
        #endregion
        
        #region Profile Management
        
        private void CreateNewProfile()
        {
            currentProfile = new PlayerProfile();
            currentProfile.playerName = $"Player{UnityEngine.Random.Range(1000, 9999)}";
            
            // Save player ID for future cloud loads
            PlayerPrefs.SetString("LastPlayerId", currentProfile.playerId);
            
            isDataLoaded = true;
            isDirty = true;
            
            OnDataLoaded?.Invoke(currentProfile);
            
            // Save immediately
            SaveData();
        }
        
        public void UpdatePlayerName(string newName)
        {
            if (currentProfile == null) return;
            
            currentProfile.playerName = newName;
            isDirty = true;
        }
        
        public void ResetProgress()
        {
            if (currentProfile == null) return;
            
            // Keep player ID and name
            string playerId = currentProfile.playerId;
            string playerName = currentProfile.playerName;
            
            // Create new profile
            currentProfile = new PlayerProfile();
            currentProfile.playerId = playerId;
            currentProfile.playerName = playerName;
            
            isDirty = true;
            SaveData();
        }
        
        #endregion
        
        #region Settings
        
        private void LoadSettings()
        {
            try
            {
                if (File.Exists(settingsPath))
                {
                    string json = File.ReadAllText(settingsPath);
                    gameSettings = JsonUtility.FromJson<GameSettings>(json);
                }
                else
                {
                    gameSettings = new GameSettings();
                }
            }
            catch
            {
                gameSettings = new GameSettings();
            }
            
            ApplySettings();
        }
        
        public void SaveSettings()
        {
            try
            {
                string json = JsonUtility.ToJson(gameSettings, true);
                File.WriteAllText(settingsPath, json);
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to save settings: {e.Message}");
            }
        }
        
        private void ApplySettings()
        {
            // Apply audio settings
            AudioManager.Instance.SetMasterVolume(gameSettings.masterVolume);
            AudioManager.Instance.SetMusicVolume(gameSettings.musicVolume);
            AudioManager.Instance.SetSFXVolume(gameSettings.sfxVolume);
            
            // Apply graphics settings
            QualitySettings.SetQualityLevel(gameSettings.graphicsQuality);
            Application.targetFrameRate = gameSettings.targetFPS;
            
            // Apply other settings
            Screen.sleepTimeout = gameSettings.preventSleep ? SleepTimeout.NeverSleep : SleepTimeout.SystemSetting;
        }
        
        #endregion
        
        #region Auto Save
        
        private IEnumerator AutoSaveCoroutine()
        {
            while (true)
            {
                yield return new WaitForSeconds(autoSaveInterval);
                
                if (isDirty && isDataLoaded)
                {
                    SaveData();
                }
            }
        }
        
        public void MarkDirty()
        {
            isDirty = true;
        }
        
        #endregion
        
        #region Encryption
        
        private string Encrypt(string text)
        {
            using (Aes aes = Aes.Create())
            {
                aes.Key = Encoding.UTF8.GetBytes(encryptionKey.Substring(0, 32));
                aes.IV = new byte[16];
                
                using (var encryptor = aes.CreateEncryptor())
                {
                    byte[] inputBytes = Encoding.UTF8.GetBytes(text);
                    byte[] encryptedBytes = encryptor.TransformFinalBlock(inputBytes, 0, inputBytes.Length);
                    return Convert.ToBase64String(encryptedBytes);
                }
            }
        }
        
        private string Decrypt(string cipherText)
        {
            using (Aes aes = Aes.Create())
            {
                aes.Key = Encoding.UTF8.GetBytes(encryptionKey.Substring(0, 32));
                aes.IV = new byte[16];
                
                using (var decryptor = aes.CreateDecryptor())
                {
                    byte[] inputBytes = Convert.FromBase64String(cipherText);
                    byte[] decryptedBytes = decryptor.TransformFinalBlock(inputBytes, 0, inputBytes.Length);
                    return Encoding.UTF8.GetString(decryptedBytes);
                }
            }
        }
        
        private string GetChecksum(string data)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(data));
                return Convert.ToBase64String(bytes);
            }
        }
        
        #endregion
        
        #region Public Access
        
        public PlayerProfile GetPlayerProfile() => currentProfile;
        public GameSettings GetGameSettings() => gameSettings;
        public bool IsDataLoaded() => isDataLoaded;
        
        public string GetLocalPlayerId() => currentProfile?.playerId ?? "";
        public string GetPlayerName() => currentProfile?.playerName ?? "Player";
        public int GetPlayerLevel() => currentProfile?.level ?? 1;
        public int GetPlayerLeague() => currentProfile?.league ?? 18;
        public int GetPlayerElo() => currentProfile?.league == 0 ? currentProfile.crowns : 1000 + (18 - currentProfile.league) * 100;
        public int GetSelectedBallId() => currentProfile?.selectedBall?.id ?? 0;
        
        #endregion
        
        private void OnApplicationPause(bool pauseStatus)
        {
            if (pauseStatus && isDirty)
            {
                SaveData();
            }
        }
        
        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus && isDirty)
            {
                SaveData();
            }
        }
        
        private void OnDestroy()
        {
            if (isDirty)
            {
                SaveData();
            }
            
            if (autoSaveCoroutine != null)
            {
                StopCoroutine(autoSaveCoroutine);
            }
        }
    }
    
    [System.Serializable]
    public class SaveData
    {
        public PlayerProfile profile;
        public DateTime saveTime;
        public string version;
        public string platform;
    }
    
    [System.Serializable]
    public class GameSettings
    {
        // Audio
        public float masterVolume = 1f;
        public float musicVolume = 0.7f;
        public float sfxVolume = 1f;
        
        // Graphics
        public int graphicsQuality = 2; // 0=Low, 1=Medium, 2=High
        public int targetFPS = 60;
        public bool showFPS = false;
        
        // Controls
        public float touchSensitivity = 1f;
        public bool invertCamera = false;
        public bool vibration = true;
        
        // Gameplay
        public bool showTrajectory = true;
        public bool autoFollow = true;
        public bool showChat = true;
        
        // Other
        public string language = "en";
        public bool notifications = true;
        public bool preventSleep = true;
    }
}