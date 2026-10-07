#if UNITY_IOS
using System.Runtime.InteropServices;
#endif
using UnityEngine;

// 'internal' ensures this class is only visible within your Unity project assembly,
// promoting the use of the public 'HapticFeedback' wrapper.

internal static class iOSHapticController
{
#if UNITY_IOS
    // --- Native Function Imports ---
    // These extern methods are the bridge to the native code (either Objective-C++ or Swift).
    // "__Internal" tells the linker to find these function symbols within the final executable.

    [DllImport("__Internal")]
    private static extern void _playSelectionHaptic();

    [DllImport("__Internal")]
    private static extern void _playNotificationHaptic(int type);

    [DllImport("__Internal")]
    private static extern void _playImpactHaptic(int style);
#endif

    // --- Public API for iOS ---
    // This method is called by the cross-platform HapticFeedback class.
    // It takes the shared enum as a parameter for consistency.
    public static void PlayImpact(HapticFeedback.ImpactStyle style)
    {
        // The #if UNITY_EDITOR directive is technically redundant here because the outer
        // #if UNITY_IOS already handles it, but it's good practice for clarity and safety
        // in case this code is moved or refactored.
#if UNITY_IOS && !UNITY_EDITOR
        try
        {
            // The logic here is simple: map the shared enum to the correct native function.
            // iOS provides different generators for different semantic meanings,
            // so we call the most appropriate one.
            switch (style)
            {
                // For light UI feedback, Selection is the most appropriate.
                case HapticFeedback.ImpactStyle.Light:
                case HapticFeedback.ImpactStyle.Soft: // Soft can also be mapped to selection for a gentle tap.
                    _playSelectionHaptic();
                    break;

                // For medium, heavy, and rigid feedback, Impact is the correct choice.
                case HapticFeedback.ImpactStyle.Medium:
                    _playImpactHaptic((int)HapticFeedback.ImpactStyle.Medium); // style = 1
                    break;
                case HapticFeedback.ImpactStyle.Heavy:
                    _playImpactHaptic((int)HapticFeedback.ImpactStyle.Heavy);  // style = 2
                    break;
                case HapticFeedback.ImpactStyle.Rigid:
                    _playImpactHaptic((int)HapticFeedback.ImpactStyle.Rigid);  // style = 3
                    break;

                // Note: We are not using _playNotificationHaptic here as it's semantically
                // different (success, warning, error). If you need those, you would add
                // a separate method in the HapticFeedback wrapper.
            }
        }
        catch (System.Exception e)
        {
            // This catch block is a safeguard. A DllNotFoundException would occur if the native
            // plugin was not correctly included in the Xcode project.
            Debug.LogWarning($"Optimus: Error playing iOS haptic feedback. Style: {style}. Is the native plugin (iOSHaptics.mm/swift) included correctly? Exception: {e}");
        }
#endif
    }
}