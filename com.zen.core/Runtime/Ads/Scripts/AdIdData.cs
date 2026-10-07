using System.Collections.Generic;
using System;
using UnityEngine;
using System.Linq;
using Base.Ads;

[CreateAssetMenu(fileName = "AdPlacement", menuName = "DataAsset/AdIdData")]
[Serializable]
public class AdIdData : ScriptableObject
{
    public AdType type = AdType.Native;
    public string id => list.FirstOrDefault();
    public List<string> list
    {
        get
        {
            if (Application.platform == RuntimePlatform.IPhonePlayer || Application.platform == RuntimePlatform.OSXPlayer)
                return IOS;
            return ANDROID;
        }
    }
    public List<string> ANDROID;
    public List<string> IOS;

    private void OnValidate()
    {
        for (int i = 0; i < ANDROID.Count; i++)
        {
            if (!string.IsNullOrEmpty(ANDROID[i]))
                ANDROID[i] = ANDROID[i].Trim();
        }

        for (int i = 0; i < IOS.Count; i++)
        {
            if (!string.IsNullOrEmpty(IOS[i]))
                IOS[i] = IOS[i].Trim();
        }
    }
}