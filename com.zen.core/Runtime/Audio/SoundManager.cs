using UnityEngine;

namespace Base
{
    /// <summary>
    /// Sound effects of the game: SoundManager.Play(clip), SoundManager.PlayClick(). Plays only while
    /// GameSettings.SoundOn, at GameSettings.SoundVolume. A few AudioSources made once, reused in turn (no object per
    /// sound). Prefab AudioManager in the Splash (DontDestroyOnLoad); without it the calls do nothing.
    ///   SoundManager.Play(coinClip);   SoundManager.Play(hitClip, 0.8f, Random.Range(0.9f, 1.1f));
    /// 2D sounds; a sound placed in the world (3D, pooled AudioSources) is SFXSampleManager, under the same settings.
    /// </summary>
    public class SoundManager : MonoBehaviour
    {
        [Tooltip("Sounds that can overlap.")]
        [SerializeField, Range(1, 16)] private int voices = 6;
        [Tooltip("Played by PlayClick (buttons). Empty = none.")]
        [SerializeField] private AudioClip click;

        private static SoundManager instance;
        private AudioSource[] sources;
        private int next;

        /// <summary>A SoundManager is running.</summary>
        public static bool HasInstance => instance != null;

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }
            instance = this;
            DontDestroyOnLoad(gameObject);
            sources = new AudioSource[voices];
            for (int i = 0; i < voices; i++)
            {
                sources[i] = gameObject.AddComponent<AudioSource>();
                sources[i].playOnAwake = false;
            }
        }

        private void OnDestroy()
        {
            if (instance == this)
                instance = null;
        }

        /// <summary>Plays a clip once (volume and pitch of this sound, times GameSettings.SoundVolume).</summary>
        public static void Play(AudioClip clip, float volume = 1f, float pitch = 1f)
        {
            if (instance == null || clip == null || !GameSettings.SoundOn)
                return;
            var source = instance.sources[instance.next];
            instance.next = (instance.next + 1) % instance.sources.Length;
            source.pitch = pitch;
            source.PlayOneShot(clip, volume * GameSettings.SoundVolume);
        }

        /// <summary>The button click sound (the click clip of the prefab).</summary>
        public static void PlayClick()
        {
            if (instance != null)
                Play(instance.click);
        }
    }
}
