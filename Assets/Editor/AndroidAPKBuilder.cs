using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using System.IO;

public class AndroidAPKBuilder
{
    [MenuItem("Tools/Build Android APK")]
    public static void BuildAPK()
    {
        Debug.Log("[AndroidAPKBuilder] Starting Android APK build...");

        // ── Set Android App Icon programmatically ──
        SetAndroidIcon();

        // Ensure output directory exists
        string buildDir = Path.Combine(Application.dataPath, "../Builds/Android");
        if (!Directory.Exists(buildDir))
        {
            Directory.CreateDirectory(buildDir);
        }

        string outputPath = Path.Combine(buildDir, "Greysword.apk");

        // Select scenes to build
        string[] scenes = new string[] { "Assets/Scenes/SampleScene.unity" };

        // Configure build settings
        BuildPlayerOptions buildPlayerOptions = new BuildPlayerOptions();
        buildPlayerOptions.scenes = scenes;
        buildPlayerOptions.locationPathName = outputPath;
        buildPlayerOptions.target = BuildTarget.Android;
        buildPlayerOptions.options = BuildOptions.None;

        // Force switch to Android target group before building
        EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);

        // Run the build
        BuildReport report = BuildPipeline.BuildPlayer(buildPlayerOptions);
        BuildSummary summary = report.summary;

        if (summary.result == BuildResult.Succeeded)
        {
            Debug.Log("[AndroidAPKBuilder] Build Succeeded! APK saved to: " + outputPath);
            EditorUtility.RevealInFinder(outputPath);
        }
        else if (summary.result == BuildResult.Failed)
        {
            Debug.LogError("[AndroidAPKBuilder] Build Failed!");
        }
    }

    private static void SetAndroidIcon()
    {
        // Load the icon texture from Assets/Icons/app_icon.png
        Texture2D icon = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Icons/app_icon.png");
        if (icon == null)
        {
            Debug.LogWarning("[AndroidAPKBuilder] Could not load icon from Assets/Icons/app_icon.png");
            return;
        }

        // Ensure the texture is imported as a readable icon
        string iconPath = AssetDatabase.GetAssetPath(icon);
        TextureImporter importer = AssetImporter.GetAtPath(iconPath) as TextureImporter;
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Default;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.isReadable = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 1024;
            importer.SaveAndReimport();
        }

        // Reload after reimport
        icon = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Icons/app_icon.png");
        if (icon == null) return;

        // 1. Set general application icons for Android target group
        var namedTarget = UnityEditor.Build.NamedBuildTarget.Android;
        int[] sizes = PlayerSettings.GetIconSizes(namedTarget, IconKind.Application);
        Texture2D[] icons = new Texture2D[sizes.Length];
        for (int i = 0; i < icons.Length; i++)
        {
            icons[i] = icon;
        }
        PlayerSettings.SetIcons(namedTarget, icons, IconKind.Application);

        // 2. Set default/unknown target icons
        var defaultTarget = UnityEditor.Build.NamedBuildTarget.Unknown;
        int[] defaultSizes = PlayerSettings.GetIconSizes(defaultTarget, IconKind.Application);
        Texture2D[] defaultIcons = new Texture2D[defaultSizes.Length];
        for (int i = 0; i < defaultIcons.Length; i++)
        {
            defaultIcons[i] = icon;
        }
        PlayerSettings.SetIcons(defaultTarget, defaultIcons, IconKind.Application);

        // 3. Set Android Platform Icons (Adaptive, Round, Legacy) using UnityEditor.Android
        try
        {
            // Set Legacy Android Icons
            PlatformIcon[] legacyIcons = PlayerSettings.GetPlatformIcons(namedTarget, UnityEditor.Android.AndroidPlatformIconKind.Legacy);
            for (int i = 0; i < legacyIcons.Length; i++)
            {
                legacyIcons[i].SetTexture(icon);
            }
            PlayerSettings.SetPlatformIcons(namedTarget, UnityEditor.Android.AndroidPlatformIconKind.Legacy, legacyIcons);

            // Set Round Android Icons
            PlatformIcon[] roundIcons = PlayerSettings.GetPlatformIcons(namedTarget, UnityEditor.Android.AndroidPlatformIconKind.Round);
            for (int i = 0; i < roundIcons.Length; i++)
            {
                roundIcons[i].SetTexture(icon);
            }
            PlayerSettings.SetPlatformIcons(namedTarget, UnityEditor.Android.AndroidPlatformIconKind.Round, roundIcons);

            // Set Adaptive Android Icons (using the same icon for foreground and background layers)
            PlatformIcon[] adaptiveIcons = PlayerSettings.GetPlatformIcons(namedTarget, UnityEditor.Android.AndroidPlatformIconKind.Adaptive);
            for (int i = 0; i < adaptiveIcons.Length; i++)
            {
                adaptiveIcons[i].SetTextures(new Texture2D[] { icon, icon });
            }
            PlayerSettings.SetPlatformIcons(namedTarget, UnityEditor.Android.AndroidPlatformIconKind.Adaptive, adaptiveIcons);
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning("[AndroidAPKBuilder] Exception while setting platform-specific Android icons: " + ex.Message);
        }

        Debug.Log("[AndroidAPKBuilder] Android icon set successfully from: " + iconPath);
    }
}
