#if USE_ADMOB
using GoogleMobileAds.Api;
using GoogleMobileAds.Ump.Api;
#endif
using System;
using System.Collections;
using UnityEngine;

namespace Base.Ads
{
    public class ADMOB : MonoBehaviour
    {
        public static bool IsInitialized = false;
        public static bool IsInitializing = false;
        protected static string TAG = "ADMOB ";

        protected static ADMOB intance;

        protected static DateTime LastInitializedTime = DateTime.MinValue;

        private void Awake()
        {
            intance = this;
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            if (!pauseStatus && !IsInitializing && !IsInitialized && LastInitializedTime == DateTime.MinValue)
            {
                Debug.Log(TAG + "OnApplicationPause INIT ADMOB" + " IsInitialized " + IsInitialized.ToString().ToUpper() + "LastInitializedTime " + LastInitializedTime.ToString());
                StartCoroutine(IEInit(null));
            }
        }

        public static IEnumerator IEInit(Action<bool> onInitDone)
        {
#if USE_ADMOB
            if (IsInitialized || IsInitializing)
            {
                onInitDone?.Invoke(IsInitialized);
                yield break;
            }

            if (!IsInitialized)
            {
                if (!IsInitializing)
                {
                    IsInitializing = true;

                    if (!UMP.CanRequestAds)
                        yield return UMP.Instance.IECheck();

                    if (UMP.CanRequestAds)
                    {
                        MobileAds.SetiOSAppPauseOnBackground(true);

                        MobileAds.Initialize((initStatus) =>
                        {
                            UnityMainThreadDispatcher.Enqueue(() =>
                            {
                                if (initStatus != null)
                                {
                                    Debug.Log(TAG + "Adapter Status Map CHECK ------------------------------------------------");
                                    var adapterStatusMap = initStatus.getAdapterStatusMap();
                                    if (adapterStatusMap != null)
                                    {
                                        foreach (var item in adapterStatusMap)
                                        {
                                            try
                                            {
                                                Debug.Log(TAG + string.Format("Adapter {0} is {1}", item.Key, item.Value.InitializationState));
                                            }
                                            catch (Exception ex)
                                            {
                                                Debug.LogException(ex);
                                            }
                                        }
                                    }
                                    Debug.Log(TAG + "Adapter Status Map DONE ------------------------------------------------");

                                    IsInitialized = true;
                                    LastInitializedTime = DateTime.Now;
                                }

                                // Clear first: an exception below must never leave the init flag stuck (no second init ever).
                                IsInitializing = false;
                                Debug.Log(TAG + "Initialize: DONE " + (initStatus != null ? "OK" : "initStatus NULL"));
                                onInitDone?.Invoke(IsInitialized);
                            });
                        });
                    }
                    else
                    {
                        IsInitializing = false;
                        Debug.LogError(TAG + "UMP ConsentStatus --> " + ConsentInformation.ConsentStatus.ToString() + " CanRequestAds: " + UMP.CanRequestAds.ToString().ToUpper() + " --> NOT INIT");
                        onInitDone?.Invoke(IsInitialized);
                    }
                }

                // OPTIMUS FIX 2: Failsafe Timeout to prevent Deadlock if AdMob server hangs
                float initTimeout = 6.8f; // Maximum wait time in seconds
                while (IsInitializing && initTimeout > 0)
                {
                    initTimeout -= Time.unscaledDeltaTime;
                    yield return null;
                }

                if (IsInitializing)
                {
                    // Keep IsInitializing: MobileAds.Initialize is still running (mediation adapters can take up to ~30 s).
                    // Clearing it let the splash's second AdsManager.IEInit call MobileAds.Initialize a second time.
                    // The caller continues now; the SDK callback still sets IsInitialized and calls onInitDone(true).
                    Debug.LogWarning(TAG + "Initialization still running after 6.8s --> continue without blocking.");
                    onInitDone?.Invoke(false);
                }
            }
#else
            Debug.LogError("Set Symbol USE_ADMOB in Player Settings");
            onInitDone?.Invoke(IsInitialized);
#endif
        }

        public void OpenAdInspector()
        {
#if USE_ADMOB
            Debug.Log("Opening ad Inspector.");

            MobileAds.OpenAdInspector((AdInspectorError error) =>
            {
                if (error != null)
                {
                    Debug.Log("Ad Inspector failed to open with error: " + error);
                    return;
                }

                Debug.Log("Ad Inspector opened successfully.");
            });
#endif
        }

    }
}