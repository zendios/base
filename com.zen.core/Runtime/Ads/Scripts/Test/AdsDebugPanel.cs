using Base;
using Base.Ads;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Debug Mode ad tools: a few ButtonTestAd buttons and one DropdownAdFlow added to the AdDebugButtons column.
///  Show Inter / Show Reward / Show AppOpen - show that format now (the frequency clock is not checked).
///  Test IDs: ON/OFF                        - AdConfig.useIdTest.
///  Log Ads                                 - writes the whole ad state to logcat (clock, AdConfig, counters, placements).
///  Flow dropdown                           - Native / AdMob / Both for inter, reward, app open and banner at once.
///  Test All Ads                            - opens / closes the full test panel (AdsTestAllPanel).
/// </summary>
public class AdsDebugPanel : MonoBehaviour
{
    [Tooltip("Base/Ads/Prefabs/ButtonTestAd.prefab")]
    [SerializeField] private GameObject buttonPrefab;
    [Tooltip("Base/Ads/Prefabs/DropdownAdFlow.prefab")]
    [SerializeField] private GameObject dropdownPrefab;

    private const string Tag = "[AdsDebug] ";
    private static AdConfig Config => AdConfig.Instance;

    private void Start()
    {
        AddFlowDropdown();
        AddButton(() => "Test All Ads", ToggleTestAll);
    }

    private void ToggleTestAll()
    {
        var panel = GetComponent<AdsTestAllPanel>();
        if (panel == null)
        {
            panel = gameObject.AddComponent<AdsTestAllPanel>();
            panel.Init(buttonPrefab, dropdownPrefab);
        }
        panel.TogglePanel();
    }

    private static void Show(PlacementType placement)
    {
        AdsManager.LoadShow(placement, Result(placement.ToString()), "debug", ShowLoadingMode.Toast);
    }

    private static Action<AdType, AdState> Result(string name) => (type, state) => Debug.Log($"{Tag}{name}: {state}");

    private void AddButton(Func<string> label, Action onClick)
    {
        var go = Instantiate(buttonPrefab, transform, false);
        var text = go.GetComponentInChildren<TMP_Text>(true);
        text.text = label();
        var button = go.GetComponent<Button>();
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() =>
        {
            onClick();
            text.text = label();
        });
    }

    /// <summary>One flow for every format: option index = AdFlow value (OnlyNative, OnlyDefault, Both).</summary>
    private void AddFlowDropdown()
    {
        var dropdown = Instantiate(dropdownPrefab, transform, false).GetComponent<TMP_Dropdown>();
        dropdown.onValueChanged.RemoveAllListeners();
        dropdown.ClearOptions();
        dropdown.AddOptions(new List<string> { "Flow: Native", "Flow: AdMob", "Flow: Both" });
        dropdown.SetValueWithoutNotify((int)Config.adInterFlow);
        dropdown.onValueChanged.AddListener(i =>
        {
            var flow = (AdFlow)i;
            Config.adInterFlow = Config.adRewardFlow = Config.adAppOpenFlow = Config.adBannerFlow = flow;
            Debug.Log($"{Tag}flow = {flow} (banner: next launch)");
        });
    }

    private static void LogAds()
    {
        var c = Config;
        var u = DataManager.UserData;
        var sb = new StringBuilder("ADS STATE\n");
        sb.AppendLine($"SDK admob={AdsManager.IsInitialized} native={AdZativeSDK.IsInitialized} ump={UMP.CanRequestAds} testIds={AdsManager.UseIdTest}");
        sb.AppendLine($"removedAds vip={u.isVIP} removed={u.isRemovedAds} inReview={DataManager.InReview}");
        sb.AppendLine($"clock inter in {AdsManager.SecondsUntilAdAllowed:0}s, gap {AdsManager.TimePlayToShowAds:0}s, plays {u.totalPlay}/{c.adInterOnPlay}, inAppReview blocks={InAppReview.Instance != null && InAppReview.Instance.isTimeToShow}");
        sb.AppendLine($"flows inter={c.adInterFlow} reward={c.adRewardFlow} appOpen={c.adAppOpenFlow} banner={c.adBannerFlow}");
        sb.AppendLine($"config timePlayToShow={c.adTimePlayToShow} timePlayReduceToShow={c.adTimePlayReduceToShow} timeBetween={c.adTimeBetween} loadTimeout={c.adLoadTimeout} interOnPlay={c.adInterOnPlay} interVsRewardRatio={c.adInterVsRewardRatio} bannerReload={c.adBannerReload} interOnStart={c.adInterOnStart} interOnComplete={c.adInterOnComplete} interOnFTUE={c.adInterOnFTUE} mrecOnFTUE={c.adMrecOnFTUE}");
        sb.AppendLine($"counters inter={u.adInterstitial} reward={u.adRewarded} rewardSkipped={u.adRewardSkipped} total={u.adTotal} forceRewardNext={AdsManager.ForceReward}");
        foreach (var ad in AdsManager.Placements.Values.Concat(FindObjectsOfType<AdBase>(true)).Where(a => a != null).Distinct().OrderBy(a => a.placement.ToString()))
        {
            string timing = ad is AdZative z ? $" timing={JsonUtility.ToJson(z.Timing)} pod={z.PodSize}" : "";
            sb.AppendLine($"  {ad.placement}: {ad.state} ready={ad.isCanShow} source={ad.SourceName} error={ad.LastError}{timing}");
        }
        sb.AppendLine($"plugin {AdZativeSDK.DumpState()}");
        Debug.Log(Tag + sb);
        AdToast.ShowNotice("Ads state written to logcat");
    }
}
