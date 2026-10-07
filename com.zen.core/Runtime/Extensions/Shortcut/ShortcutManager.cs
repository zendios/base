using System;
using UnityEngine;

public class ShortcutManager : MonoBehaviour
{
    public static string TAG = "[ShortcutManager]";
    [SerializeField] protected string continueTitle = "Continue";
    [SerializeField] protected string dailyTitle = "Daily Challenge";
    [SerializeField] protected string rewardTitle = "Claim Reward";
    [SerializeField] protected string feedbackTitle = "Help & Feedback";

    public static ShortcutManager Instance;
    public static event Action OnContinueTriggered;
    public static event Action OnDailyChallengeTriggered;
    public static event Action OnClaimRewardTriggered;
    public static event Action OnHelpFeedbackTriggered;

#if UNITY_ANDROID
    private AndroidJavaClass _shortcutHelperClass;
    private AndroidJavaObject _currentActivity;
    private bool _isInitialized = false;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        DontDestroyOnLoad(gameObject);

        // Check if the GameObject is named correctly for Java to find it
        gameObject.name = "ShortcutManager";

        InitializeNativePlugin();
    }

    private void Start()
    {
        OnContinueTriggered += HandleContinue;
        OnDailyChallengeTriggered += HandleDailyChallenge;
        OnClaimRewardTriggered += HandleClaimReward;
        OnHelpFeedbackTriggered += HandleHelpFeedback;

        ConfigureDynamicShortcuts();

        // Check immediately on Cold Start
        CheckPendingShortcut();
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        // Backup poll in case focus transitions occur during Warm Start
        if (hasFocus)
        {
            CheckPendingShortcut();
        }
    }

    private void OnApplicationPause(bool isPaused)
    {
        // Backup poll when app resumes from background
        if (!isPaused)
        {
            CheckPendingShortcut();
        }
    }

    private void OnDestroy()
    {
        OnContinueTriggered -= HandleContinue;
        OnDailyChallengeTriggered -= HandleDailyChallenge;
        OnClaimRewardTriggered -= HandleClaimReward;
        OnHelpFeedbackTriggered -= HandleHelpFeedback;
    }

    private void HandleContinue()
    {
        Debug.Log(TAG + "Continue");
    }

    private void HandleDailyChallenge()
    {
        Debug.Log(TAG + "Daily Challenge");
    }

    private void HandleClaimReward()
    {
        Debug.Log(TAG + "Claim Reward");
    }

    private void HandleHelpFeedback()
    {
        Debug.Log(TAG + "Help & Feedback");
    }

    public void InitializeNativePlugin()
    {
        if (Application.platform != RuntimePlatform.Android)
        {
            return;
        }

        try
        {
            using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            {
                _currentActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
            }

            _shortcutHelperClass = new AndroidJavaClass("com.zen.plugins.ShortcutHelper");
            _isInitialized = true;
        }
        catch (Exception exception)
        {
            Debug.LogError($"{TAG} Failed to initialize native JNI plugin: {exception.Message}");
            _isInitialized = false;
        }
    }

    public void ConfigureDynamicShortcuts(string continueTitle = "Continue", string dailyTitle = "Daily Challenge", string rewardTitle = "Claim Reward", string feedbackTitle = "Help & Feedback")
    {
        if (Application.platform != RuntimePlatform.Android || !_isInitialized || _shortcutHelperClass == null || _currentActivity == null)
        {
            return;
        }

        try
        {
            _shortcutHelperClass.CallStatic("configureShortcuts",
                _currentActivity,
                continueTitle,
                dailyTitle,
                rewardTitle,
                feedbackTitle
            );
        }
        catch (Exception exception)
        {
            Debug.LogError($"{TAG} Failed to update dynamic shortcuts: {exception.Message}");
        }
    }

    /// <summary>
    /// JNI Target Method. Triggered instantly by Java's UnitySendMessage.
    /// </summary>
    public void ReceiveShortcutMessage(string shortcutId)
    {
        Debug.Log($"{TAG} JNI Push Message received: {shortcutId}");
        if (!string.IsNullOrEmpty(shortcutId))
        {
            // Verify and consume cache from Java to avoid duplicate execution
            ConsumeAndDispatchShortcut(shortcutId);
        }
    }

    /// <summary>
    /// Backup Polling Method. Retrieves and clears any pending shortcut from Java.
    /// </summary>
    public void CheckPendingShortcut()
    {
        if (Application.platform != RuntimePlatform.Android || !_isInitialized || _shortcutHelperClass == null)
        {
            return;
        }

        try
        {
            string pendingShortcutId = _shortcutHelperClass.CallStatic<string>("popPendingShortcut");
            if (!string.IsNullOrEmpty(pendingShortcutId))
            {
                DispatchShortcutEvent(pendingShortcutId);
            }
        }
        catch (Exception exception)
        {
            Debug.LogError($"{TAG} Failed to poll pending shortcut: {exception.Message}");
        }
    }

    private void ConsumeAndDispatchShortcut(string shortcutId)
    {
        if (Application.platform != RuntimePlatform.Android || !_isInitialized || _shortcutHelperClass == null)
        {
            DispatchShortcutEvent(shortcutId);
            return;
        }

        try
        {
            // Try to pop the pending ID to clear the cache and verify state
            string verifiedId = _shortcutHelperClass.CallStatic<string>("popPendingShortcut");
            if (!string.IsNullOrEmpty(verifiedId))
            {
                DispatchShortcutEvent(verifiedId);
            }
        }
        catch (Exception exception)
        {
            Debug.LogError($"{TAG} Failed to consume pending shortcut: {exception.Message}");
            // Fallback dispatch if pop execution fails
            DispatchShortcutEvent(shortcutId);
        }
    }

    private void DispatchShortcutEvent(string shortcutId)
    {
        Debug.Log($"{TAG} Dispatching shortcut event: {shortcutId}");
        switch (shortcutId)
        {
            case "continue":
                OnContinueTriggered?.Invoke();
                break;
            case "daily_challenge":
                OnDailyChallengeTriggered?.Invoke();
                break;
            case "claim_reward":
                OnClaimRewardTriggered?.Invoke();
                break;
            case "help_feedback":
                OnHelpFeedbackTriggered?.Invoke();
                break;
            default:
                Debug.LogWarning($"{TAG} Unknown shortcut ID triggered: {shortcutId}");
                break;
        }
    }
#endif
}