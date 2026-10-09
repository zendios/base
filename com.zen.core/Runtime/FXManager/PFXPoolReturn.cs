using System;
using UnityEngine;

[RequireComponent(typeof(ParticleSystem))]
public class PFXPoolReturn : PoolReturnBase<PFXPoolReturn>
{
    [SerializeField] protected ParticleSystem particle;
    private ParticleSystem.MainModule main;

    public override void Init(Action<PFXPoolReturn> releaseAction, Transform parent, float timeAlive = 0)
    {
        base.Init(releaseAction, parent, timeAlive);
        if (timeAlive > 0)
            main.duration = timeAlive;
    }

    private void OnValidate()
    {
        particle = GetComponent<ParticleSystem>();
        main = particle.main;
        main.playOnAwake = true;
    }

    public void OnParticleSystemStopped()
    {
        Release();
    }
}
