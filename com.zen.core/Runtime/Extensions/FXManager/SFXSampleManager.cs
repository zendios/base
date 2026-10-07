using UnityEngine;

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

    public static void Set(Transform parrent)
    {
        var pool = instance.Get();
        var clip = instance.audioClips[Random.Range(0, instance.audioClips.Length)];
        pool.Init(instance.ReturnPoll, instance.transform, clip);
        pool.transform.SetParent(parrent);
        pool.transform.localPosition = Vector3.zero;
    }

    public static void Set(Vector3 position)
    {
        var pool = instance.Get();
        var clip = instance.audioClips[Random.Range(0, instance.audioClips.Length)];
        pool.Init(instance.ReturnPoll, instance.transform, clip);
        pool.Init(instance.ReturnPoll, null);
        pool.transform.localPosition = position;
    }
}
