using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Origins.Editor
{
    [InitializeOnLoad]
    public static class ProjectSetup
    {
        static ProjectSetup() { EditorApplication.delayCall+=EnsureScene; }
        static void EnsureScene()
        {
            EnsureMaterial();
            if(EditorApplication.isPlayingOrWillChangePlaymode || File.Exists("Assets/Scenes/Origins.unity")) return;
            CreateScene();
        }
        [MenuItem("Истоки/Открыть стартовую сцену")]
        public static void OpenScene()
        {
            if(!File.Exists("Assets/Scenes/Origins.unity")) CreateScene();
            EditorSceneManager.OpenScene("Assets/Scenes/Origins.unity");
        }
        public static void CreateScene()
        {
            EnsureMaterial();
            Directory.CreateDirectory("Assets/Scenes");
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            new GameObject("Origins").AddComponent<OriginsController>();
            EditorSceneManager.SaveScene(scene,"Assets/Scenes/Origins.unity");
            EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene("Assets/Scenes/Origins.unity",true)};
            PlayerSettings.companyName="Origins"; PlayerSettings.productName="Истоки";
            PlayerSettings.defaultScreenWidth=1600; PlayerSettings.defaultScreenHeight=900;
            PlayerSettings.runInBackground=true;
            PlayerSettings.fullScreenMode=FullScreenMode.FullScreenWindow;
            PlayerSettings.defaultIsNativeResolution=true;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64,false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneWindows64,new[]{UnityEngine.Rendering.GraphicsDeviceType.Direct3D11});
            PlayerSettings.colorSpace=ColorSpace.Linear;
            AssetDatabase.SaveAssets();
        }
        [MenuItem("Истоки/Проверить правила и генерацию")]
        public static void Validate()
        {
            ModelChecks.Run();
            Debug.Log("ORIGINS: all model checks passed");
        }
        [MenuItem("Истоки/Собрать игру для Windows")]
        public static void BuildWindows()
        {
            EnsureMaterial();
            PlayerSettings.fullScreenMode=FullScreenMode.FullScreenWindow;
            PlayerSettings.defaultIsNativeResolution=true;
            Validate();
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64,false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneWindows64,new[]{UnityEngine.Rendering.GraphicsDeviceType.Direct3D11});
            if(!File.Exists("Assets/Scenes/Origins.unity")) CreateScene();
            Directory.CreateDirectory("Builds/Windows");
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes=new[]{"Assets/Scenes/Origins.unity"},locationPathName="Builds/Windows/Origins.exe",target=BuildTarget.StandaloneWindows64,options=BuildOptions.None });
            if(report.summary.result!=UnityEditor.Build.Reporting.BuildResult.Succeeded) throw new Exception("Windows build failed: "+report.summary.result);
            Debug.Log("ORIGINS: Windows build succeeded");
        }
        public static void ValidateBatch() { Validate(); CreateScene(); }
        static void EnsureMaterial()
        {
            if(File.Exists("Assets/Resources/WorldSurface.mat")) return;
            Directory.CreateDirectory("Assets/Resources");
            var surface=new Material(Shader.Find("Standard"));
            surface.SetFloat("_Glossiness",.08f);
            AssetDatabase.CreateAsset(surface,"Assets/Resources/WorldSurface.mat");
            AssetDatabase.SaveAssets();
        }
    }
}
