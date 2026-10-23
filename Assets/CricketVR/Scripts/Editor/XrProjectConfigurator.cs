using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEditor.XR.OpenXR;
using UnityEditor.XR.OpenXR.Features;
using UnityEngine;
using UnityEngine.XR.Management;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features;

namespace CricketVR.Editor
{
    /// <summary>
    /// Enables the OpenXR loader for Android and the Meta Quest feature/controller profiles.
    /// Idempotent: existing settings are only added to, never reset.
    /// </summary>
    public static class XrProjectConfigurator
    {
        const string OpenXRLoaderType = "UnityEngine.XR.OpenXR.OpenXRLoader";
        const string SettingsFolder = "Assets/XR";

        // Matched by type name so this file doesn't need compile-time references
        // to the separate Meta Quest feature assembly.
        static readonly string[] RequiredFeatureTypes =
        {
            "MetaQuestFeature",
            "OculusTouchControllerProfile",
            "MetaQuestTouchPlusControllerProfile",
        };

        [MenuItem("CricketVR/Setup/4. Configure OpenXR for Quest")]
        public static void Configure()
        {
            var group = BuildTargetGroup.Android;

            if (!EditorBuildSettings.TryGetConfigObject(XRGeneralSettings.k_SettingsKey, out XRGeneralSettingsPerBuildTarget perTarget) || perTarget == null)
            {
                if (!AssetDatabase.IsValidFolder(SettingsFolder))
                    AssetDatabase.CreateFolder("Assets", "XR");
                perTarget = ScriptableObject.CreateInstance<XRGeneralSettingsPerBuildTarget>();
                AssetDatabase.CreateAsset(perTarget, SettingsFolder + "/XRGeneralSettingsPerBuildTarget.asset");
                EditorBuildSettings.AddConfigObject(XRGeneralSettings.k_SettingsKey, perTarget, true);
            }

            if (!perTarget.HasSettingsForBuildTarget(group))
                perTarget.CreateDefaultSettingsForBuildTarget(group);
            if (!perTarget.HasManagerSettingsForBuildTarget(group))
                perTarget.CreateDefaultManagerSettingsForBuildTarget(group);

            var general = perTarget.SettingsForBuildTarget(group);
            general.InitManagerOnStart = true;
            var manager = general.Manager;
            if (!manager.activeLoaders.Any(l => l != null && l.GetType().FullName == OpenXRLoaderType))
            {
                if (XRPackageMetadataStore.AssignLoader(manager, OpenXRLoaderType, group))
                    Debug.Log("[CricketVR] Assigned OpenXR loader for Android.");
                else
                    Debug.LogError("[CricketVR] Could not assign the OpenXR loader. Open Edit > Project Settings > XR Plug-in Management, Android tab, and tick OpenXR.");
            }

            FeatureHelpers.RefreshFeatures(group);
            var openXr = OpenXRSettings.GetSettingsForBuildTargetGroup(group);
            if (openXr == null)
            {
                Debug.LogError("[CricketVR] OpenXR settings for Android not found yet. Open Edit > Project Settings > XR Plug-in Management > OpenXR once, then re-run this step.");
                return;
            }

            openXr.renderMode = OpenXRSettings.RenderMode.SinglePassInstanced;
            // Poll controller input as late as possible each frame: lower bat-tracking latency.
            openXr.latencyOptimization = OpenXRSettings.LatencyOptimization.PrioritizeInputPolling;
            var features = openXr.GetFeatures();
            foreach (var typeName in RequiredFeatureTypes)
            {
                var feature = features.FirstOrDefault(f => f != null && f.GetType().Name == typeName);
                if (feature == null)
                {
                    Debug.LogError($"[CricketVR] OpenXR feature '{typeName}' not found in this OpenXR version.");
                    continue;
                }
                if (!feature.enabled)
                {
                    feature.enabled = true;
                    EditorUtility.SetDirty(feature);
                    Debug.Log($"[CricketVR] Enabled OpenXR feature {feature.name}.");
                }
            }

            EditorUtility.SetDirty(openXr);
            EditorUtility.SetDirty(general);
            AssetDatabase.SaveAssets();
            ReportValidation(fixAutomatic: true);
        }

        [MenuItem("CricketVR/Setup/5. Report OpenXR Validation Issues")]
        public static void ReportValidationMenu() => ReportValidation(fixAutomatic: false);

        /// <summary>Logs OpenXR project validation issues for Android; optionally applies the automatic fixes.</summary>
        public static int ReportValidation(bool fixAutomatic)
        {
            EditorUserBuildSettings.selectedBuildTargetGroup = BuildTargetGroup.Android;
            var issues = new List<OpenXRFeature.ValidationRule>();
            if (!TryGetIssues(issues))
                return 1;

            if (fixAutomatic)
            {
                foreach (var issue in issues.Where(i => i.fixIt != null && i.fixItAutomatic))
                {
                    Debug.Log("[CricketVR] Auto-fixing: " + issue.message);
                    issue.fixIt();
                }
                if (!TryGetIssues(issues))
                    return 1;
            }

            foreach (var issue in issues)
            {
                var text = $"[CricketVR] OpenXR validation ({(issue.error ? "error" : "warning")}): {issue.message}";
                if (issue.error) Debug.LogError(text); else Debug.LogWarning(text);
            }
            Debug.Log($"[CricketVR] OpenXR validation: {issues.Count(i => i.error)} error(s), {issues.Count(i => !i.error)} warning(s).");
            return issues.Count(i => i.error);
        }

        static bool TryGetIssues(List<OpenXRFeature.ValidationRule> issues)
        {
            try
            {
                OpenXRProjectValidation.GetCurrentValidationIssues(issues, BuildTargetGroup.Android);
                return true;
            }
            catch (System.Exception e)
            {
                Debug.LogError("[CricketVR] OpenXR validation could not run; check Edit > Project Settings > XR Plug-in Management > Project Validation manually.\n" + e);
                return false;
            }
        }

        /// <summary>Batch-mode entry: -executeMethod CricketVR.Editor.XrProjectConfigurator.ConfigureBatch</summary>
        public static void ConfigureBatch()
        {
            ProjectBootstrap.ApplyQuestPlayerSettings();
            Configure();
        }
    }
}
