using UnityEngine;
using System.Collections;

#if UNITY_IOS
/*
1. In the Unity Editor, select Window > Package Manager to open the Package Manager.
2. Select the iOS 14 Advertising Support package from the list, then select the most recent verified version.
3. Click the Install or Update button.
*/

using System;
using UnityEngine.iOS;
using Unity.Advertisement.IosSupport;
#endif

#if USE_FACEBOOK && UNITY_IOS
using System.Runtime.InteropServices;

public static class AudienceNetworkSettings
{
    [DllImport("__Internal")]
    private static extern void FBAdSettingsBridgeSetAdvertiserTrackingEnabled(bool advertiserTrackingEnabled);

    public static void SetAdvertiserTrackingEnabled(bool advertiserTrackingEnabled)
    {
        FBAdSettingsBridgeSetAdvertiserTrackingEnabled(advertiserTrackingEnabled);
        Debug.Log("FBAdSettingsBridgeSetAdvertiserTrackingEnabled: " + advertiserTrackingEnabled);
    }
}
#endif

public class ATTHelper : MonoBehaviour
{
    protected static ATTHelper instance = null;

    private static ATTStatus status = ATTStatus.NOT_DETERMINED;

    internal static ATTStatus Status
    {
        get
        {
#if UNITY_IOS
            return status;
#else
            status = (ATTStatus)PlayerPrefs.GetInt("ATTStatus", (int)ATTStatus.NOT_DETERMINED);
            return status;
#endif
        }
        set
        {
            if (value != status)
            {
                status = value;
                PlayerPrefs.SetInt("ATTStatus", (int)status);
            }
        }
    }
    private void Awake()
    {
        instance = this;
    }

    public static IEnumerator IECheck()
    {
#if UNITY_IOS
        Version currentVersion = new Version("15.6");
        try
        {
            currentVersion = new Version(Device.systemVersion);
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
        }

        status = (ATTStatus)ATTrackingStatusBinding.GetAuthorizationTrackingStatus();

        Debug.LogWarning("[ATTHelper] GetAuthorizationTrackingStatus: " + status.ToString() + " iOS Version: " + currentVersion);

        yield return new WaitForSeconds(0.25f);

        if (status == (ATTStatus)ATTrackingStatusBinding.AuthorizationTrackingStatus.NOT_DETERMINED)
        {
            //Change iOS in com.unity.ads.ios-support from  1.0.0 to 1.2.0 in "Packages/manifest.json" and "Packages/packages-lock.json"
            ATTrackingStatusBinding.RequestAuthorizationTracking((success) =>
            {
                status = (ATTStatus)ATTrackingStatusBinding.GetAuthorizationTrackingStatus(); ;
                Debug.LogWarning("[ATTHelper] RequestAuthorizationTracking: " + status.ToString() + " success: " + success.ToString());

#if USE_FACEBOOK
                if (status == ATTStatus.AUTHORIZED)
                    AudienceNetworkSettings.SetAdvertiserTrackingEnabled(true);
                else
                    AudienceNetworkSettings.SetAdvertiserTrackingEnabled(false);
#endif
            });
        }

        float timeOut = 1.0f;
        while (timeOut > 0 && status == ATTStatus.NOT_DETERMINED)
        {
            timeOut -= Time.deltaTime;
            yield return null;
        }

        yield return new WaitForSeconds(0.25f);
        status = (ATTStatus)ATTrackingStatusBinding.GetAuthorizationTrackingStatus(); ;
        Debug.LogWarning("[ATTHelper] WaitForSeconds: " + status.ToString());
#endif

        yield return new WaitForSeconds(0.25f);
    }
}

public enum ATTStatus
{
    NOT_DETERMINED = 0,
    RESTRICTED,
    DENIED,
    AUTHORIZED
}