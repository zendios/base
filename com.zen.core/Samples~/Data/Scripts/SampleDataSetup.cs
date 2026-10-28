using UnityEngine;

/// <summary>
/// Registers the sample's save sections and legacy save handler. It must run before DataManager loads, so it sits
/// in the same scene as DataManager (Awake runs before DataManager's Start). In a game, do the same from a static
/// [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)] method, so it works from any scene.
/// </summary>
public class SampleDataSetup : MonoBehaviour
{
    private static bool _registered;

    private void Awake()
    {
        if (_registered)
            return;
        _registered = true;

        DataManager.RegisterSection(SamplePlayerProgress.Current);
        DataManager.RegisterSection(new SampleVehicleSave());
        DataManager.OnLegacySaveLoaded += SampleLegacySave.Apply;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => _registered = false;
}
