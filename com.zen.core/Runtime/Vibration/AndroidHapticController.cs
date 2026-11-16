using UnityEngine;

// Internal class, only accessible within the assembly.
// The public API is HapticManager.
internal static class AndroidHapticController
{
    // --- Pre-cached Waveform Arrays for Zero GC Allocation ---
    private static readonly long[] RIGID_TIMINGS = { 0, 20 };
    private static readonly int[] RIGID_AMPLITUDES = { 0, 255 };
    private static readonly long[] SOFT_TIMINGS = { 0, 15, 5, 20 };
    private static readonly int[] SOFT_AMPLITUDES = { 0, 128, 0, 180 };

    private static AndroidJavaClass _vibrationEffectClass;
    private static AndroidJavaObject _vibrator;
    private static int _sdkInt = -1;
    private static bool _hasVibrator = false;

    static AndroidHapticController()
    {
        if (Application.platform != RuntimePlatform.Android) return;

        try
        {
            _sdkInt = new AndroidJavaClass("android.os.Build$VERSION").GetStatic<int>("SDK_INT");

            using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (var currentActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
            {
                _vibrator = currentActivity.Call<AndroidJavaObject>("getSystemService", "vibrator");
                if (_vibrator != null)
                {
                    _hasVibrator = _vibrator.Call<bool>("hasVibrator");
                }
            }

            if (_sdkInt >= 26)
            {
                _vibrationEffectClass = new AndroidJavaClass("android.os.VibrationEffect");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Optimus: Error initializing AndroidHapticController. Haptics will be disabled. Exception: {e}");
            _vibrator = null;
            _hasVibrator = false;
        }
    }

    public static void PlayImpact(HapticManager.ImpactStyle style)
    {
        if (!_hasVibrator) return;

        try
        {
            if (_sdkInt >= 26 && _vibrationEffectClass != null)
            {
                AndroidJavaObject effect = CreateModernEffect(style);
                if (effect != null) _vibrator.Call("vibrate", effect);
            }
            else
            {
                long duration = GetLegacyDuration(style);
                if (duration > 0) _vibrator.Call("vibrate", duration);
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Optimus: Error playing haptic feedback. Style: {style}. Exception: {e}");
        }
    }

    private static AndroidJavaObject CreateModernEffect(HapticManager.ImpactStyle style)
    {
        switch (style)
        {
            case HapticManager.ImpactStyle.Light:
                return _vibrationEffectClass.CallStatic<AndroidJavaObject>("createPredefined", _vibrationEffectClass.GetStatic<int>("EFFECT_TICK"));
            case HapticManager.ImpactStyle.Medium:
                return _vibrationEffectClass.CallStatic<AndroidJavaObject>("createPredefined", _vibrationEffectClass.GetStatic<int>("EFFECT_CLICK"));
            case HapticManager.ImpactStyle.Heavy:
                return _vibrationEffectClass.CallStatic<AndroidJavaObject>("createPredefined", _vibrationEffectClass.GetStatic<int>("EFFECT_HEAVY_CLICK"));
            case HapticManager.ImpactStyle.Rigid:
                return _vibrationEffectClass.CallStatic<AndroidJavaObject>("createWaveform", RIGID_TIMINGS, RIGID_AMPLITUDES, -1);
            case HapticManager.ImpactStyle.Soft:
                return _vibrationEffectClass.CallStatic<AndroidJavaObject>("createWaveform", SOFT_TIMINGS, SOFT_AMPLITUDES, -1);
            default:
                return null;
        }
    }

    private static long GetLegacyDuration(HapticManager.ImpactStyle style)
    {
        switch (style)
        {
            case HapticManager.ImpactStyle.Light: return 10;
            case HapticManager.ImpactStyle.Medium: return 20;
            case HapticManager.ImpactStyle.Heavy: return 30;
            case HapticManager.ImpactStyle.Rigid: return 35;
            case HapticManager.ImpactStyle.Soft: return 25;
            default: return 0;
        }
    }
}