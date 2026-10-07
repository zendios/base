#if UNITY_EDITOR && UNITY_IOS
using System;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;
using UnityEngine;

/// <summary>
/// Optimus Pro: Ultimate Lean Architecture for Unity 6.4+
/// Resolves ITMS-90208, ITMS-90428, and missing dSYMs without over-engineering.
/// </summary>
public static class iOSBuildPostProcess
{
    // Idempotent signature to prevent redundant injections in 'Append' build mode.
    private const string OptimusHookMark = "# [OPTIMUS_PRO_HOOK]";

    // Separate Podfile logic to run BEFORE EDM4U triggers 'pod install' (usually around order 40-50).
    [PostProcessBuild(10)]
    public static void OnPostprocessPodfile(BuildTarget buildTarget, string pathToXcode)
    {
        if (buildTarget != BuildTarget.iOS) return;
        
        string targetOSVersion = PlayerSettings.iOS.targetOSVersionString;
        EnforcePodfile(pathToXcode, targetOSVersion);
    }

    // Executed at int.MaxValue to ensure this is the final modification in the build pipeline.
    [PostProcessBuild(int.MaxValue)]
    public static void OnPostprocessBuild(BuildTarget buildTarget, string pathToXcode)
    {
        if (buildTarget != BuildTarget.iOS) return;

        Debug.Log("[iOSBuildPostProcess] Starting ultimate lean architecture execution...");

        try
        {
            string targetOSVersion = PlayerSettings.iOS.targetOSVersionString;
            
            UpdateInfoPList(pathToXcode, targetOSVersion);
            UpdatePBXProject(pathToXcode, targetOSVersion);
            
            // Hard-sync all pre-compiled frameworks to pass Apple's strict validation
            SynchronizeFrameworksPLists(pathToXcode, targetOSVersion);
            
            Debug.Log("[iOSBuildPostProcess] Build configuration completed securely.");
        }
        catch (Exception e)
        {
            // Defensive programming: prevent silent failures in automated pipelines.
            Debug.LogError($"[iOSBuildPostProcess] CRITICAL FAILURE: {e.Message}\n{e.StackTrace}");
        }
    }

    private static void UpdateInfoPList(string buildPath, string targetOSVersion)
    {
        string plistPath = Path.Combine(buildPath, "Info.plist");
        if (!File.Exists(plistPath))
        {
            Debug.LogError("[iOSBuildPostProcess] Info.plist not found.");
            return;
        }

        PlistDocument plistObj = new PlistDocument();
        plistObj.ReadFromString(File.ReadAllText(plistPath));
        PlistElementDict plistRoot = plistObj.root;

        // 1. Force the MinimumOSVersion to match Unity Player Settings to hard-fix ITMS-90208 at the root.
        plistRoot.SetString("MinimumOSVersion", targetOSVersion);

        // 2. Apply necessary privacy descriptions and standard configurations.
        plistRoot.SetString("NSUserTrackingUsageDescription", "This game includes ads. To improve your experience and see ads that match your interests, allow tracking.");
        plistRoot.SetString("NSCalendarsUsageDescription", "$(PRODUCT_NAME) uses your calendar.");
        plistRoot.SetString("NSLocationAlwaysUsageDescription", "$(PRODUCT_NAME) uses your location.");
        plistRoot.SetString("NSLocationWhenInUseUsageDescription", "$(PRODUCT_NAME) uses your location.");
        plistRoot.SetBoolean("ITSAppUsesNonExemptEncryption", false);
        plistRoot.SetBoolean("AppsFlyerShouldSwizzle", true);
        plistRoot.SetString("NSAdvertisingAttributionReportEndpoint", "https://appsflyer-skadnetwork.com/");
        plistRoot.SetBoolean("FirebaseAutomaticScreenReportingEnabled", false);

        File.WriteAllText(plistPath, plistObj.WriteToString());
        Debug.Log($"[iOSBuildPostProcess] Info.plist updated: MinimumOSVersion synced to {targetOSVersion}.");
    }

    private static void UpdatePBXProject(string buildPath, string targetOSVersion)
    {
        string projectPath = PBXProject.GetPBXProjectPath(buildPath);
        if (!File.Exists(projectPath))
        {
            Debug.LogError("[iOSBuildPostProcess] PBXProject file not found.");
            return;
        }

        PBXProject pbxProject = new PBXProject();
        pbxProject.ReadFromString(File.ReadAllText(projectPath));

        // Unity 6.x specific architecture: Targeting all split assemblies.
        string mainTargetGuid = pbxProject.GetUnityMainTargetGuid();
        string frameworkTargetGuid = pbxProject.GetUnityFrameworkTargetGuid();
        string testTargetGuid = pbxProject.TargetGuidByName(PBXProject.GetUnityTestTargetName());
        string runtimeTargetGuid = pbxProject.TargetGuidByName("UnityRuntime");     // Crucial for Unity 6+
        string assemblyTargetGuid = pbxProject.TargetGuidByName("GameAssembly");   // Crucial for Unity 6+

        // Configuration for all existing targets to ensure absolute consistency.
        string[] allTargets = { mainTargetGuid, frameworkTargetGuid, testTargetGuid, runtimeTargetGuid, assemblyTargetGuid };

        foreach (string targetGuid in allTargets)
        {
            if (string.IsNullOrEmpty(targetGuid)) continue;

            // Fix ITMS-90428: Prevent unnecessary Swift library embedding. Native to OS since iOS 12.2.
            pbxProject.SetBuildProperty(targetGuid, "ALWAYS_EMBED_SWIFT_STANDARD_LIBRARIES", "NO");

            // Fix ITMS-90208: Enforce deployment target synchronization across all sub-targets.
            pbxProject.SetBuildProperty(targetGuid, "IPHONEOS_DEPLOYMENT_TARGET", targetOSVersion);

            // Fix Symbol Uploads: Force dSYM generation for crash analysis and prevent stripping.
            pbxProject.SetBuildProperty(targetGuid, "DEBUG_INFORMATION_FORMAT", "dwarf-with-dsym");
            pbxProject.SetBuildProperty(targetGuid, "GCC_GENERATE_DEBUGGING_SYMBOLS", "YES");

            // Complete lockdown on stripping phases to guarantee dSYM preservation
            pbxProject.SetBuildProperty(targetGuid, "STRIP_INSTALLED_PRODUCT", "NO");
            pbxProject.SetBuildProperty(targetGuid, "COPY_PHASE_STRIP", "NO");
            pbxProject.SetBuildProperty(targetGuid, "DEPLOYMENT_POSTPROCESSING", "NO");
        }

        pbxProject.WriteToFile(projectPath);
        Debug.Log("[iOSBuildPostProcess] PBXProject updated: Synced UnityRuntime & GameAssembly, Anti-Stripping applied.");
    }

    private static void EnforcePodfile(string buildPath, string targetOSVersion)
    {
        string podfilePath = Path.Combine(buildPath, "Podfile");
        if (!File.Exists(podfilePath))
        {
            Debug.Log("[iOSBuildPostProcess] No Podfile detected. Skipping CocoaPods configuration.");
            return;
        }

        string podfileContent = File.ReadAllText(podfilePath);

        // Idempotency check: bypass if the hook is already present to protect 'Append Build' integrity.
        if (podfileContent.Contains(OptimusHookMark))
        {
            Debug.Log("[iOSBuildPostProcess] Optimus hook already exists in Podfile. Skipping injection.");
            return;
        }

        // Ruby payload: Enforce OS Version, generate dSYM, and STRICTLY prevent Xcode from stripping pre-compiled frameworks.
        string hookPayload = $@"
  {OptimusHookMark} Auto-enforced configurations for App Store compliance
  installer.pods_project.targets.each do |target|
    target.build_configurations.each do |config|
      config.build_settings['IPHONEOS_DEPLOYMENT_TARGET'] = '{targetOSVersion}'
      config.build_settings['DEBUG_INFORMATION_FORMAT'] = 'dwarf-with-dsym'
      config.build_settings['STRIP_INSTALLED_PRODUCT'] = 'NO'
      config.build_settings['STRIP_STYLE'] = 'debugging'
      config.build_settings['COPY_PHASE_STRIP'] = 'NO'
      config.build_settings['DEPLOYMENT_POSTPROCESSING'] = 'NO'
    end
  end
";

        // Regex to find active 'post_install' blocks, safely avoiding commented lines.
        string postInstallRegex = @"^(?!\s*#)\s*post_install\s+do\s+\|installer\|";
        
        if (Regex.IsMatch(podfileContent, postInstallRegex, RegexOptions.Multiline))
        {
            string replacement = $"post_install do |installer|\n{hookPayload}";
            podfileContent = Regex.Replace(podfileContent, postInstallRegex, replacement, RegexOptions.Multiline);
            Debug.Log("[iOSBuildPostProcess] Injected anti-stripping and dSYM configurations into existing Podfile hook.");
        }
        else
        {
            string fullHook = $"\npost_install do |installer|\n{hookPayload}end\n";
            podfileContent += fullHook;
            Debug.Log("[iOSBuildPostProcess] Appended new post_install hook to Podfile.");
        }

        File.WriteAllText(podfilePath, podfileContent);
    }

    // Hard-syncs MinimumOSVersion for all embedded frameworks to obliterate ITMS-90208
    private static void SynchronizeFrameworksPLists(string buildPath, string targetOSVersion)
    {
        try
        {
            // Recursively locate all Info.plist files inside the Xcode project
            string[] plistFiles = Directory.GetFiles(buildPath, "Info.plist", SearchOption.AllDirectories);
            
            foreach (string plistPath in plistFiles)
            {
                // We only care about Info.plist files residing within a .framework bundle
                if (plistPath.Contains(".framework"))
                {
                    PlistDocument plist = new PlistDocument();
                    plist.ReadFromFile(plistPath);
                    
                    // If the framework specifies a MinimumOSVersion, enforce our target OS version
                    if (plist.root.values.ContainsKey("MinimumOSVersion"))
                    {
                        string currentVersion = plist.root.values["MinimumOSVersion"].AsString();
                        if (currentVersion != targetOSVersion)
                        {
                            plist.root.SetString("MinimumOSVersion", targetOSVersion);
                            plist.WriteToFile(plistPath);
                            Debug.Log($"[OptimusPro] Hard-synced MinimumOSVersion ({currentVersion} -> {targetOSVersion}) for framework: {plistPath}");
                        }
                    }
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[iOSBuildPostProcess] Error synchronizing framework PLists: {e.Message}");
        }
    }
}
#endif