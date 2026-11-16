using Base;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Home of the UI sample: Play starts a game (GameStateManager.Load: GameFlow loads it, the game screen counts down);
/// Daily Login (opens by itself when a reward is waiting, or the popup a tapped reminder asks for), Missions,
/// Achievements, Settings, Shop, and "Quit?" on the back key at the root. Anchors + SafeArea, so one prefab fits
/// portrait and landscape.
/// </summary>
public class UIScreenMain : UIScreen
{
    [SerializeField] private Button playButton;
    [SerializeField] private Button dailyLoginButton;
    [Tooltip("Shown on the Daily Login button while a reward is waiting.")]
    [SerializeField] private GameObject dailyLoginDot;
    [SerializeField] private Button missionsButton;
    [Tooltip("Shown on the Missions button while a reward can be claimed.")]
    [SerializeField] private GameObject missionsDot;
    [SerializeField] private Button achievementsButton;
    [Tooltip("Shown on the Achievements button while a reward can be claimed.")]
    [SerializeField] private GameObject achievementsDot;
    [SerializeField] private Button settingsButton;
    [SerializeField] private Button shopButton;

    private static bool dailyLoginShown;   // once per session

    protected override void Awake()
    {
        base.Awake();
        playButton.onClick.AddListener(() => GameStateManager.Load());
        dailyLoginButton.onClick.AddListener(() => UIPopupDailyLogin.Show());
        missionsButton.onClick.AddListener(() => UIPopupDailyMission.Show());
        achievementsButton.onClick.AddListener(() => UIPopupAchievements.Show());
        settingsButton.onClick.AddListener(() => UIPopupSettings.Show());
        shopButton.onClick.AddListener(() => CurrencyManager.OpenShop?.Invoke());
    }

    private void OnEnable()
    {
        UIManager.OnBackAtRoot += AskQuit;
        DailyLogin.OnClaimed += OnDailyLoginClaimed;
        DailyMissions.OnChanged += RefreshMissionsDot;
        Achievements.OnChanged += RefreshAchievementsDot;
        NotificationManager.OnChanged += OpenRoute;   // a reminder tapped while the game was in the background
    }

    private void OnDisable()
    {
        UIManager.OnBackAtRoot -= AskQuit;
        DailyLogin.OnClaimed -= OnDailyLoginClaimed;
        DailyMissions.OnChanged -= RefreshMissionsDot;
        Achievements.OnChanged -= RefreshAchievementsDot;
        NotificationManager.OnChanged -= OpenRoute;
    }

    protected override void OnShow()
    {
        shopButton.gameObject.SetActive(CurrencyManager.OpenShop != null);   // no IAP in the game: no shop
        dailyLoginButton.gameObject.SetActive(DailyLogin.IsAvailable);   // no DailyLoginDatas: no calendar
        missionsButton.gameObject.SetActive(DailyMissions.IsAvailable);
        achievementsButton.gameObject.SetActive(Achievements.IsAvailable);
        RefreshDailyLoginDot();
        RefreshMissionsDot();
        RefreshAchievementsDot();
    }

    protected override void OnShown()
    {
        OpenRoute();
        if (dailyLoginShown || !DailyLogin.IsAvailable || !DailyLogin.Datas.showOnHome || !DailyLogin.CanClaim)
            return;
        dailyLoginShown = true;
        UIPopupDailyLogin.Show();
    }

    // A tapped reminder opens what it promised, never over a race (the home shows again after it); nothing left to
    // claim there is fine (the popup shows the state).
    private void OpenRoute()
    {
        if (GameStateManager.CurrentState == GameState.Play)
            return;
        switch (NotificationManager.TakeRoute())
        {
            case NotificationPlanner.RouteDailyLogin:
                dailyLoginShown = true;
                if (DailyLogin.IsAvailable)
                    UIPopupDailyLogin.Show();
                break;
            case NotificationPlanner.RouteMissions:
                if (DailyMissions.IsAvailable)
                    UIPopupDailyMission.Show();
                break;
            default:
                break;
        }
    }

    private void OnDailyLoginClaimed(int day, DailyLoginData data, bool doubled) => RefreshDailyLoginDot();

    private void RefreshDailyLoginDot() => dailyLoginDot.SetActive(DailyLogin.CanClaim);

    private void RefreshMissionsDot() => missionsDot.SetActive(DailyMissions.HasClaimable);

    private void RefreshAchievementsDot() => achievementsDot.SetActive(Achievements.HasClaimable);

    private static void AskQuit() =>
        UIManager.Message("Quit?", "Your progress is saved.", onConfirm: Application.Quit, confirmLabel: "Quit", cancelLabel: "Stay");
}
