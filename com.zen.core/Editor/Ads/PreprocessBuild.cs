using Base.Ads;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor;
using UnityEngine;

public class PreprocessBuild : IPreprocessBuildWithReport
{
    public int callbackOrder { get { return 0; } }

    public void OnPreprocessBuild(BuildReport report)
    {
#if USE_APPSFLYER && UNITY_IOS
        Debug.LogWarning("Make sure the APPLE App ID is correctly set in AdConfig!");
#endif
    }
}
