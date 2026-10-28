using UnityEngine;
using System.Collections.Generic;
using System.Collections;
using System;
using Base.Ads;

#if UNITY_IOS
using Unity.Advertisement.IosSupport;
#endif
#if APPSFLYER_TOO_OLD
#error Base needs AppsFlyer 7.0.0 or newer: Base > Hub > Setup, AppsFlyer row, "Use 7.0.3".
#endif
#if !USE_APPSFLYER
public class AppsFlyerHelper : MonoBehaviour
{
    /// <summary>AppsFlyer not installed: nothing to start (same call as the AppsFlyer build of this class).</summary>
    public static IEnumerator IEInit()
    {
        yield break;
    }

    public static string UserId
    {
        get
        {
            string userId = PlayerPrefs.GetString("UserId", "");
            if (string.IsNullOrEmpty(userId))
            {
                userId = Guid.NewGuid().ToString();
                PlayerPrefs.SetString("UserId", userId);
                PlayerPrefs.Save();
            }
            return userId;
        }
    }
}
#else
using AppsFlyerSDK;
// This class is intended to be used the the AppsFlyerHelper.prefab
public class AppsFlyerHelper : MonoBehaviour, IAppsFlyerConversionData
{

    // These fields are set from the editor so do not modify!
    //******************************//
    public static readonly string TAG = "[AppsFlyer] ";
    public static AdConfig AdConfig => AdConfig.Instance;
    public static string DevKey => AdConfig.devKey;
    public static string AppID => AdConfig.appId;

    public bool getConversionData = true;
    public bool waitInitDone = true;

    private static bool IsInitialized = false;
    //******************************//

    public static AppsFlyerHelper instance = null;

    private void Awake()
    {
        instance = this;

#if USE_FIREBASE_MESSAGE && USE_APPSFLYER && UNITY_ANDROID
        Firebase.Messaging.FirebaseMessaging.TokenReceived += OnTokenReceived;
#endif
    }

    public static IEnumerator IEInit()
    {
        if (instance != null)
        {
            try
            {
                IsInitialized = false;

                if (AdConfig == null || string.IsNullOrEmpty(AdConfig.devKey))
                {
                    Debug.LogError($"{TAG} DevKey is NULL or EMPTY");
                    yield break;
                }

                InitAndStart();

                if (DebugMode.IsOn)
                    Debug.Log($"{TAG} enableDebug {DebugMode.IsOn} init {DevKey} {AppID} start...");
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }

            if (instance.getConversionData && instance.waitInitDone)
            {
                float elapseTime = 0;
                while (IsInitialized == false && elapseTime < 2.5f)
                {
                    yield return null;
                    elapseTime += Time.unscaledDeltaTime;
                }

                if (IsInitialized)
                {
                    Debug.Log($"{TAG} init an callback in {elapseTime:0.0}s");
                }
            }
            else
            {
                IsInitialized = true;
            }
        }
        else
        {
            IsInitialized = true;
            throw new Exception($"{TAG} instance is null. Make sure the AppsFlyerHelper prefab is in the first scene and not destroyed on load.");
        }
    }

    /// <summary>AppsFlyer 7.x: enableDebug, init and start are Awaitable, run in sequence.</summary>
    private static async void InitAndStart()
    {
        try
        {
            await AppsFlyer.enableDebug(DebugMode.IsOn);
            await AppsFlyer.init(DevKey, AppID, instance.getConversionData ? instance : null);
            await AppsFlyer.start();
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
        }
    }

    // Mark AppsFlyer CallBacks
    public void onConversionDataSuccess(string conversionData)
    {
        IsInitialized = true;
        Debug.Log($"{TAG} onConversionDataSuccess!");
        if (!string.IsNullOrEmpty(conversionData))
        {
            Dictionary<string, object> conversionDataDictionary = AppsFlyer.CallbackStringToDictionary(conversionData);
            if (conversionDataDictionary != null && conversionDataDictionary.TryGetValue("af_status", out var status) && status is string && status.Equals("Organic"))
            {
                DataManager.UserData.isNonOrganic = false;
            }
            else
            {
                DataManager.UserData.isNonOrganic = true;
            }
        }
    }

    public void onConversionDataFail(string error)
    {
        IsInitialized = true;
        Debug.LogWarning($"{TAG} onConversionDataFail: " + error);
    }

    public void onAppOpenAttribution(string attributionData)
    {
        if (DebugMode.IsOn)
            Debug.LogError($"{TAG} onAppOpenAttribution " + attributionData);
        //if (!string.IsNullOrEmpty(attributionData))
        //{
        //    Dictionary<string, object> attributionDataDictionary = AppsFlyer.CallbackStringToDictionary(attributionData);
        //}
    }

    public void onAppOpenAttributionFailure(string error)
    {
        if (DebugMode.IsOn)
            Debug.LogError($"{TAG} onAppOpenAttributionFailure: " + error);
    }
}
#endif