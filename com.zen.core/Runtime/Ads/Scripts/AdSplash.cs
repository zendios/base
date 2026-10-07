using Base;
using Base.Ads;
using System.Collections;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Debug = UnityEngine.Debug;

public class AdSplash : MonoBehaviour
{
    public static bool IsShowing = true; [SerializeField, Tooltip("Make sure scene add in build settings")]
    protected int loadSceneMainIndex = 1;
    [SerializeField]
    protected LoadSceneMode loadSceneMainMode = LoadSceneMode.Single;

    [SerializeField]
    protected int loadSceneGameIndex = 0;
    [SerializeField]
    protected LoadSceneMode loadSceneGameMode = LoadSceneMode.Additive;

    protected AdConfig adConfig;

    // Track parallel scene loading operations
    private AsyncOperation mainSceneLoadOp;
    private AsyncOperation gameSceneLoadOp;

    // Cache PlayerPrefs to avoid Disk I/O bottleneck during startup
    private int cachedInterstitialShowCount = -1;

    protected int showInterstitial
    {
        get
        {
            if (AdConfig.Instance != null)
                adConfig = AdConfig.Instance;
            return adConfig != null ? adConfig.adInterOnFTUE : 0;
        }
    }

    public bool isShowInterstitial
    {
        get
        {
            if (cachedInterstitialShowCount == -1)
            {
                cachedInterstitialShowCount = PlayerPrefs.GetInt("isShowInterstitial", 0);
            }
            return cachedInterstitialShowCount < showInterstitial;
        }
        set
        {
            // Note: Preserving the original logic where assigning ANY value increments the counter.
            // Cached in RAM to prevent multiple Disk writes during critical initialization.
            if (cachedInterstitialShowCount == -1)
            {
                cachedInterstitialShowCount = PlayerPrefs.GetInt("isShowInterstitial", 0);
            }

            cachedInterstitialShowCount++;
            PlayerPrefs.SetInt("isShowInterstitial", cachedInterstitialShowCount);
        }
    }

    private void Start()
    {
        IsShowing = true;
        AudioListener.volume = 0;

        StartCoroutine(DOLoad());
    }

    private IEnumerator DOLoad()
    {
        Log("Checking App Tracking Transparency...");
        AdToast.ShowLoading("Checking App Tracking Transparency...", 60f);
        yield return new WaitForSeconds(0.25f);
        yield return ATTHelper.IECheck();

        Log("Data manager initialize....");
        AdToast.SetUpdate("Data manager initialize....", 0.10f, 10f);
        yield return new WaitForEndOfFrame();
        yield return DataManager.IELoad();

        Log("Checking User Messaging Platform...");
        AdToast.SetUpdate("Checking UMP...", 0.20f, 10f);
        yield return new WaitForEndOfFrame();
        yield return UMP.Instance.IECheck();

        Log("Checking AppsFlyer...");
        AdToast.SetUpdate("Checking AppsFlyer...", 0.25f, 5f);
        yield return new WaitForEndOfFrame();
        yield return AppsFlyerHelper.IEInit();

        Log("Firebase initialize...");
        AdToast.SetUpdate("Firebase Manager initialize......", 0.30f, 5f);
        yield return new WaitForEndOfFrame();
        yield return ZenRemote.Init();

        Log("Getting remote config...");
        AdToast.SetUpdate("Firebase manager getting remote config...", 0.35f, 5f);
        yield return new WaitForEndOfFrame();
        yield return ZenRemote.Fetch();

        var userData = DataManager.UserData;
        var gameConfig = DataManager.GameConfig;

        if (gameConfig != null && !string.IsNullOrEmpty(gameConfig.FTUE_LevelVariant))
            userData.abTesting = gameConfig.FTUE_LevelVariant;
        else
            userData.abTesting = "Control";

        Log($"FTUE_LevelVariant: {(gameConfig != null ? gameConfig.FTUE_LevelVariant : "Null")} A/B testing: {userData.abTesting}");

        if ((gameConfig != null && gameConfig.inAppUpdate) || DataManager.IsForceUpdate)
        {
            AdToast.SetUpdate("Checking new version...", 0.40f, 10f);
            yield return new WaitForEndOfFrame();
            bool isAvailable = false;
            yield return InAppUpdate.Instance.CheckForUpdate((available) => { isAvailable = available; });
            if (isAvailable)
                yield return InAppUpdate.Instance.DOShow(null, DataManager.IsForceUpdate);
        }

        AdToast.SetUpdate("Ads manager init...", 0.50f, 10f);
        yield return new WaitForEndOfFrame();
        if (UMP.CanRequestAds)
            yield return AdsManager.IEInit();

        if (!AdsManager.IsRemovedAds && isShowInterstitial)
        {
            yield return AdsManager.IEInit();

            if (adConfig.adInterFlow == AdFlow.OnlyDefault || adConfig.adInterFlow == AdFlow.Both)
            {
                yield return AdsManager.IELoad(PlacementType.AdmobInterOpen, (type, state) =>
                {
                    if (state == AdState.LoadRequest)
                        AdToast.SetUpdate("Checking open data...", 0.60f, 10f);
                }, "ftue", ShowLoadingMode.None);
            }
        }

        AdToast.SetUpdate("Loading scene main...", 0.80f, 10f);
        yield return new WaitForEndOfFrame();
        yield return IELoadGameScene();

        AdToast.SetUpdate("Active scene game...", 0.90f, 10f);
        yield return new WaitForEndOfFrame();
        yield return IEActiveScene();

        while (AudioListener.volume < 1f)
        {
            AudioListener.volume += 0.5f * Time.deltaTime;
            yield return null;
        }

        if (!AdsManager.IsRemovedAds && isShowInterstitial)
        {
            if (adConfig.adInterFlow == AdFlow.OnlyDefault || adConfig.adInterFlow == AdFlow.Both)
            {
                bool isAvaiable = AdsManager.CheckAvailable(PlacementType.AdmobInterOpen, "ftue");
                if (isAvaiable)
                {
                    yield return AdsManager.IEShow(PlacementType.AdmobInterOpen, (type, state) =>
                    {
                        if (state == AdState.ShowSuccess)
                            isShowInterstitial = false;
                    }, "ftue", 0f);
                }
                else if (adConfig.adInterFlow == AdFlow.Both)
                {
                    yield return AdsManager.IELoadShow(PlacementType.AdZativeInterOpen, (type, state) =>
                    {
                        if (state == AdState.ShowSuccess)
                            isShowInterstitial = false;
                    }, "ftue", ShowLoadingMode.None);
                }
            }
            else if (adConfig.adInterFlow == AdFlow.OnlyNative)
            {
                yield return AdsManager.IELoadShow(PlacementType.AdZativeInterOpen, (type, state) =>
                {
                    if (state == AdState.ShowSuccess)
                        isShowInterstitial = false;
                }, "ftue", ShowLoadingMode.None);
            }
        }

        yield return new WaitForEndOfFrame();

        AdsManager.LoadAds();

        AdToast.HideLoading();
        IsShowing = false;
    }

    protected IEnumerator IELoadGameScene()
    {
        // We only start loading if the scene isn't already loaded (or loading)
        Scene mainScene = SceneManager.GetSceneByBuildIndex(loadSceneMainIndex);
        if (!mainScene.IsValid() || !mainScene.isLoaded)
        {
            ZenAnalytics.LogEvent("load_scene_main");
            mainSceneLoadOp = SceneManager.LoadSceneAsync(loadSceneMainIndex, loadSceneMainMode);
            // Yielding the operation directly is the Unity-standard way to wait for it.
            if (mainSceneLoadOp != null) yield return mainSceneLoadOp;
        }

        if (loadSceneGameIndex > 1 && loadSceneGameIndex != loadSceneMainIndex)
        {
            Scene gameScene = SceneManager.GetSceneByBuildIndex(loadSceneGameIndex);
            if (!gameScene.IsValid() || !gameScene.isLoaded)
            {
                ZenAnalytics.LogEvent("load_scene_game");
                gameSceneLoadOp = SceneManager.LoadSceneAsync(loadSceneGameIndex, loadSceneGameMode);
                if (gameSceneLoadOp != null) yield return gameSceneLoadOp;
            }
        }
        yield return new WaitForEndOfFrame();
    }

    protected IEnumerator IEActiveScene()
    {
        // Defensive check: Ensure parallel loading coroutine is actually finished.
        // If main SDKs loaded faster than scenes, we must wait here.
        while (mainSceneLoadOp != null && !mainSceneLoadOp.isDone)
        {
            yield return null;
        }

        while (gameSceneLoadOp != null && !gameSceneLoadOp.isDone)
        {
            yield return null;
        }

        ZenAnalytics.LogEvent("active_scene_game");

        if (loadSceneGameIndex > 0)
        {
            Scene checkActiveScene;
            if (loadSceneGameIndex > 1 && loadSceneGameIndex != loadSceneMainIndex)
            {
                checkActiveScene = SceneManager.GetSceneByBuildIndex(loadSceneGameIndex);
            }
            else
            {
                checkActiveScene = SceneManager.GetSceneByBuildIndex(loadSceneMainIndex);
            }

            yield return null;

            if (checkActiveScene.IsValid() && checkActiveScene.isLoaded)
            {
                SceneManager.SetActiveScene(checkActiveScene);
                Log($"Active scene set to: {checkActiveScene.name}");
            }
            else
            {
                LogError($"Failed to set active scene. Index {loadSceneGameIndex} is invalid or not loaded.");
            }
        }
        yield return new WaitForEndOfFrame();
    }

    public void Log(string value)
    {
        if (DebugMode.IsOn)
            Debug.Log(value);
    }

    public void LogWarning(string value)
    {
        if (DebugMode.IsOn)
            Debug.LogWarning(value);
    }

    public void LogError(string value)
    {
        if (DebugMode.IsOn)
            Debug.LogError(value);
    }
}