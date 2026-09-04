using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.Android;
using UnityEngine;
using UnityEngine.Rendering;

public static class BuildAndroidTest
{
    public static void Build()
    {
        const string scenePath = "Assets/Scenes/HelloAndroid.unity";
        const string outputPath = "Builds/HelloAndroid.apk";

        Directory.CreateDirectory("Builds");
        EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
        var localAppData = System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData);
        var toolchainRoot = Path.Combine(localAppData, "Unity", "AndroidToolchain-6000.6.0f1");
        AndroidExternalToolsSettings.jdkRootPath = Path.Combine(toolchainRoot, "OpenJDK");
        AndroidExternalToolsSettings.sdkRootPath = Path.Combine(toolchainRoot, "SDK");
        AndroidExternalToolsSettings.ndkRootPath = Path.Combine(toolchainRoot, "NDK");
        PlayerSettings.productName = "Hello Android";
        PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, "com.example.helloandroid");
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
        PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevel36;
        PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);
        ConvertEmperorMaterialsToStandard();
        IncludeRuntimeShaders();

        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = new[] { scenePath },
            locationPathName = outputPath,
            target = BuildTarget.Android,
            options = BuildOptions.None
        });

        Debug.Log($"Android build result: {report.summary.result}, size: {report.summary.totalSize} bytes");
        if (report.summary.result != BuildResult.Succeeded)
        {
            throw new BuildFailedException("Android build failed: " + report.summary.result);
        }
    }

    private const string EmperorBodyMaterialPath =
        "Assets/Mikhail Nesterov/Emperor Angelfish/Art/Materials/M_Emperor_body.mat";
    private const string EmperorFinsMaterialPath =
        "Assets/Mikhail Nesterov/Emperor Angelfish/Art/Materials/M_Emperor_fins.mat";
    private const string UrpLitShaderLine =
        "m_Shader: {fileID: 4800000, guid: 933532a4fcc9baf4fa0491de14d08ed7, type: 3}";
    private const string StandardShaderLine =
        "m_Shader: {fileID: 46, guid: 0000000000000000f000000000000000, type: 0}";

    private static void ConvertEmperorMaterialsToStandard()
    {
        // The Emperor Angelfish package targets the URP Lit shader, but this project
        // uses the built-in render pipeline. Its materials already carry the matching
        // built-in property names (_MainTex/_Color/_Glossiness...), so switching the
        // shader reference plus a few keyword/float tweaks is enough.
        ConvertMaterialFile(EmperorBodyMaterialPath, 0.6f, false);
        ConvertMaterialFile(EmperorFinsMaterialPath, 0.5f, true);
        AssetDatabase.Refresh();
    }

    private static void ConvertMaterialFile(string path, float glossMapScale, bool alphaBlend)
    {
        if (!File.Exists(path))
        {
            throw new BuildFailedException("Emperor material not found: " + path);
        }

        var text = File.ReadAllText(path).Replace("\r\n", "\n");
        if (!text.Contains(UrpLitShaderLine))
        {
            Debug.Log("Android build: material already converted: " + path);
            return;
        }

        // 1. Point the material at the built-in Standard shader.
        text = text.Replace(UrpLitShaderLine, StandardShaderLine);

        // 2. Use the metallic workflow (drop the legacy specular-setup keyword).
        text = text.Replace("  - _SPECULAR_SETUP\n", "");

        // 3. Reproduce the intended glossiness from the metallic gloss map alpha.
        text = text.Replace("  - _GlossMapScale: 0\n",
            "  - _GlossMapScale: " +
            glossMapScale.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + "\n");

        // 4. Fins were authored as transparent in URP; enable Standard's fade blend.
        if (alphaBlend && !text.Contains("  - _ALPHABLEND_ON\n"))
        {
            text = text.Replace("  - _METALLICSPECGLOSSMAP\n",
                "  - _ALPHABLEND_ON\n  - _METALLICSPECGLOSSMAP\n");
        }

        File.WriteAllText(path, text);
        Debug.Log("Android build: converted material to Standard: " + path);
    }

    private static void IncludeRuntimeShaders()
    {
        var standardShader = Shader.Find("Standard");
        if (standardShader == null)
        {
            throw new BuildFailedException("Could not find the built-in Standard shader in the Unity editor.");
        }

        var graphicsSettingsAssets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset");
        if (graphicsSettingsAssets == null || graphicsSettingsAssets.Length == 0)
        {
            throw new BuildFailedException("Could not load ProjectSettings/GraphicsSettings.asset.");
        }

        var serializedSettings = new SerializedObject(graphicsSettingsAssets[0]);
        var alwaysIncludedShaders = serializedSettings.FindProperty("m_AlwaysIncludedShaders");
        if (alwaysIncludedShaders == null)
        {
            throw new BuildFailedException("Could not find m_AlwaysIncludedShaders in GraphicsSettings.asset.");
        }

        var alreadyIncluded = false;
        for (var i = 0; i < alwaysIncludedShaders.arraySize; i++)
        {
            if (alwaysIncludedShaders.GetArrayElementAtIndex(i).objectReferenceValue == standardShader)
            {
                alreadyIncluded = true;
                break;
            }
        }

        if (!alreadyIncluded)
        {
            alwaysIncludedShaders.InsertArrayElementAtIndex(alwaysIncludedShaders.arraySize);
            alwaysIncludedShaders.GetArrayElementAtIndex(alwaysIncludedShaders.arraySize - 1).objectReferenceValue = standardShader;
            serializedSettings.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
        }

        Debug.Log("Android build: Standard shader added to always included shaders.");
    }
}
