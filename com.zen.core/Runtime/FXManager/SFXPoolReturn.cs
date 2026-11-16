using System;
using Base;
using UnityEngine;

/// <summary>
/// A pooled AudioSource of SFXSampleManager: plays one clip where it is put (3D sound), then goes back to the pool.
/// Same rule as SoundManager: silent while GameSettings.SoundOn is off, at GameSettings.SoundVolume.
/// </summary>
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
        audioClip = clip != null ? clip : audioSource.clip;
        float seconds = 0f;
        if (audioClip != null && GameSettings.SoundOn)
        {
            audioSource.pitch = UnityEngine.Random.Range(0.8f, 1.2f);
            audioSource.PlayOneShot(audioClip, GameSettings.SoundVolume);
            seconds = audioClip.length / audioSource.pitch;   // a higher pitch plays it faster
        }
        Invoke(nameof(Release), seconds);   // back to the pool once played (next frame when nothing plays)
    }
}
