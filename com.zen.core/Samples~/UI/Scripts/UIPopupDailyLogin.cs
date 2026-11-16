using System.Collections.Generic;
using Base;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Daily login calendar: one UIDailyLoginCell per day of DailyLoginDatas, Claim and "x2 with an ad" buttons, then
/// the reminder offer (NotificationManager) after claiming day 1, 3 or 7.
/// Prefab: Resources/UIPopupDailyLogin, a Popup_Base variant: the game copies and restyles it. The logic is
/// DailyLogin (package).
/// </summary>
public class UIPopupDailyLogin : UIPopupBase<UIPopupDailyLogin>
{
    [SerializeField] private RectTransform cellRoot;
    [Tooltip("Hidden template, cloned per day.")]
    [SerializeField] private UIDailyLoginCell cellTemplate;
    [SerializeField] private Button claimButton;
    [SerializeField] private Button doubleButton;
    [Tooltip("Shown after the claim when NotificationManager.ShouldOffer: Remind me tomorrow / Not now.")]
    [SerializeField] private GameObject remindRow;
    [SerializeField] private Button remindButton;
    [SerializeField] private Button notNowButton;

    private readonly List<UIDailyLoginCell> cells = new List<UIDailyLoginCell>();

    protected override void Awake()
    {
        base.Awake();
        cellTemplate.gameObject.SetActive(false);
        claimButton.onClick.AddListener(() => DailyLogin.Claim(claimButton.transform));
        doubleButton.onClick.AddListener(() => DailyLogin.ClaimDouble(doubleButton.transform));
        remindButton.onClick.AddListener(() => StartCoroutine(NotificationManager.IETurnOn(OnRemindAnswered)));
        notNowButton.onClick.AddListener(NotificationManager.Decline);
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        DailyLogin.OnClaimed += OnClaimed;
        NotificationManager.OnChanged += Refresh;
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        DailyLogin.OnClaimed -= OnClaimed;
        NotificationManager.OnChanged -= Refresh;
    }

    protected override void OnShow() => Refresh();

    private void OnClaimed(int day, DailyLoginData data, bool doubled) => Refresh();

    private void OnRemindAnswered(bool allowed) =>
        UIManager.Toast(allowed ? "We'll remind you tomorrow." : "Reminders are off. Turn them on anytime in Settings.");

    private void Refresh()
    {
        if (!DailyLogin.IsAvailable)
        {
            Debug.LogWarning("[UIPopupDailyLogin] no DailyLoginDatas in DataManager.itemDatas, or the feature is off.");
            Close();
            return;
        }

        var days = DailyLogin.Datas.list;
        while (cells.Count < days.Count)
        {
            var cell = Instantiate(cellTemplate, cellRoot);
            cell.gameObject.SetActive(true);
            cells.Add(cell);
        }
        for (int i = 0; i < cells.Count; i++)
        {
            bool shown = i < days.Count;
            cells[i].gameObject.SetActive(shown);
            if (shown)
                cells[i].Set(i + 1, days[i], i < DailyLogin.ClaimedCount, DailyLogin.CanClaim && i == DailyLogin.DayIndex);
        }

        claimButton.gameObject.SetActive(DailyLogin.CanClaim);
        doubleButton.gameObject.SetActive(DailyLogin.CanClaimDouble);
        remindRow.SetActive(NotificationManager.ShouldOffer);
    }
}
