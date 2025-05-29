using UnityEngine;
using System.Collections.Generic;
using System.Collections;

namespace MicrogolfMasters
{
    public class AudioManager : MonoBehaviour
    {
        private static AudioManager _instance;
        public static AudioManager Instance => _instance;
        
        [Header("Audio Sources")]
        [SerializeField] private AudioSource musicSource;
        [SerializeField] private AudioSource sfxSource;
        [SerializeField] private AudioSource uiSource;
        [SerializeField] private AudioSource ambientSource;
        
        [Header("Audio Clips")]
        [SerializeField] private List<AudioClip> musicTracks = new List<AudioClip>();
        [SerializeField] private Dictionary<string, AudioClip> soundEffects = new Dictionary<string, AudioClip>();
        
        [Header("Settings")]
        [SerializeField] private float masterVolume = 1f;
        [SerializeField] private float musicVolume = 0.7f;
        [SerializeField] private float sfxVolume = 1f;
        [SerializeField] private float fadeTime = 1f;
        
        [Header("Audio Database")]
        [SerializeField] private AudioDatabase audioDatabase;
        
        // State
        private int currentMusicIndex = 0;
        private Coroutine musicFadeCoroutine;
        private Dictionary<string, float> soundCooldowns = new Dictionary<string, float>();
        private List<AudioSource> pooledSources = new List<AudioSource>();
        
        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);
            
            InitializeAudio();
        }
        
        private void InitializeAudio()
        {
            // Load sound effects from database
            if (audioDatabase != null)
            {
                foreach (var sound in audioDatabase.soundEffects)
                {
                    soundEffects[sound.name] = sound.clip;
                }
                
                musicTracks = new List<AudioClip>(audioDatabase.musicTracks);
            }
            
            // Create audio source pool
            CreateAudioSourcePool();
            
            // Load saved settings
            LoadAudioSettings();
            
            // Start background music
            PlayRandomMusic();
        }
        
        private void CreateAudioSourcePool()
        {
            for (int i = 0; i < 10; i++)
            {
                GameObject sourceObj = new GameObject($"PooledAudioSource_{i}");
                sourceObj.transform.SetParent(transform);
                AudioSource source = sourceObj.AddComponent<AudioSource>();
                source.playOnAwake = false;
                pooledSources.Add(source);
            }
        }
        
        #region Music Control
        
        public void PlayMusic(int trackIndex)
        {
            if (trackIndex < 0 || trackIndex >= musicTracks.Count) return;
            
            if (musicFadeCoroutine != null)
            {
                StopCoroutine(musicFadeCoroutine);
            }
            
            currentMusicIndex = trackIndex;
            musicFadeCoroutine = StartCoroutine(CrossfadeMusic(musicTracks[trackIndex]));
        }
        
        public void PlayRandomMusic()
        {
            if (musicTracks.Count == 0) return;
            
            int randomIndex = Random.Range(0, musicTracks.Count);
            PlayMusic(randomIndex);
        }
        
        public void PlayNextMusic()
        {
            currentMusicIndex = (currentMusicIndex + 1) % musicTracks.Count;
            PlayMusic(currentMusicIndex);
        }
        
        private IEnumerator CrossfadeMusic(AudioClip newClip)
        {
            float startVolume = musicSource.volume;
            
            // Fade out current music
            float elapsed = 0f;
            while (elapsed < fadeTime)
            {
                elapsed += Time.deltaTime;
                musicSource.volume = Mathf.Lerp(startVolume, 0f, elapsed / fadeTime);
                yield return null;
            }
            
            // Switch track
            musicSource.clip = newClip;
            musicSource.Play();
            
            // Fade in new music
            elapsed = 0f;
            while (elapsed < fadeTime)
            {
                elapsed += Time.deltaTime;
                musicSource.volume = Mathf.Lerp(0f, musicVolume * masterVolume, elapsed / fadeTime);
                yield return null;
            }
            
            musicFadeCoroutine = null;
        }
        
        public void StopMusic()
        {
            if (musicFadeCoroutine != null)
            {
                StopCoroutine(musicFadeCoroutine);
            }
            
            musicFadeCoroutine = StartCoroutine(FadeOutMusic());
        }
        
        private IEnumerator FadeOutMusic()
        {
            float startVolume = musicSource.volume;
            float elapsed = 0f;
            
            while (elapsed < fadeTime)
            {
                elapsed += Time.deltaTime;
                musicSource.volume = Mathf.Lerp(startVolume, 0f, elapsed / fadeTime);
                yield return null;
            }
            
            musicSource.Stop();
            musicFadeCoroutine = null;
        }
        
        #endregion
        
        #region Sound Effects
        
        public void PlaySound(string soundName, float volumeScale = 1f)
        {
            if (!soundEffects.ContainsKey(soundName))
            {
                Debug.LogWarning($"Sound effect '{soundName}' not found!");
                return;
            }
            
            // Check cooldown
            if (soundCooldowns.ContainsKey(soundName) && Time.time < soundCooldowns[soundName])
            {
                return;
            }
            
            AudioClip clip = soundEffects[soundName];
            sfxSource.PlayOneShot(clip, sfxVolume * masterVolume * volumeScale);
            
            // Set cooldown to prevent spam
            soundCooldowns[soundName] = Time.time + 0.1f;
        }
        
        public void PlaySoundAtPosition(string soundName, Vector3 position, float volumeScale = 1f)
        {
            if (!soundEffects.ContainsKey(soundName))
            {
                Debug.LogWarning($"Sound effect '{soundName}' not found!");
                return;
            }
            
            AudioClip clip = soundEffects[soundName];
            AudioSource source = GetPooledAudioSource();
            
            if (source != null)
            {
                source.transform.position = position;
                source.clip = clip;
                source.volume = sfxVolume * masterVolume * volumeScale;
                source.spatialBlend = 1f; // 3D sound
                source.Play();
                
                StartCoroutine(ReturnToPool(source, clip.length));
            }
        }
        
        public void PlayUISound(string soundName)
        {
            if (!soundEffects.ContainsKey(soundName))
            {
                Debug.LogWarning($"Sound effect '{soundName}' not found!");
                return;
            }
            
            AudioClip clip = soundEffects[soundName];
            uiSource.PlayOneShot(clip, sfxVolume * masterVolume);
        }
        
        private AudioSource GetPooledAudioSource()
        {
            foreach (var source in pooledSources)
            {
                if (!source.isPlaying)
                {
                    return source;
                }
            }
            
            // Create new source if all are busy
            GameObject sourceObj = new GameObject($"PooledAudioSource_Extra");
            sourceObj.transform.SetParent(transform);
            AudioSource newSource = sourceObj.AddComponent<AudioSource>();
            newSource.playOnAwake = false;
            pooledSources.Add(newSource);
            
            return newSource;
        }
        
        private IEnumerator ReturnToPool(AudioSource source, float delay)
        {
            yield return new WaitForSeconds(delay);
            source.Stop();
            source.clip = null;
            source.transform.position = Vector3.zero;
        }
        
        #endregion
        
        #region Special Sound Effects
        
        public void PlayBallHitSound(float power)
        {
            // Play different sounds based on power
            if (power < 0.3f)
            {
                PlaySound("BallHitSoft");
            }
            else if (power < 0.7f)
            {
                PlaySound("BallHitMedium");
            }
            else
            {
                PlaySound("BallHitHard");
            }
        }
        
        public void PlaySurfaceSound(SurfaceType surface)
        {
            switch (surface)
            {
                case SurfaceType.Grass:
                    PlaySound("RollGrass");
                    break;
                case SurfaceType.Sand:
                    PlaySound("RollSand");
                    break;
                case SurfaceType.Ice:
                    PlaySound("SlideIce");
                    break;
                case SurfaceType.Water:
                    PlaySound("WaterSplash");
                    break;
                case SurfaceType.SpeedBoost:
                    PlaySound("SpeedBoost");
                    break;
            }
        }
        
        public void PlayCrowdReaction(string reactionType)
        {
            switch (reactionType)
            {
                case "HoleInOne":
                    PlaySound("CrowdCheer");
                    break;
                case "Eagle":
                    PlaySound("CrowdApplause");
                    break;
                case "NearMiss":
                    PlaySound("CrowdOoh");
                    break;
            }
        }
        
        #endregion
        
        #region Ambient Sounds
        
        public void SetAmbientSound(string ambientName)
        {
            if (!soundEffects.ContainsKey(ambientName)) return;
            
            ambientSource.clip = soundEffects[ambientName];
            ambientSource.loop = true;
            ambientSource.volume = sfxVolume * masterVolume * 0.3f; // Ambient is quieter
            ambientSource.Play();
        }
        
        public void StopAmbientSound()
        {
            ambientSource.Stop();
        }
        
        #endregion
        
        #region Volume Control
        
        public void SetMasterVolume(float volume)
        {
            masterVolume = Mathf.Clamp01(volume);
            UpdateAllVolumes();
            SaveAudioSettings();
        }
        
        public void SetMusicVolume(float volume)
        {
            musicVolume = Mathf.Clamp01(volume);
            musicSource.volume = musicVolume * masterVolume;
            SaveAudioSettings();
        }
        
        public void SetSFXVolume(float volume)
        {
            sfxVolume = Mathf.Clamp01(volume);
            sfxSource.volume = sfxVolume * masterVolume;
            uiSource.volume = sfxVolume * masterVolume;
            SaveAudioSettings();
        }
        
        private void UpdateAllVolumes()
        {
            musicSource.volume = musicVolume * masterVolume;
            sfxSource.volume = sfxVolume * masterVolume;
            uiSource.volume = sfxVolume * masterVolume;
            ambientSource.volume = sfxVolume * masterVolume * 0.3f;
        }
        
        #endregion
        
        #region Save/Load Settings
        
        private void SaveAudioSettings()
        {
            PlayerPrefs.SetFloat("MasterVolume", masterVolume);
            PlayerPrefs.SetFloat("MusicVolume", musicVolume);
            PlayerPrefs.SetFloat("SFXVolume", sfxVolume);
            PlayerPrefs.Save();
        }
        
        private void LoadAudioSettings()
        {
            masterVolume = PlayerPrefs.GetFloat("MasterVolume", 1f);
            musicVolume = PlayerPrefs.GetFloat("MusicVolume", 0.7f);
            sfxVolume = PlayerPrefs.GetFloat("SFXVolume", 1f);
            
            UpdateAllVolumes();
        }
        
        #endregion
        
        #region Utility
        
        public float GetMasterVolume() => masterVolume;
        public float GetMusicVolume() => musicVolume;
        public float GetSFXVolume() => sfxVolume;
        
        public bool IsMusicPlaying() => musicSource.isPlaying;
        public string GetCurrentMusicName() => musicSource.clip != null ? musicSource.clip.name : "";
        
        public void ToggleMute()
        {
            AudioListener.pause = !AudioListener.pause;
        }
        
        #endregion
    }
    
    [System.Serializable]
    public class AudioDatabase : ScriptableObject
    {
        [System.Serializable]
        public class SoundEffect
        {
            public string name;
            public AudioClip clip;
            [Range(0f, 1f)] public float defaultVolume = 1f;
        }
        
        public List<AudioClip> musicTracks = new List<AudioClip>();
        public List<SoundEffect> soundEffects = new List<SoundEffect>();
    }
}