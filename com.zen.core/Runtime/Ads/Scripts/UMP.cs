using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine.EventSystems;

#if USE_ADMOB
using GoogleMobileAds.Ump.Api;
#endif

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Base.Ads
{
    public class UMP : MonoBehaviour, IPointerClickHandler //ENABLE_INPUT_SYSTEM in Player Settings may cause EventSystem.current.currentInputModule.inputActions to be null, so we use IPointerClickHandler to capture clicks on the description text for link handling
    {
        protected static string TAG = "UMP ";
        public static bool HasError = false;
#if USE_ADMOB
        /// <summary>
        /// Ads may be requested: only the UMP answer decides (ConsentInformation.CanRequestAds() keeps the consent of a
        /// previous session, so an offline start still serves ads to users who consented before). A consent error or
        /// ATT denied never allows requests by itself.
        /// </summary>
        public static bool CanRequestAds => ConsentInformation.CanRequestAds();

        /// <summary>The consent form (initial or privacy options) is on screen.</summary>
        public static bool IsFormShowing { get; private set; }

        /// <summary>The user must be offered a privacy options entry point (Settings button -> ShowPrivacyOptionsForm).</summary>
        public static bool IsPrivacyOptionsRequired =>
            ConsentInformation.PrivacyOptionsRequirementStatus == PrivacyOptionsRequirementStatus.Required;


        [Header("DEBUG")]
        [SerializeField]
        private DebugGeography debugGeography = DebugGeography.Disabled;
        [SerializeField, Tooltip("https://developers.google.com/admob/unity/test-ads")]
        private List<string> testDeviceIds;
#endif

        [Header("PRIVACY & TERM")]
        [SerializeField]
        public TextMeshProUGUI title;
        public TextMeshProUGUI description;
        public GameObject content;
        public Button continueButton;

        public Camera uiCamera;

        protected AdConfig adConfig => AdConfig.Instance;


#if USE_ADMOB

        public static UMP Instance = null;

        private void Awake()
        {
            Instance = this;
            if (content != null)
                content.SetActive(false);
            if (title != null)
                title.text = "Welcom to " + Application.productName;
        }

        protected static bool isChecking = false;
        /// <summary>
        /// Startup method for the Google User Messaging Platform (UMP) SDK
        /// which will run all startup logic including loading any required
        /// updates and displaying any required forms.
        /// </summary>
        public IEnumerator IECheck()
        {
            // ATT only decides the IDFA; GDPR consent is still asked below.
            bool attDenied = ATTHelper.Status == ATTStatus.DENIED;

            if (IsFormShowing)
            {
                Debug.Log(TAG + "Consent form is showing --> RETURN (its answer starts the ads if allowed)");
                yield break;
            }

            if (!attDenied && content != null)
            {
                if (ATTHelper.Status == ATTStatus.NOT_DETERMINED)
                {
                    content.SetActive(true);
                    continueButton.onClick.AddListener(() =>
                    {
                        ATTHelper.Status = ATTStatus.AUTHORIZED;
                        content.SetActive(false);
                    });
                    while (ATTHelper.Status == ATTStatus.NOT_DETERMINED)
                        yield return null;
                }
                else
                {
                    ATTHelper.Status = ATTStatus.AUTHORIZED;
                    content.SetActive(false);
                }
            }

            if (isChecking)
            {
                Debug.LogError(TAG + ConsentInformation.ConsentStatus.ToString().ToUpper() + " CHECKING --> RETURN");
                yield break;
            }

            isChecking = true;
            HasError = false;   // this check decides

            ConsentRequestParameters requestParameters = new ConsentRequestParameters
            {
                TagForUnderAgeOfConsent = false,  // False means users are not under age.
                ConsentDebugSettings = new ConsentDebugSettings
                {
                    DebugGeography = debugGeography, // For debugging consent settings by geography.                
                    TestDeviceHashedIds = testDeviceIds, // https://developers.google.com/admob/unity/test-ads
                }
            };

            // The Google Mobile Ads SDK provides the User Messaging Platform (Google's
            // IAB Certified consent management platform) as one solution to capture
            // consent for users in GDPR impacted countries. This is an example and
            // you can choose another consent management platform to capture consent.
            Debug.Log(TAG + ConsentInformation.ConsentStatus.ToString().ToUpper() + " --> UPDATE");

            ConsentInformation.Update(requestParameters, (FormError error) =>
            {
                UnityMainThreadDispatcher.Enqueue(() =>
                {
                    if (error != null)
                    {
                        Debug.LogError(TAG + ConsentInformation.ConsentStatus.ToString().ToUpper() + " --> " + error.Message);
                        isChecking = false;
                        HasError = true;
                        return;
                    }

                    if (CanRequestAds) // Determine the consent-related action to take based on the ConsentStatus.
                    {
                        // Consent has already been gathered or not required.
                        // Return control back to the user.
                        Debug.Log(TAG + "Update " + ConsentInformation.ConsentStatus.ToString().ToUpper() + " -- Consent has already been gathered or not required");
                        isChecking = false;
                        return;
                    }

                    // Consent not obtained and is required.
                    // Load the initial consent request form for the user.
                    Debug.Log(TAG + ConsentInformation.ConsentStatus.ToString().ToUpper() + " --> LOAD AND SHOW ConsentForm If Required");
                    IsFormShowing = true;
                    ConsentForm.LoadAndShowConsentFormIfRequired((FormError error) =>
                    {
                        UnityMainThreadDispatcher.Enqueue(() =>
                        {
                            IsFormShowing = false;
                            if (error != null) // Form load failed.
                            {
                                Debug.LogError(TAG + ConsentInformation.ConsentStatus.ToString().ToUpper() + " --> " + error.Message);
                                HasError = true;
                            }
                            else  // Form showing succeeded.
                            {
                                Debug.Log(TAG + ConsentInformation.ConsentStatus.ToString().ToUpper() + " --> LOAD AND SHOW SUCCESS");
                            }
                            isChecking = false;
                            InitAdsIfLateConsent();
                        });
                    });
                });
            });

            Debug.Log(TAG + ConsentInformation.ConsentStatus.ToString().ToUpper() + " --> WAIT!");

            float initTimeout = 6.8f;
            while (isChecking && initTimeout > 0 && (ConsentInformation.ConsentStatus == ConsentStatus.Required || ConsentInformation.ConsentStatus == ConsentStatus.Unknown))
            {
                initTimeout -= Time.unscaledDeltaTime;
                yield return null;
            }

            isChecking = false;
            Debug.Log(TAG + ConsentInformation.ConsentStatus.ToString().ToUpper());
        }

        /// <summary>
        /// The splash waits for consent at most 6.8 s; a form answered later used to leave MobileAds uninitialized for the
        /// whole session. Start the ads now (AdsManager keeps its state: IsInitialized, the pending session preload).
        /// </summary>
        private static void InitAdsIfLateConsent()
        {
            if (!ConsentInformation.CanRequestAds())
                return;
            if (ADMOB.IsInitialized || ADMOB.IsInitializing || AdsManager.IsInitialized || AdsManager.IsInitializing)
                return;
            Debug.Log(TAG + "Late consent --> AdsManager.IEInit");
            UnityMainThreadDispatcher.Enqueue(AdsManager.IEInit());
        }

        /// <summary>A check that failed (e.g. offline start) is retried when the app comes back.</summary>
        private void OnApplicationPause(bool pause)
        {
            if (pause || !HasError || isChecking || IsFormShowing || ConsentInformation.CanRequestAds())
                return;
            HasError = false;
            StartCoroutine(IERecheck());
        }

        private IEnumerator IERecheck()
        {
            yield return IECheck();
            InitAdsIfLateConsent();
        }

        /// <summary>
        /// Shows the privacy options form to the user.
        /// </summary>
        /// <remarks>
        /// Your app needs to allow the user to change their consent status at any time.
        /// Load another form and store it to allow the user to change their consent status
        /// </remarks>
        public void ShowPrivacyOptionsForm()
        {
            UnityMainThreadDispatcher.Enqueue(() =>
            {
                Debug.Log(TAG + "Showing privacy options form...");
                ZenAnalytics.LogEvent("ump_option_show");
                IsFormShowing = true;
                ConsentForm.ShowPrivacyOptionsForm((FormError error) =>
                {
                    IsFormShowing = false;
                    if (error != null)
                    {
                        Debug.LogError(TAG + "Showing privacy options form - ERROR " + error.Message);
                        AdToast.ShowNotice("ERROR: " + error.Message);
                        ZenAnalytics.LogEvent("ump_option_show_error", new Dictionary<string, object> { { "error", error.Message } });
                    }
                    else  // Form showing succeeded.
                    {
                        Debug.Log(TAG + "Showing privacy options form - SUCCESS");
                        ZenAnalytics.LogEvent("ump_option_show_success");
                    }
                });
            });
        }

        /// <summary>
        /// Reset ConsentInformation for the user.
        /// </summary>
        public void ResetConsentInformation()
        {
            UnityMainThreadDispatcher.Enqueue(() =>
            {
                AdToast.ShowNotice("Ooop...! \"We\" want asks for your consent to improve the best experience!", 3f);
                ZenAnalytics.LogEvent("ump_reset");
                ConsentInformation.Reset();
            });
        }

        public void OpenPrivacyPolicy()
        {
            ZenAnalytics.LogEvent("ump_open_privacy");
            if (!string.IsNullOrEmpty(adConfig.urlPrivacyPolicy))
                Application.OpenURL(adConfig.urlPrivacyPolicy);
            else
                Application.OpenURL("https://policies.google.com/privacy");

        }

        public void OpenTermsOfService()
        {
            ZenAnalytics.LogEvent("ump_open_terms");
            if (!string.IsNullOrEmpty(adConfig.urlTermsOfService))
                Application.OpenURL(adConfig.urlTermsOfService);
            else
                Application.OpenURL("https://policies.google.com/terms");

        }

        public void OnPointerClick(PointerEventData eventData)
        {
            // Pass the exact interaction position and the camera that captured the event
            ProcessLinkClick(eventData.position, eventData.pressEventCamera);
        }

        // Refactored to accept Camera dependency directly, removing expensive Linq queries
        private void ProcessLinkClick(Vector2 screenPosition, Camera eventCamera)
        {
            // Defensive check: Ensure UI text components are fully initialized
            if (description == null || description.textInfo == null || description.textInfo.linkInfo == null)
                return;

            // Fallback to assigned uiCamera if EventSystem camera is null (e.g., Canvas is Screen Space - Overlay)
            Camera activeCamera = eventCamera != null ? eventCamera : uiCamera;

            int linkIndex = TMP_TextUtilities.FindIntersectingLink(description, screenPosition, activeCamera);

            if (linkIndex != -1 && description.textInfo.linkInfo.Length > 0)
            {
                TMP_LinkInfo linkInfo = description.textInfo.linkInfo[linkIndex];
                string linkId = linkInfo.GetLinkID();

                switch (linkId)
                {
                    case "linkTerm":
                        OpenTermsOfService();
                        break;
                    case "linkPolicy":
                        OpenPrivacyPolicy();
                        break;
                }
            }
        }
#else
        public object IECheck(bool reset) { return null; }

        public bool CanRequestAds { get; internal set; }

        public void OnPointerClick(PointerEventData eventData)
        {
            throw new NotImplementedException();
        }
#endif
    }
}

