using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

#if USE_UNITY_LEADERBOARD
using Unity.Services.Core;
using Unity.Services.Authentication;
using Unity.Services.Leaderboards;
using Unity.Services.Leaderboards.Models;
#endif

/// <summary>
/// Optimus: Cost-Effective Leaderboard Manager.
/// This class implements aggressive caching and conditional submission 
/// to minimize API calls and keep costs at $0 for as long as possible.
/// </summary>
public class LeaderboardManager : MonoBehaviour
{
#if USE_UNITY_LEADERBOARD
    public static LeaderboardManager Instance { get; private set; }

    [Header("Configuration")]
    [SerializeField] private string _leaderboardId = "MainHighScores";

    // Optimus: Cache duration in seconds. 
    // Don't fetch new scores if the last fetch was within this time window.
    [SerializeField] private float _cacheDuration = 60f;

    private bool _isInitialized = false;

    // Optimus: Internal Cache State
    private List<LeaderboardEntry> _cachedTopScores;
    private float _lastFetchTime = -999f;
    private double _localBestScore = -1;

    private const string LOCAL_SCORE_KEY = "Optimus_Local_Best_Score";

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this.gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(this.gameObject);

        // Optimus: Load local best score immediately to prevent unnecessary writes later
        _localBestScore = PlayerPrefs.GetFloat(LOCAL_SCORE_KEY, 0);
    }

    private async void Start()
    {
        await InitializeServicesAsync();
    }

    private async Task InitializeServicesAsync()
    {
        try
        {
            await UnityServices.InitializeAsync();

            if (!AuthenticationService.Instance.IsSignedIn)
            {
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
            }

            _isInitialized = true;
            Debug.Log($"[Optimus] Service Initialized. PlayerID: {AuthenticationService.Instance.PlayerId}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[Optimus] Init Failed: {e.Message}");
        }
    }

    /// <summary>
    /// Optimus: Smart Submit.
    /// Only talks to the server if the new score is actually a high score.
    /// SAVES MONEY: Reduces Write Operations by ~90%.
    /// </summary>
    public async void SubmitScore(double currentScore, Action onSuccess = null, Action<string> onFail = null)
    {
        if (!_isInitialized) return;

        // Optimus: Logic Check - Is this actually a new high score?
        if (currentScore <= _localBestScore)
        {
            Debug.Log($"[Optimus] Score {currentScore} is not higher than local best {_localBestScore}. API Call Skipped (Cost Saved).");
            // Still trigger success because from the UI perspective, the game is over successfully.
            onSuccess?.Invoke();
            return;
        }

        try
        {
            // Optimus: It's a new record! Update local first.
            _localBestScore = currentScore;
            PlayerPrefs.SetFloat(LOCAL_SCORE_KEY, (float)_localBestScore);
            PlayerPrefs.Save();

            // Optimus: Now pay the cost to update the server
            var response = await LeaderboardsService.Instance.AddPlayerScoreAsync(_leaderboardId, currentScore);

            Debug.Log($"[Optimus] New High Score Submitted to Cloud: {response.Score}");

            // Optimus: Invalidate cache because the board has changed
            _lastFetchTime = -999f;

            onSuccess?.Invoke();
        }
        catch (Exception e)
        {
            Debug.LogError($"[Optimus] Submit Failed: {e.Message}");
            onFail?.Invoke(e.Message);
        }
    }

    /// <summary>
    /// Optimus: Smart Fetch.
    /// Returns cached data if called frequently.
    /// SAVES MONEY: Reduces Read Operations significantly.
    /// </summary>
    public async void GetTopScores(int limit, Action<List<LeaderboardEntry>> onSuccess, Action<string> onFail)
    {
        if (!_isInitialized)
        {
            onFail?.Invoke("Service not ready");
            return;
        }

        // Optimus: Check Cache Validity
        if (_cachedTopScores != null && Time.time - _lastFetchTime < _cacheDuration)
        {
            Debug.Log("[Optimus] Returning Cached Leaderboard (Free of charge).");
            onSuccess?.Invoke(_cachedTopScores);
            return;
        }

        try
        {
            var options = new GetScoresOptions { Limit = limit };
            var scoresResponse = await LeaderboardsService.Instance.GetScoresAsync(_leaderboardId, options);

            // Optimus: Update Cache
            _cachedTopScores = scoresResponse.Results;
            _lastFetchTime = Time.time;

            Debug.Log($"[Optimus] Fetched fresh data from Cloud. Count: {_cachedTopScores.Count}");
            onSuccess?.Invoke(_cachedTopScores);
        }
        catch (Exception e)
        {
            Debug.LogError($"[Optimus] Fetch Failed: {e.Message}");
            onFail?.Invoke(e.Message);
        }
    }

    /// <summary>
    /// Optimus: Force Refresh.
    /// Call this only when the user explicitly clicks a "Refresh" button.
    /// </summary>
    public void ForceRefresh()
    {
        _lastFetchTime = -999f;
    }
#endif
}