using Base;
using Base.Ads;
using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class InAppReview : MonoBehaviour
{
    public GameObject content;
    public Button buttonNotNow;
    public Button buttonSubmit;
    public TextMeshProUGUI textTitle;
    public TextMeshProUGUI textDescription;
    public TMP_InputField inputField;
    public Slider sliderRating;

    public string androidAppId => Application.identifier;

    [SerializeField] protected int _winToShow = 3;
    public int winToShow
    {
        get
        {
            return PlayerPrefs.GetInt("winToShow", 3);
        }
        set
        {
            PlayerPrefs.SetInt("winToShow", value);
            PlayerPrefs.Save();
        }
    }

    protected int _isRated = 0;
    public bool isRated
    {
        get
        {
            _isRated = PlayerPrefs.GetInt("isRated", _isRated);
            return _isRated >= 1;
        }
        set
        {
            _isRated = value ? 1 : 0;
            PlayerPrefs.SetInt("isRated", _isRated);
            PlayerPrefs.Save();
        }
    }

    public bool isTimeToShow => !isRated && DataManager.UserData != null && DataManager.UserData.totalWin >= winToShow;

    protected int rating;

    public static InAppReview Instance { get; private set; }

    private void Awake()
    {
        Instance = this;
        GameStateManager.OnGameStateChanged += OnGameStateChanged;
    }

    private void OnDestroy()
    {
        Instance = null;
        GameStateManager.OnGameStateChanged -= OnGameStateChanged;
    }

    private void Start()
    {
        buttonNotNow.onClick.AddListener(OnNotNowClicked);
        buttonSubmit.onClick.AddListener(OnSubmitClicked);
        sliderRating.onValueChanged.AddListener(OnRatingChanged);
        sliderRating.value = 0;
        inputField.onValueChanged.AddListener(OnInputFieldEndEdit);
        inputField.text = string.Empty;
        content.SetActive(false);
    }

    private void OnSubmitClicked()
    {
        if (rating == 5)
        {
            ZenAnalytics.LogEvent("rate_submit", new Dictionary<string, object> { { "rating", rating }, { "total_win", DataManager.UserData.totalWin } });
            ShowInAppReview();
        }
        else
        {
            ZenAnalytics.LogEvent("rate_feedback", new Dictionary<string, object> { { "rating", rating }, { "total_win", DataManager.UserData.totalWin } });
            if (inputField.text == string.Empty)
            {
                buttonSubmit.interactable = false;
                textDescription.gameObject.SetActive(false);
                inputField.gameObject.SetActive(true);
            }
            else
            {
                content.SetActive(false);
                AdsManager.ShowInter((type, state) =>
                {
                    //if (state == AdState.ShowSuccess && (type == AdType.Reward || type == AdType.RewardInter))
                    //{
                    //    CurrencyManager.AddSoft(AdsManager.SoftCurrencyByReward, name, transform);
                    //}
                }, name);
            }
        }
    }

    private void OnNotNowClicked()
    {
        ZenAnalytics.LogEvent("rate_later", new Dictionary<string, object> { { "rating", rating }, { "total_win", DataManager.UserData.totalWin } });
        content.SetActive(false);
    }

    private void OnRatingChanged(float value)
    {
        rating = (int)value;
        buttonSubmit.interactable = rating > 0;
    }

    private void OnInputFieldEndEdit(string value)
    {
        buttonSubmit.interactable = !string.IsNullOrEmpty(value) && value.Length >= 32;
    }


    private void OnGameStateChanged(GameState current, GameState last, object data)
    {
        if (last == GameState.Win)
        {
            CheckToShow();
        }
    }

    private void CheckToShow()
    {
        if (DebugMode.IsOn)
            Debug.Log("InAppReview: CheckToShow: isRated=" + isRated + ", totalWin=" + DataManager.UserData.totalWin + ", winToShow=" + winToShow);

        if (isTimeToShow)
        {
            winToShow += 3;
            if (winToShow >= 8)
                winToShow = -1;
            ShowInAppReview();
        }
    }

    public void ShowInAppReview()
    {
        content.SetActive(false);
        StartCoroutine(IERequestReview((result) =>
        {
            Debug.Log("Review: " + result);
        }));
    }

    public IEnumerator IERequestReview(Action<bool> result = null)
    {
        yield return null;
#if UNITY_ANDROID
#if USE_IN_APP_REVIEW
        var reviewManager = new Google.Play.Review.ReviewManager();
        // start preloading the review prompt in the background
        var playReviewInfoAsyncOperation = reviewManager.RequestReviewFlow();
        // define a callback after the preloading is done
        playReviewInfoAsyncOperation.Completed += playReviewInfoAsync =>
        {
            if (playReviewInfoAsync.Error == Google.Play.Review.ReviewErrorCode.NoError)
            {
                // display the review prompt
                var playReviewInfo = playReviewInfoAsync.GetResult();
                var launchReviewFlow = reviewManager.LaunchReviewFlow(playReviewInfo);
                launchReviewFlow.Completed += (s) =>
                {
                    Debug.Log("Review: " + s.IsSuccessful + " " + s.IsDone + " " + playReviewInfoAsync.Error.ToString());
                    isRated = true;
                    result?.Invoke(s.IsSuccessful);
                };
            }
            else
            {
                Debug.LogError("Review: " + "Handle error when loading review prompt");
                result?.Invoke(false);
            }
        };
#else
        // The player leaves for the store page: no app open ad when they come back.
        AdsManager.SuppressNextAppOpen("store_review");
        Application.OpenURL("http://play.google.com/store/apps/details?id=" + androidAppId);
        result?.Invoke(false);
        isRated = true;
#endif
#elif UNITY_IOS
        var canReview = UnityEngine.iOS.Device.RequestStoreReview();
        Debug.Log("Review: " + canReview);
        result?.Invoke(canReview);
        isRated = true;
#endif
    }
}
