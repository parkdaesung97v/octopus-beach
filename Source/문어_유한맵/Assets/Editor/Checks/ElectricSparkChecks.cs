using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace OctopusSurvivors.EditorTools
{
    [InitializeOnLoad]
    public static class ElectricSparkChecks
    {
        const string Key = "Octopus.ElectricSparkCheck";
        static ElectricSparkChecks()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Key, false))
                    new GameObject("Electric spark integration checks").AddComponent<ElectricSparkCapture>();
                if (state == PlayModeStateChange.EnteredEditMode) SessionState.SetBool(Key, false);
            };
        }

        [MenuItem("문어/전기 스파크 폭발 검증")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/electric-spark-check.status", "RUNNING");
            File.WriteAllText("Logs/electric-spark-checks.log", "");
            ShaderUtil.allowAsyncCompilation = false;
            SessionState.SetBool(Key, true); EditorApplication.EnterPlaymode();
        }

        internal static void Log(string text)
        {
            Debug.Log(text);
            File.AppendAllText("Logs/electric-spark-checks.log", text + "\n");
        }

        internal static void Finish(bool pass)
        {
            File.WriteAllText("Logs/electric-spark-check.status", pass ? "PASS" : "FAIL");
            EditorApplication.isPlaying = false;
        }
    }

    public sealed class ElectricSparkCapture : MonoBehaviour
    {
        static readonly BindingFlags Hidden = BindingFlags.NonPublic | BindingFlags.Instance;
        IEnumerator run; float deadline; bool done;
        static object Call(object o, string method, params object[] args) => o.GetType().GetMethod(method, Hidden).Invoke(o, args);
        static List<SimActor> Actors(GameSession s) => (List<SimActor>)typeof(GameSession).GetField("_actors", Hidden).GetValue(s);
        static void Check(bool pass, string text)
        {
            if (!pass) throw new Exception(text);
            ElectricSparkChecks.Log("ELECTRIC SPARK PASS: " + text);
        }
        static SimActor Target(GameSession s, Vector2 pos)
        {
            var e = (SimActor)Call(s, "Create", ActorKind.Enemy, pos, .7f);
            e.Pos = pos; e.Hp = e.MaxHp = 10000; e.StunTime = 100; e.Element = ElementId.Grass; return e;
        }
        static void Tick(GameSession s, float dt)
        {
            foreach (var a in Actors(s).ToArray())
                if (!a.Dead && (a.Kind == ActorKind.CurrentEmitter || a.Kind == ActorKind.RadialCurrent))
                    Call(s, "TickPlannedAbility", a, dt);
            Actors(s).RemoveAll(a => a.Dead && a.Kind != ActorKind.Enemy);
        }
        void Start() { deadline = Time.realtimeSinceStartup + 180; run = Run(); }
        void Update()
        {
            if (done) return;
            try
            {
                if (Time.realtimeSinceStartup > deadline) throw new TimeoutException("Electric spark checks timed out");
                if (!run.MoveNext()) { done = true; ElectricSparkChecks.Finish(true); }
            }
            catch (Exception e)
            {
                ElectricSparkChecks.Log("ELECTRIC SPARK CHECK FAILED: " + e);
                done = true; ElectricSparkChecks.Finish(false);
            }
        }

        IEnumerator Run()
        {
            yield return null;
            ushort port;
            using (var socket = new System.Net.Sockets.UdpClient(new System.Net.IPEndPoint(System.Net.IPAddress.Loopback, 0)))
                port = (ushort)((System.Net.IPEndPoint)socket.Client.LocalEndPoint).Port;
            typeof(GameBootstrap).GetMethod("StartHost", BindingFlags.Static | BindingFlags.NonPublic, null,
                new[] { typeof(ushort) }, null).Invoke(null, new object[] { port });
            while (GameSession.Instance == null || !GameSession.Instance.IsSpawned) yield return null;
            var s = GameSession.Instance;
            s.SelectStartingElement(ElementId.Electric);
            while (s.HostElement.Value == 0) yield return null;
            s.ToggleLocalReady(); while (!s.CanStart()) yield return null; s.RequestStart();
            while (!s.IsPlaying || OctopusPlayer.Local == null) yield return null;
            var p = OctopusPlayer.Local; p.enabled = s.enabled = false;
            p.UpAttrRank.Value = 0; p.Facing.Value = Vector2.right; p.transform.position = Vector3.zero; p.NetPos.Value = Vector2.zero;
            p.Slot0.Value = p.Slot1.Value = p.Slot2.Value = p.Slot3.Value = 0;
            ElectricSparkChecks.Log("ELECTRIC SPARK CHECKS BEGIN");
            Time.captureDeltaTime = 1f / 30;

            Check(GameConfig.ActorBytes == 25, "existing snapshot record preserved");

            // 1. Visual does not consume the gameplay random stream.
            UnityEngine.Random.InitState(1234); float expected = UnityEngine.Random.value;
            UnityEngine.Random.InitState(1234);
            ElectricSparkBurst.Spawn(new Vector2(30, 30), 6.1f);
            for (int i = 0; i < 6; i++) yield return null;
            Check(Mathf.Approximately(UnityEngine.Random.value, expected), "spark visual leaves UnityEngine.Random untouched");

            // 2. Budget: a flood of bursts stays inside the pooled limits and retires.
            for (int i = 0; i < 40; i++) ElectricSparkBurst.Spawn(new Vector2(30 + i * .1f, 30), 6.1f);
            for (int i = 0; i < 3; i++) yield return null;
            Check(ElectricSparkBurst.ActiveBursts <= 8 && ElectricSparkBurst.ActiveSparks <= 220,
                "burst and particle budget bounded (" + ElectricSparkBurst.ActiveBursts + " bursts, " + ElectricSparkBurst.ActiveSparks + " sparks)");
            for (int i = 0; i < 40; i++) yield return null;
            Check(ElectricSparkBurst.ActiveBursts == 0 && ElectricSparkBurst.ActiveSparks == 0, "all bursts retire after their lifetime");

            // 3. Real basic skill: spark appears once per radial wave and gameplay damage is unaffected.
            Call(s, "ClearFieldForNextStage"); Actors(s).RemoveAll(a => a.Dead);
            var layout = new[] { new Vector2(3.6f, 1.2f), new Vector2(-4.2f, 1.6f), new Vector2(1.2f, 4.4f), new Vector2(-2.6f, -3.8f),
                new Vector2(4.4f, -2.6f), new Vector2(-1.0f, 2.8f) };
            var enemies = new List<SimActor>();
            foreach (var pos in layout) enemies.Add(Target(s, pos));
            var outside = Target(s, new Vector2(9f, 9f));
            int before = ElectricSparkBurst.TotalSpawned;
            s.CastBasicAbility(p, ElementId.Electric, 1);

            var camera = Camera.main; var follow = camera.GetComponent<PlayerCameraFollow>(); if (follow != null) follow.enabled = false;
            camera.orthographicSize = GameConfig.CameraSize; camera.aspect = 16f / 9f; camera.transform.position = new Vector3(0, 0, -10);
            string output = Path.GetFullPath("Build/Validation/ElectricSparkFrames"); Directory.CreateDirectory(output);
            foreach (var old in Directory.GetFiles(output, "frame-*.png")) File.Delete(old);

            for (int frame = 0; frame < 90; frame++)
            {
                Tick(s, 1f / 30);
                Call(s, "RenderLocal");
                yield return null;
                Capture(camera, Path.Combine(output, "frame-" + frame.ToString("D3") + ".png"));
            }
            Check(ElectricSparkBurst.TotalSpawned - before == 1, "exactly one spark burst for the single radial wave (" + (ElectricSparkBurst.TotalSpawned - before) + ")");
            Check(enemies.TrueForAll(e => e.Hp < e.MaxHp), "every enemy inside the range was damaged");
            Check(outside.Hp == outside.MaxHp, "enemy outside the range was not damaged");
            Check(ElectricSparkBurst.ActiveBursts == 0, "capture ends with no leftover spark objects");
            Time.captureDeltaTime = 0;
            ElectricSparkChecks.Log("ELECTRIC SPARK CHECKS PASS: peak sparks " + ElectricSparkBurst.PeakSparks);
        }

        static void Capture(Camera camera, string path)
        {
            var previous = RenderTexture.active; var rt = RenderTexture.GetTemporary(960, 540, 24);
            var texture = new Texture2D(960, 540, TextureFormat.RGB24, false);
            try
            {
                RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = rt });
                RenderTexture.active = rt; texture.ReadPixels(new Rect(0, 0, 960, 540), 0, 0); texture.Apply();
                File.WriteAllBytes(path, texture.EncodeToPNG());
            }
            finally { RenderTexture.active = previous; RenderTexture.ReleaseTemporary(rt); UnityEngine.Object.Destroy(texture); }
        }
        void OnDestroy() { Time.captureDeltaTime = 0; }
    }
}
