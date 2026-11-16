using System.Collections;
using UnityEngine;

namespace Base
{
    /// <summary>
    /// Background music: MusicManager.Play(clip) fades from the track that plays to the new one (two looping
    /// AudioSources, real time, so it also fades during a pause). Follows GameSettings.MusicOn / MusicVolume at once.
    /// A fullscreen ad mutes it with the rest (AdsManager pauses AudioListener). Prefab AudioManager in the Splash
    /// (DontDestroyOnLoad); without it the calls do nothing.
    ///   MusicManager.Play(homeMusic);   MusicManager.Stop();
    /// </summary>
    public class MusicManager : MonoBehaviour
    {
        [Tooltip("Seconds of the fade between two tracks.")]
        [SerializeField, Min(0f)] private float fadeSeconds = 0.5f;
        [Tooltip("Played when the game starts. Empty = silence until Play.")]
        [SerializeField] private AudioClip startMusic;

        private static MusicManager instance;
        private AudioSource current;
        private AudioSource other;
        private Coroutine fade;

        /// <summary>A MusicManager is running.</summary>
        public static bool HasInstance => instance != null;

        private static float Volume => GameSettings.MusicOn ? GameSettings.MusicVolume : 0f;

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }
            instance = this;
            DontDestroyOnLoad(gameObject);
            current = NewSource();
            other = NewSource();
        }

        private void OnEnable()
        {
            GameSettings.OnMusicChanged += OnMusicChanged;
            GameSettings.OnMusicVolumeChanged += OnMusicVolumeChanged;
        }

        private void OnDisable()
        {
            GameSettings.OnMusicChanged -= OnMusicChanged;
            GameSettings.OnMusicVolumeChanged -= OnMusicVolumeChanged;
        }

        private void Start()
        {
            if (instance == this && startMusic != null)
                Play(startMusic);
        }

        private void OnDestroy()
        {
            if (instance == this)
                instance = null;
        }

        /// <summary>Fades to this track (nothing when it already plays).</summary>
        public static void Play(AudioClip clip)
        {
            if (instance == null || clip == null || (instance.current.clip == clip && instance.current.isPlaying))
                return;
            (instance.current, instance.other) = (instance.other, instance.current);
            instance.current.clip = clip;
            instance.current.volume = 0f;
            instance.current.Play();
            instance.StartFade();
        }

        /// <summary>Fades the music out.</summary>
        public static void Stop()
        {
            if (instance == null)
                return;
            (instance.current, instance.other) = (instance.other, instance.current);
            instance.current.Stop();
            instance.current.clip = null;
            instance.StartFade();
        }

        private AudioSource NewSource()
        {
            var source = gameObject.AddComponent<AudioSource>();
            source.loop = true;
            source.playOnAwake = false;
            source.volume = 0f;
            return source;
        }

        private void StartFade()
        {
            if (fade != null)
                StopCoroutine(fade);
            fade = StartCoroutine(Fade());
        }

        // current up to the music volume, other down to silence, then stopped.
        private IEnumerator Fade()
        {
            float start = other.volume;
            for (float t = 0f; t < fadeSeconds; t += Time.unscaledDeltaTime)
            {
                float k = t / fadeSeconds;
                current.volume = Volume * k;
                other.volume = start * (1f - k);
                yield return null;
            }
            current.volume = current.clip != null ? Volume : 0f;
            other.volume = 0f;
            other.Stop();
            fade = null;
        }

        private void OnMusicChanged(bool on) => OnMusicVolumeChanged(GameSettings.MusicVolume);

        private void OnMusicVolumeChanged(float volume)
        {
            if (fade == null && current.clip != null)
                current.volume = Volume;
        }
    }
}
