using Base;
using Base.Ads;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Settings popup: Sound / Music volume, Vibration, graphics Quality (GameSettings), Reminders (NotificationManager),
/// Privacy (UMP), Rate (InAppReview), Restore purchases (RestorePurchasesButton, iOS), Remove Ads (RemoveAdsButton) and
/// the version (VersionInfo). A button whose part of Base is missing hides itself. Prefab: Resources/UIPopupSettings, a Popup_Base variant: the game copies and restyles it.
/// </summary>
public class UIPopupSettings : UIPopupBase<UIPopupSettings>
{
    [SerializeField] private Slider soundSlider;
    [SerializeField] private Slider musicSlider;
    [SerializeField] private Toggle vibrationToggle;
    [SerializeField] private Toggle remindersToggle;
    [SerializeField] private Button privacyButton;
    [SerializeField] private Button rateButton;
    [Tooltip("Auto, then one toggle per quality level from the lowest (QualitySettings.names).")]
    [SerializeField] private Toggle[] qualityToggles;

    protected override void Awake()
    {
        base.Awake();
        soundSlider.onValueChanged.AddListener(volume => GameSettings.SoundVolume = volume);
        musicSlider.onValueChanged.AddListener(volume => GameSettings.MusicVolume = volume);
        for (int i = 0; i < qualityToggles.Length; i++)
        {
            int quality = i - 1;   // toggle 0 = Auto (-1), then the levels
            qualityToggles[i].onValueChanged.AddListener(on =>
            {
                if (on)
                    GameSettings.Quality = quality;
            });
        }
        vibrationToggle.onValueChanged.AddListener(on =>
        {
            GameSettings.VibrationOn = on;
            if (on)
                HapticManager.PlayImpact(HapticManager.ImpactStyle.Light);
        });
        remindersToggle.onValueChanged.AddListener(OnRemindersChanged);
        privacyButton.onClick.AddListener(OpenPrivacy);
        rateButton.onClick.AddListener(() =>
        {
            if (InAppReview.Instance != null)
                InAppReview.Instance.ShowInAppReview();
        });
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        Application.focusChanged += OnFocusChanged;   // back from the OS notification settings
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        Application.focusChanged -= OnFocusChanged;
    }

    protected override void OnShow()
    {
        soundSlider.SetValueWithoutNotify(GameSettings.SoundOn ? GameSettings.SoundVolume : 0f);
        musicSlider.SetValueWithoutNotify(GameSettings.MusicOn ? GameSettings.MusicVolume : 0f);
        for (int i = 0; i < qualityToggles.Length; i++)
        {
            qualityToggles[i].SetIsOnWithoutNotify(i - 1 == GameSettings.Quality);
            qualityToggles[i].gameObject.SetActive(i - 1 < QualitySettings.names.Length);
            var label = qualityToggles[i].GetComponentInChildren<TMP_Text>(true);
            if (label != null)
                label.text = QualityName(i - 1);
        }
        vibrationToggle.SetIsOnWithoutNotify(GameSettings.VibrationOn);
        remindersToggle.gameObject.SetActive(NotificationManager.IsSupported);
        RefreshReminders();
        rateButton.gameObject.SetActive(InAppReview.Instance != null);
    }

    protected override void OnHide()
    {
        if (DataManager.IsLoaded)
            DataManager.Save(true);
    }

    private void OnRemindersChanged(bool on)
    {
        if (on)
            StartCoroutine(NotificationManager.IETurnOn(allowed => RefreshReminders()));
        else
            NotificationManager.TurnOff();
    }

    private void OnFocusChanged(bool focused)
    {
        if (focused)
            RefreshReminders();
    }

    private static string QualityName(int quality) =>
        quality == GameSettings.QualityAuto ? "Auto" : QualitySettings.names[quality];

    private void RefreshReminders() =>
        remindersToggle.SetIsOnWithoutNotify(NotificationManager.RemindersOn && NotificationManager.IsAllowed);

    /// <summary>The UMP privacy options when the region needs them, else the privacy policy page.</summary>
    private static void OpenPrivacy()
    {
        if (UMP.Instance == null)
            Application.OpenURL(AdConfig.Instance != null ? AdConfig.Instance.urlPrivacyPolicy : "https://policies.google.com/privacy");
        else if (UMP.IsPrivacyOptionsRequired)
            UMP.Instance.ShowPrivacyOptionsForm();
        else
            UMP.Instance.OpenPrivacyPolicy();
    }
}
