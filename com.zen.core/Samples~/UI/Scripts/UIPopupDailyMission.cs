using System;
using System.Collections;
using System.Collections.Generic;
using Base;
using TMPro;
using UnityEngine;

/// <summary>
/// Daily missions: one UIDailyMissionRow per mission of today and a "Reset in hh:mm:ss" countdown to midnight.
/// Prefab: Resources/UIPopupDailyMission, a Popup_Base variant: the game copies and restyles it. The logic is
/// DailyMissions (package).
/// </summary>
public class UIPopupDailyMission : UIPopupBase<UIPopupDailyMission>
{
    [SerializeField] private RectTransform rowRoot;
    [Tooltip("Hidden template, cloned per mission.")]
    [SerializeField] private UIDailyMissionRow rowTemplate;
    [SerializeField] private TMP_Text countdownText;

    private readonly List<UIDailyMissionRow> rows = new List<UIDailyMissionRow>();
    private readonly WaitForSecondsRealtime second = new WaitForSecondsRealtime(1f);
    private Coroutine countdown;

    protected override void Awake()
    {
        base.Awake();
        rowTemplate.gameObject.SetActive(false);
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        DailyMissions.OnChanged += Refresh;
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        DailyMissions.OnChanged -= Refresh;
        StopCountdown();
    }

    protected override void OnShow()
    {
        Refresh();
        StopCountdown();
        countdown = StartCoroutine(Countdown());
    }

    private void Refresh()
    {
        if (!DailyMissions.IsAvailable)
        {
            Debug.LogWarning("[UIPopupDailyMission] no DailyMissionDatas in DataManager.itemDatas, or the feature is off.");
            Close();
            return;
        }

        var today = DailyMissions.Today;
        while (rows.Count < today.Count)
        {
            var row = Instantiate(rowTemplate, rowRoot);
            row.gameObject.SetActive(true);
            rows.Add(row);
        }
        for (int i = 0; i < rows.Count; i++)
        {
            bool shown = i < today.Count;
            rows[i].gameObject.SetActive(shown);
            if (shown)
                rows[i].Set(today[i].mission, today[i].value, today[i].claimed, DailyMissions.Claim);
        }
    }

    private void StopCountdown()
    {
        if (countdown != null)
            StopCoroutine(countdown);
        countdown = null;
    }

    private IEnumerator Countdown()
    {
        var reset = DailyMissions.NextReset;
        while (true)
        {
            countdownText.text = "Reset in " + reset.ToCountDown();
            yield return second;
            if (DateTime.Now < reset)
                continue;
            reset = DailyMissions.NextReset;   // past midnight: the next read starts the new day
            Refresh();
        }
    }
}
