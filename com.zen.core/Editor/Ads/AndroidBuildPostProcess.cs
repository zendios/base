#if UNITY_ANDROID
using System.IO;
using System.Text;
using UnityEditor.Android;
using UnityEngine;

public class AndroidBuildPostProcess : IPostGenerateGradleAndroidProject
{
    public int callbackOrder => 9;

    public void OnPostGenerateGradleAndroidProject(string path)
    {

    }

    public void FixUnityPlayerActivity(string path)
    {
        // src\main\java\com\unity3d\player\UnityPlayerActivity.java
        var activityClassPath = Path.Combine(path, "src", "main", "java", "com", "unity3d", "player", "UnityPlayerActivity.java");
        var activityClassText = File.ReadAllText(activityClassPath);

        Debug.Log("OnPostGenerateGradleAndroidProject: UnityPlayerActivity" + activityClassText.ToString());

        var methodIndex = activityClassText.IndexOf("protected void onDestroy");

        var methodBodyIndex = activityClassText.IndexOf('{', methodIndex);
        var methodEndIndex = activityClassText.IndexOf('}', methodBodyIndex);

        var sb = new StringBuilder(activityClassText.Length + 100);

        sb.AppendLine(activityClassText.Substring(0, methodBodyIndex + 1));

        sb.AppendLine("     mUnityPlayer.removeAllViews();");
        sb.AppendLine("     mUnityPlayer.quit();");
        sb.AppendLine("     super.onDestroy();");

        sb.Append(activityClassText.Substring(methodEndIndex));

        File.WriteAllText(activityClassPath, sb.ToString());

        Debug.Log("OnPostGenerateGradleAndroidProject: UnityPlayerActivity" + sb.ToString());
    }
}
#endif