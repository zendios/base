using System.Collections.Generic;
using UnityEngine;

public class SFXRandom : MonoBehaviour
{
    [SerializeField] AudioSource audioSource;
    [SerializeField] List<AudioClip> sfxList = new List<AudioClip>();
    private RandomFast _rng = new RandomFast(1234);

    public void Random()
    {
        var randomIndex = _rng.NextInt(0, sfxList.Count);
        var sfx = sfxList[randomIndex];
        audioSource.PlayOneShot(sfx);
    }
}
