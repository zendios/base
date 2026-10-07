using System;
using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class SFXPoolReturn : PoolReturnBase<SFXPoolReturn>
{
    [SerializeField] protected AudioSource audioSource;
    [SerializeField] protected AudioClip audioClip;

    private void OnValidate()
    {
        audioSource = GetComponent<AudioSource>();
    }

    public void Init(Action<SFXPoolReturn> releaseAction, Transform parent, AudioClip clip)
    {
        base.Init(releaseAction, parent, 0);
        if (clip == null)
            audioClip = audioSource.clip;
        else
            audioClip = clip;

        if (audioClip)
        {
            audioSource.pitch = UnityEngine.Random.Range(0.8f, 1.2f);
            audioSource.PlayOneShot(audioClip);
            Invoke(nameof(Release), audioClip.length * audioSource.pitch);
        }
    }
}
