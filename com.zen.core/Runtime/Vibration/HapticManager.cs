using UnityEngine;

/// <summary>
/// Vibration of the game: HapticManager.PlayImpact(style), silent while vibration is off. The on / off setting is
/// GameSettings.VibrationOn (stored here, PlayerPrefs key "HapticFeedback", kept from older versions). Android and iOS
/// play it through AndroidHapticController / iOSHapticController; elsewhere it does nothing.
/// </summary>
public static class HapticManager
{
    private const string PrefsKey = "HapticFeedback";
    private static int isOn = -1;

    public enum ImpactStyle { Light = 0, Medium = 1, Heavy = 2, Rigid = 3, Soft = 4 }

    /// <summary>1 = vibration on, 0 = off (GameSettings.VibrationOn reads and writes it).</summary>
    public static int IsOn
    {
        get
        {
            if (isOn == -1)
                isOn = PlayerPrefs.GetInt(PrefsKey, 1);
            return isOn;
        }
        set
        {
            if (value == isOn)
                return;
            isOn = value;
            PlayerPrefs.SetInt(PrefsKey, isOn);
            PlayerPrefs.Save();
        }
    }

    /// <summary>One vibration, only while it is on.</summary>
    public static void PlayImpact(ImpactStyle style)
    {
        if (IsOn <= 0)
            return;
#if UNITY_IOS
        iOSHapticController.PlayImpact(style);
#elif UNITY_ANDROID
        AndroidHapticController.PlayImpact(style);
#endif
    }
}
