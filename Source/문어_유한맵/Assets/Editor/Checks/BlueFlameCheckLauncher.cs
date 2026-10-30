using System.IO;
using UnityEditor;
using UnityEngine;

namespace OctopusSurvivors.EditorTools
{
    [InitializeOnLoad]
    public static class BlueFlameCheckLauncher
    {
        static BlueFlameCheckLauncher()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.EnteredEditMode)
                    SessionState.SetBool("Octopus.BlueFlameCheck",false);
            };
        }
        [MenuItem("문어/청염 개발 검증")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
            ShaderUtil.allowAsyncCompilation = false;
            SystemDataChecks.Run();
            Directory.CreateDirectory(Application.dataPath+"/../Logs");
            File.WriteAllText(Application.dataPath+"/../Logs/blueflame-check.status","RUNNING");
            using (var socket = new System.Net.Sockets.UdpClient(new System.Net.IPEndPoint(System.Net.IPAddress.Loopback,0)))
                SessionState.SetInt("Octopus.BlueFlamePort",((System.Net.IPEndPoint)socket.Client.LocalEndPoint).Port);
            SessionState.SetBool("Octopus.BlueFlameCheck", true);
            EditorApplication.EnterPlaymode();
        }
    }
}
