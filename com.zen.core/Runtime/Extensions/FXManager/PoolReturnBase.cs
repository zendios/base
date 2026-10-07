using System;
using UnityEngine;

public class PoolReturnBase<T> : MonoBehaviour where T : MonoBehaviour
{
    [SerializeField] protected Transform orgParent;

    protected Action<T> onRelease;
    public virtual void Init(Action<T> releaseAction, Transform parent, float timeAlive = 0)
    {
        onRelease = releaseAction;
        orgParent = parent;
    }

    public void Release()
    {
        if (orgParent)
            transform.SetParent(orgParent);
        onRelease?.Invoke(this as T);
    }
}
