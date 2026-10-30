using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace OctopusSurvivors.EditorTools
{
    [InitializeOnLoad]
    public static class GrassThornChecks
    {
        const string Key = "Octopus.GrassThornCheck";
        static GrassThornChecks()
        {
            double nextPoll = 0;
            EditorApplication.update += () =>
            {
                if (EditorApplication.timeSinceStartup < nextPoll) return;
                nextPoll = EditorApplication.timeSinceStartup + .5;
                if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
                const string request = "Logs/grass-thorn-check.request";
                if (File.Exists(request))
                {
                    File.Delete(request); SessionState.SetBool(Key + ".Pending", true);
                    AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport); return;
                }
                if (!SessionState.GetBool(Key + ".Pending", false)) return;
                SessionState.SetBool(Key + ".Pending", false); Run();
            };
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Key, false))
                    new GameObject("Grass thorn integration checks").AddComponent<GrassThornCapture>();
                if (state == PlayModeStateChange.EnteredEditMode && SessionState.GetBool(Key, false))
                {
                    SessionState.SetBool(Key, false);
                    if (Application.isBatchMode) EditorApplication.Exit(SessionState.GetInt(Key + ".Exit", 2));
                }
            };
        }

        [MenuItem("문어/풀 가시 픽셀 검증")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/grass-thorn-check.status", "RUNNING");
            File.WriteAllText("Logs/grass-thorn-checks.log", "");
            ShaderUtil.allowAsyncCompilation = false;
            EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
            SessionState.SetBool(Key, true);
            EditorApplication.EnterPlaymode();
        }

        internal static void Log(string text)
        {
            Debug.Log(text);
            File.AppendAllText("Logs/grass-thorn-checks.log", text + "\n");
        }

        internal static void Finish(bool pass)
        {
            File.WriteAllText("Logs/grass-thorn-check.status", pass ? "PASS" : "FAIL");
            SessionState.SetInt(Key + ".Exit", pass ? 0 : 2);
            EditorApplication.isPlaying = false;
        }
    }

    public sealed class GrassThornCapture : MonoBehaviour
    {
        static readonly BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        IEnumerator routine;
        float deadline;
        bool done;
        static object Call(object obj, string method, params object[] args) => obj.GetType().GetMethod(method, Hidden).Invoke(obj, args);
        static List<SimActor> Actors(GameSession s) => (List<SimActor>)typeof(GameSession).GetField("_actors", Hidden).GetValue(s);
        static void Check(bool ok, string message)
        {
            if (!ok) throw new Exception(message);
            GrassThornChecks.Log("GRASS THORN PASS: " + message);
        }
        static SimActor Target(GameSession s, Vector2 position)
        {
            var e = (SimActor)Call(s, "Create", ActorKind.Enemy, position, .7f);
            e.Hp = e.MaxHp = 10000; e.StunTime = 100; e.Element = ElementId.Water;
            return e;
        }
        void Start() { deadline = Time.realtimeSinceStartup + 180; routine = Run(); }
        void Update()
        {
            if (done) return;
            try
            {
                if (Time.realtimeSinceStartup > deadline) throw new TimeoutException("Grass thorn checks timed out");
                if (!routine.MoveNext()) { done = true; GrassThornChecks.Finish(true); }
            }
            catch (Exception e)
            {
                GrassThornChecks.Log("GRASS THORN CHECK FAILED: " + e);
                done = true; GrassThornChecks.Finish(false);
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
            s.SelectStartingElement(ElementId.Grass);
            while (s.HostElement.Value == 0) yield return null;
            s.ToggleLocalReady(); while (!s.CanStart()) yield return null; s.RequestStart();
            while (!s.IsPlaying || OctopusPlayer.Local == null) yield return null;
            var p = OctopusPlayer.Local;
            p.enabled = s.enabled = false;
            p.transform.position = new Vector3(0, -1.8f, 0); p.NetPos.Value = p.transform.position;
            p.UpAttrRank.Value = 0;
            p.Slot0.Value = p.Slot1.Value = p.Slot2.Value = p.Slot3.Value = 0;
            Call(s, "ClearFieldForNextStage"); Actors(s).RemoveAll(a => a.Dead);
            Check(GameConfig.ActorBytes == 25, "network snapshot format unchanged");
            for (int i = 0; i < 8; i++)
            {
                var frame = GrassThornVisual.Frame(i);
                Check(frame != null && frame.texture.filterMode == FilterMode.Point && frame.rect.width == 512 &&
                    frame.rect.height == 544 && Mathf.Abs(frame.pivot.y - 44) < .01f,
                    "approved frame " + i + " loads with point sampling and fixed ground pivot");
            }
            Check(GrassThornVisual.FrameIndex(.15f) == 1 && GrassThornVisual.FrameIndex(.3f) == 2 &&
                GrassThornVisual.FrameIndex(.5f) == 3 && GrassThornVisual.FrameIndex(.75f) == 4 &&
                GrassThornVisual.FrameIndex(1) == 7,
                "readable anticipation, slower growth, extended full plant and sand-only ending");
            var enemies = new[] { Target(s, new Vector2(-1.8f, .2f)), Target(s, new Vector2(0, .4f)), Target(s, new Vector2(1.8f, .2f)) };
            var fourth = Target(s, new Vector2(0, 4));
            var outside = Target(s, new Vector2(12, 0));
            s.CastBasicAbility(p, ElementId.Grass, 1);
            var emitter = Actors(s).Find(a => a.Kind == ActorKind.ThornEmitter && !a.Dead);
            Check(emitter != null && emitter.EffectLevel == 3 && Mathf.Approximately(emitter.Damage, 19f),
                "basic grass retains three nearest targets and 19 damage per strike");
            for (int i = 0; i < 4; i++) Call(s, "StrikeUpgradedVines", emitter);
            Check(Array.TrueForAll(enemies, e => Mathf.Abs(e.Hp - (10000 - 19 * 4)) < .01f) &&
                fourth.Hp == 10000 && outside.Hp == 10000, "four strikes preserve damage, target count and range");
            emitter.Dead = true;
            var vines = Actors(s).FindAll(a => a.Kind == ActorKind.Vine && !a.Dead);
            Check(vines.Count == 12 && vines.TrueForAll(a => Mathf.Approximately(a.Life, .72f) &&
                Mathf.Approximately(a.Duration, .72f)) && Mathf.Approximately(emitter.Duration, .64f),
                "plant visuals last .72 seconds while original .64-second attack duration stays unchanged");
            // Capture a single strike per target, instead of stacking the four test strikes.
            for (int i = 3; i < vines.Count; i++) vines[i].Dead = true;
            Call(s, "RenderLocal"); yield return null;
            var visuals = UnityEngine.Object.FindObjectsByType<GrassThornVisual>(FindObjectsSortMode.None);
            Check(visuals.Length == 3, "WorldView routes live vine actors to selected pixel visual");
            foreach (var v in visuals)
            {
                v.enabled = false;
                Check(v.GetComponentsInChildren<SpriteRenderer>().Length == 2 &&
                    v.transform.Find("Textured afterglow") == null, "legacy atlas and glow are absent");
                v.Configure(.45f, .72f);
                var child = v.transform.Find("Matte pixel thorn strike").GetComponent<SpriteRenderer>();
                var selected = child.sprite;
                v.Configure(0, 0);
                Check(child.sprite == selected, "zero-age snapshots preserve selected frame and fixed registration");
            }
            var camera = Camera.main;
            var follow = camera.GetComponent<PlayerCameraFollow>(); if (follow != null) follow.enabled = false;
            camera.orthographicSize = 3.7f; camera.aspect = 16f / 9; camera.transform.position = new Vector3(0, .6f, -10);
            string folder = Path.GetFullPath("Build/Validation/GrassThornFrames"); Directory.CreateDirectory(folder);
            float[] times = { 0, .12f, .21f, .32f, .48f, .61f, .67f, .71f };
            for (int i = 0; i < times.Length; i++)
            {
                foreach (var v in visuals) v.RenderAt(times[i]);
                yield return null;
                Capture(camera, Path.Combine(folder, "frame-" + i.ToString("D2") + ".png"));
            }
            Call(s, "TickActors", .2f); Call(s, "RenderLocal"); yield return null;
            Check(vines[0].Age >= .2f && !vines[0].Dead &&
                UnityEngine.Object.FindObjectsByType<GrassThornVisual>(FindObjectsSortMode.None).Length == 3,
                "plant survives former .18-second cutoff and synchronizes presentation age");
            Check(Array.TrueForAll(enemies, e => Mathf.Abs(e.Hp - (10000 - 19 * 4)) < .01f),
                "longer plant display applies no additional damage");
            Call(s, "TickActors", .53f); Call(s, "RenderLocal"); yield return null;
            Check(UnityEngine.Object.FindObjectsByType<GrassThornVisual>(FindObjectsSortMode.None).Length == 0,
                "expired transient actors remove all pixel renderers");
            GrassThornChecks.Log("GRASS THORN CHECKS PASS");
        }

        static void Capture(Camera camera, string path)
        {
            var previous = RenderTexture.active;
            var rt = RenderTexture.GetTemporary(960, 540, 24);
            var texture = new Texture2D(960, 540, TextureFormat.RGB24, false);
            try
            {
                RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = rt });
                RenderTexture.active = rt; texture.ReadPixels(new Rect(0, 0, 960, 540), 0, 0); texture.Apply();
                File.WriteAllBytes(path, texture.EncodeToPNG());
            }
            finally { RenderTexture.active = previous; RenderTexture.ReleaseTemporary(rt); Destroy(texture); }
        }
    }
}
