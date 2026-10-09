using UnityEngine;

public class PFXSampleManager : PoolerBase<PFXPoolReturn>
{
    [SerializeField] private PFXPoolReturn explosionPrefab;
    private static PFXSampleManager instance;

    private void Awake()
    {
        instance = this;
    }

    private void Start()
    {
        instance.InitPool(instance.explosionPrefab, instance.defaultCapicity, instance.maxCapicity, true);
    }

    public void ReturnPoll(PFXPoolReturn exp)
    {
        instance.Release(exp);
    }

    public static void Set(Transform parrent)
    {
        var pool = instance.Get();
        pool.Init(instance.ReturnPoll, instance.transform);
        pool.transform.SetParent(parrent);
        pool.transform.localPosition = Vector3.zero;
    }

    public static void Set(Vector3 position)
    {
        var pool = instance.Get();
        pool.Init(instance.ReturnPoll, null);
        pool.transform.localPosition = position;
    }
}
