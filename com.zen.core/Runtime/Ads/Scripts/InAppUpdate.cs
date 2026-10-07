using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Base;
using TMPro;

#if USE_IN_APP_UPDATE && UNITY_ANDROID
//1. Download:
// - com.google.external-dependency-manager.tgz from https://dl.google.com/games/registry/unity/com.google.external-dependency-manager/com.google.external-dependency-manager-1.2.177.tgz
// - com.google.android.appbundle.tgz from https://dl.google.com/games/registry/unity/com.google.android.appbundle/com.google.android.appbundle-1.9.0.tgz
// - com.google.play.appupdate.tgz from https://dl.google.com/games/registry/unity/com.google.play.appupdate/com.google.play.appupdate-1.8.1.tgz
// - com.google.play.common.tgz from https://dl.google.com/games/registry/unity/com.google.play.common/com.google.play.common-1.8.2.tgz
// - com.google.play.core.tgz from https://dl.google.com/games/registry/unity/com.google.play.core/com.google.play.core-1.8.2.tgz

//2. Copy all packages downloaded to ProjectFolder/Packages

//3. Add all packages to Project from Packages Manager --> Add package from tarball --> Browse packages

using Google.Play.Common;
using Google.Play.AppUpdate;
#endif


public class InAppUpdate : MonoBehaviour
{
    public GameObject content;
    public Button updateButton;
    public Button laterButton;
    public TextMeshProUGUI updateStatus;

    protected string TAG = "IN_APP_UPDATE ";
#if USE_IN_APP_UPDATE && UNITY_ANDROID
    protected AppUpdateManager appUpdateManager;
    protected AppUpdateInfo appUpdateInfo;
#endif

    public static InAppUpdate Instance = null;

    protected Action<string> OnUpdateError;

    private void Awake()
    {
        Instance = this;
        if (content != null)
            content.SetActive(false);
        updateStatus.text = "It's take a few seconds...";

    }

    private void Start()
    {
        updateButton.onClick.AddListener(() =>
        {
            ZenAnalytics.LogEvent("iau_click_update");
            updateStatus.text = "Getting Update info...";
            updateButton.interactable = false;
            laterButton.interactable = false;
            StopAllCoroutines();

            StartCoroutine(CheckForUpdate((onAvailable) =>
            {
                if (onAvailable)
                {
                    StartCoroutine(UpdateApp((onUpdateError) =>
                    {
                        OnUpdateError?.Invoke(onUpdateError);

                        if (string.IsNullOrEmpty(onUpdateError))
                            content.SetActive(false);
                        else
                        {
                            updateButton.interactable = true;
                            laterButton.interactable = true;
                        }
                    }));
                }
                else
                {
                    OnUpdateError?.Invoke("not_available");
                    content.SetActive(false);
                }
            }));
        });

        laterButton.onClick.AddListener(() =>
        {
            ZenAnalytics.LogEvent("iau_click_later");
            updateButton.interactable = false;
            laterButton.interactable = false;
            OnUpdateError?.Invoke("later");
            content.SetActive(false);
        });
    }

    /// <summary>
    /// Check New Version Code Update Availability
    /// </summary>
    /// <param name="isAvaiable">if isAvaiable == true --> call DOShow</param>
    /// <returns></returns>
    public IEnumerator CheckForUpdate(Action<bool> isAvaiable)
    {
        if (Application.platform != RuntimePlatform.Android)
        {
            updateButton.interactable = false;
            isAvaiable?.Invoke(false);
            yield break;
        }

#if USE_IN_APP_UPDATE && UNITY_ANDROID
        ZenAnalytics.LogEvent("iau_check");
        updateButton.interactable = false;
        yield return new WaitForEndOfFrame();

        Debug.Log(TAG + "GetAppUpdateInfo");
        updateStatus.text = "Getting Update Info...";
        appUpdateManager = new AppUpdateManager();
        if (appUpdateManager == null)
        {
            isAvaiable?.Invoke(false);
            yield break;
        }

        PlayAsyncOperation<AppUpdateInfo, AppUpdateErrorCode> updateInfo = appUpdateManager.GetAppUpdateInfo();
        if (updateInfo == null)
        {
            isAvaiable?.Invoke(false);
            yield break;
        }

        yield return updateInfo; // Wait until the asynchronous operation completes.

        while (!updateInfo.IsDone)
            yield return null;

        if (updateInfo.IsSuccessful)
        {
            Debug.Log(TAG + "GetAppUpdateInfo SUCCESS");
            appUpdateInfo = updateInfo.GetResult();
            // Check AppUpdateInfo's UpdateAvailability, UpdatePriority, IsUpdateTypeAllowed(), etc. and decide whether to ask the user to start an in-app update.
            if (appUpdateInfo != null)
            {
                var updateAvailability = appUpdateInfo.UpdateAvailability;
                var appUpdateStatus = appUpdateInfo.AppUpdateStatus;
                var availableVersionCode = appUpdateInfo.AvailableVersionCode;

                updateStatus.text = "Status: " + updateAvailability.ToString() + " Version Code: " + availableVersionCode;
                Debug.Log(TAG + "Status: " + updateAvailability.ToString() + " AppUpdateStatus: " + appUpdateStatus.ToString() + " VersionCode: " + availableVersionCode);

                ZenAnalytics.LogEvent("iau_check_" + updateAvailability.ToString());
                if (updateAvailability == UpdateAvailability.UpdateNotAvailable)
                {
                    updateStatus.text = "No updates are available.";
                    isAvaiable?.Invoke(false);
                }
                else if (updateAvailability == UpdateAvailability.Unknown)
                {
                    updateStatus.text = "Update availability is unknown.";
                    isAvaiable?.Invoke(false);
                }
                else if (updateAvailability == UpdateAvailability.DeveloperTriggeredUpdateInProgress)
                {
                    updateStatus.text = "An update has been triggered by the developer and is in progress.";
                    isAvaiable?.Invoke(false);
                }
                else
                {
                    updateButton.interactable = true;
                    updateStatus.text = "An update available with VersionCode: " + availableVersionCode;
                    isAvaiable?.Invoke(true);
                }
            }
            else
            {
                updateStatus.text = "ERROR: " + "UpdateInfoResull NULL";
                Debug.LogError(TAG + "ERROR: " + "UpdateInfoResull NULL");
                isAvaiable?.Invoke(false);
            }
        }
        else
        {
            updateStatus.text = "ERROR CODE: " + updateInfo.Error.ToString();
            Debug.LogError(TAG + "GetAppUpdateInfo ERROR: " + updateInfo.Error.ToString()); // Log appUpdateInfoOperation.Error.
            isAvaiable?.Invoke(false);
        }
#endif
    }

    public IEnumerator DOShow(Action<string> onUpdateError, bool forceUpdate)
    {
        ZenAnalytics.LogEvent("iau_show");
        OnUpdateError = onUpdateError;
        laterButton.gameObject.SetActive(!forceUpdate);
        content.SetActive(true);
        yield return new WaitForEndOfFrame();
        while (content.activeSelf)
            yield return null;
    }

    public IEnumerator UpdateApp(Action<string> onUpdateError)
    {
        yield return new WaitForEndOfFrame();

#if USE_IN_APP_UPDATE && UNITY_ANDROID
        if (appUpdateManager == null || appUpdateInfo == null)
        {
            onUpdateError?.Invoke("appUpdateManager or appUpdateInfo is NULL");
            Debug.LogError(TAG + "appUpdateManager or appUpdateInfo is NULL");
            yield break;
        }

        ZenAnalytics.LogEvent("iau_update_start");
        updateStatus.text = "Start update...";
        Debug.Log(TAG + "StartUpdate");
        // Creates an AppUpdateOptions for an immediate flow that allows asset pack deletion.
        var appUpdateOptions = AppUpdateOptions.ImmediateAppUpdateOptions(allowAssetPackDeletion: true);

        // Creates an AppUpdateRequest that can be used to monitor the requested in-app update flow.
        var startUpdateRequest = appUpdateManager.StartUpdate(
          // The result returned by PlayAsyncOperation.GetResult().
          appUpdateInfo,
          // The AppUpdateOptions created defining the requested in-app update and its parameters.
          appUpdateOptions);

        if (startUpdateRequest == null)
        {
            onUpdateError?.Invoke("Update StartUpdate is NULL");
            Debug.LogError(TAG + "Update StartUpdate is NULL");
            yield break;
        }

        yield return startUpdateRequest;

        while (!startUpdateRequest.IsDone)
        {
            // For flexible flow,the user can continue to use the app while the update downloads in the background. You can implement a progress bar showing the download status during this time.
            updateStatus.text = "Update " + startUpdateRequest.Status.ToString() + " Download Process " + startUpdateRequest.DownloadProgress.ToString("#0.0");
            Debug.Log(TAG + "StartUpdate: " + startUpdateRequest.Status.ToString() + " " + startUpdateRequest.DownloadProgress.ToString("#0.0"));
            yield return new WaitForSeconds(0.25f);
        }

        if (startUpdateRequest.Status == AppUpdateStatus.Failed)
        {
            string error = startUpdateRequest?.Error.ToString();
            ZenAnalytics.LogEvent("iau_update_request_" + error);
            updateStatus.text = "UPDATE REQUEST ERROR CODE: " + error;
            Debug.LogError(TAG + "UPDATE REQUEST ERROR CODE: " + error); // Log appUpdateInfoOperation.Error.
            yield return new WaitForSeconds(1);
            onUpdateError?.Invoke(error);
            yield break;
        }

        if (appUpdateManager == null)
        {
            onUpdateError?.Invoke("appUpdateManager is NULL");
            Debug.LogError(TAG + "appUpdateManager is NULL");
            yield break;
        }

        updateStatus.text = "Get update result...";
        Debug.Log(TAG + "GetUpdateResult");
        // If the update completes successfully, then the app restarts and this line is never reached. If this line is reached, then handle the failure (for example, by logging result.Error or by displaying a message to the user).
        var result = appUpdateManager.CompleteUpdate();

        if (result == null)
        {
            onUpdateError?.Invoke("Update result is NULL");
            Debug.LogError(TAG + "Update result is NULL");
            yield break;
        }

        yield return result;

        if (result.IsSuccessful)
        {
            ZenAnalytics.LogEvent("iau_update_success");
            updateStatus.text = "Update Success!";
            Debug.Log(TAG + "SUCCESS");
            yield return new WaitForSeconds(1);
            onUpdateError?.Invoke(null);
        }
        else
        {
            string error = result?.Error.ToString();
            ZenAnalytics.LogEvent("iau_update_" + error);
            updateStatus.text = "ERROR CODE: " + error;
            Debug.LogError(TAG + "ERROR: " + error); // Log appUpdateInfoOperation.Error.
            yield return new WaitForSeconds(1);
            onUpdateError?.Invoke(error);
        }
#else
        Debug.LogWarning(TAG + "NOT SUPPORT PLATFORM");
        onUpdateError?.Invoke("Not support platform");
        yield break;
#endif


    }

    private void OnDestroy()
    {
        StopAllCoroutines();
    }
}
