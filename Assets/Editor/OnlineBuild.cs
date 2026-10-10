using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Origins.Editor
{
    public static class OnlineBuild
    {
        [MenuItem("Истоки/Онлайн/Собрать браузерную игру")]
        public static void Build()
        {
            ProjectSetup.Validate(); MultiplayerChecks.Run();
            PlayerSettings.WebGL.template="PROJECT:Origins";
            PlayerSettings.WebGL.compressionFormat=WebGLCompressionFormat.Gzip;
            PlayerSettings.WebGL.decompressionFallback=true;
            PlayerSettings.WebGL.dataCaching=true;
            PlayerSettings.WebGL.nameFilesAsHashes=true;
            PlayerSettings.WebGL.initialMemorySize=256;
            PlayerSettings.WebGL.maximumMemorySize=1536;
            PlayerSettings.runInBackground=true;
            Directory.CreateDirectory("Online/Server/wwwroot");
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes=new[]{"Assets/Scenes/Origins.unity"},locationPathName="Online/Server/wwwroot",target=BuildTarget.WebGL,options=BuildOptions.None });
            if(report.summary.result!=UnityEditor.Build.Reporting.BuildResult.Succeeded) throw new Exception("Web build failed: "+report.summary.result);
            Directory.CreateDirectory("Online/Server/wwwroot/licenses");
            File.Copy("Docs/ThirdParty/NotoSans-LICENSE.txt","Online/Server/wwwroot/licenses/NotoSans-LICENSE.txt",true);
            Debug.Log("ORIGINS ONLINE: WebGL build succeeded");
        }
    }
}
