using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEditor.PackageManager.UI;
using UnityEngine;
using UnityEngine.Rendering;

namespace CricketVR.Editor
{
    /// <summary>
    /// First-run project setup that must work before any XR package is installed,
    /// so this assembly deliberately references nothing outside UnityEditor/UnityEngine.
    /// Every step is idempotent: re-running it only fills in what is missing.
    /// </summary>
    public static class ProjectBootstrap
    {
        // No versions: Package Manager picks the release recommended for the installed Editor.
        // The resolved versions are recorded in Packages/packages-lock.json and the README.
        static readonly string[] RequiredPackages =
        {
            "com.unity.inputsystem",
            "com.unity.xr.management",
            "com.unity.xr.openxr",
            "com.unity.xr.interaction.toolkit",
            "com.unity.test-framework",
        };

        const string XriPackage = "com.unity.xr.interaction.toolkit";
        const string StarterAssetsSample = "Starter Assets";

        static AddAndRemoveRequest _packageRequest;
        static bool _exitWhenDone;

        [MenuItem("CricketVR/Setup/1. Install Required Packages")]
        public static void InstallPackages()
        {
            if (_packageRequest != null && !_packageRequest.IsCompleted)
            {
                Debug.Log("[CricketVR] Package installation already in progress.");
                return;
            }

            Debug.Log("[CricketVR] Installing packages: " + string.Join(", ", RequiredPackages));
            _packageRequest = Client.AddAndRemove(RequiredPackages);
            EditorApplication.update += PollPackageRequest;
        }

        /// <summary>Batch-mode entry: -executeMethod CricketVR.Editor.ProjectBootstrap.InstallPackagesBatch</summary>
        public static void InstallPackagesBatch()
        {
            _exitWhenDone = true;
            InstallPackages();
        }

        static void PollPackageRequest()
        {
            if (!_packageRequest.IsCompleted)
                return;

            EditorApplication.update -= PollPackageRequest;
            bool ok = _packageRequest.Status == StatusCode.Success;
            if (ok)
            {
                foreach (var p in _packageRequest.Result.Where(p => RequiredPackages.Contains(p.name)))
                    Debug.Log($"[CricketVR] Installed {p.name} {p.version}");
                Debug.Log("[CricketVR] Packages installed. Next: CricketVR > Setup > 2. Import XRI Starter Assets.");
            }
            else
            {
                Debug.LogError("[CricketVR] Package installation failed: " + _packageRequest.Error?.message);
            }

            if (_exitWhenDone)
                EditorApplication.Exit(ok ? 0 : 1);
        }

        [MenuItem("CricketVR/Setup/2. Import XRI Starter Assets")]
        public static void ImportStarterAssets()
        {
            var sample = Sample.FindByPackage(XriPackage, null)
                .FirstOrDefault(s => s.displayName == StarterAssetsSample);

            if (string.IsNullOrEmpty(sample.displayName))
            {
                Debug.LogError($"[CricketVR] '{StarterAssetsSample}' sample not found. Run step 1 first and wait for package import to finish.");
                return;
            }

            if (sample.isImported)
            {
                Debug.Log($"[CricketVR] '{StarterAssetsSample}' already imported at {sample.importPath}.");
                return;
            }

            if (sample.Import(Sample.ImportOptions.HideImportWindow))
                Debug.Log($"[CricketVR] Imported '{StarterAssetsSample}' to {sample.importPath}.");
            else
                Debug.LogError($"[CricketVR] Failed to import '{StarterAssetsSample}'.");
        }

        /// <summary>Batch-mode entry: -executeMethod CricketVR.Editor.ProjectBootstrap.ImportStarterAssetsBatch</summary>
        public static void ImportStarterAssetsBatch()
        {
            ImportStarterAssets();
            AssetDatabase.Refresh();
        }

        [MenuItem("CricketVR/Setup/3. Apply Quest Player Settings")]
        public static void ApplyQuestPlayerSettings()
        {
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
                EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
            // Some OpenXR validation rules read the *selected* group (the Build Settings UI selection),
            // which batch mode leaves at Standalone; keep it in step with the active target.
            EditorUserBuildSettings.selectedBuildTargetGroup = BuildTargetGroup.Android;

            PlayerSettings.companyName = "CricketVR";
            PlayerSettings.productName = "CricketVR";
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.cricketvr.prototype");

            // Quest 3 is a 64-bit Android device; IL2CPP is required for ARM64.
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            // Meta Quest OpenXR validation requires API level 32 (Android 12L) or higher.
            if (PlayerSettings.Android.minSdkVersion < AndroidSdkVersions.AndroidApiLevel32)
                PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel32;

            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.Vulkan });
            EditorUserBuildSettings.androidBuildSubtarget = MobileTextureSubtarget.ASTC;

            // Text serialization keeps scenes/prefabs reviewable in Git.
            EditorSettings.serializationMode = SerializationMode.ForceText;
            VersionControlSettings.mode = "Visible Meta Files";

            AssetDatabase.SaveAssets();
            Debug.Log("[CricketVR] Applied Quest player settings (Android, IL2CPP, ARM64, API 32+, Vulkan, Linear, ASTC).");
        }
    }
}
