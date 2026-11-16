using UnityEngine;

/// <summary>
/// Sample of 3D sounds: one random clip of audioClips played by a pooled AudioSource (SFXPoolReturn) at a transform or
/// a position, e.g. an explosion in the world. Same settings as SoundManager (GameSettings.SoundOn / SoundVolume);
/// SoundManager plays the 2D sounds (UI, game events).
/// </summary>
public class SFXSampleManager : PoolerBase<SFXPoolReturn>
{
    [SerializeField] private SFXPoolReturn explosionPrefab;

    private static SFXSampleManager instance;

    [SerializeField] AudioClip[] audioClips;

    private void Awake()
    {
        instance = this;
    }

    private void Start()
    {
        instance.InitPool(instance.explosionPrefab, instance.defaultCapicity, instance.maxCapicity, true);
    }

    public void ReturnPoll(SFXPoolReturn exp)
    {
        instance.Release(exp);
    }

    /// <summary>Plays a random clip at this transform (it follows it while playing).</summary>
    public static void Set(Transform parrent)
    {
        var pool = instance.Get();
        pool.Init(instance.ReturnPoll, instance.transform, RandomClip());
        pool.transform.SetParent(parrent);
        pool.transform.localPosition = Vector3.zero;
    }

    /// <summary>Plays a random clip at this world position.</summary>
    public static void Set(Vector3 position)
    {
        var pool = instance.Get();
        pool.Init(instance.ReturnPoll, instance.transform, RandomClip());
        pool.transform.position = position;
    }

    private static AudioClip RandomClip() =>
        instance.audioClips.Length > 0 ? instance.audioClips[Random.Range(0, instance.audioClips.Length)] : null;
}
