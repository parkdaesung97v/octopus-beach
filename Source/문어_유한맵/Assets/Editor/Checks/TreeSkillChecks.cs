using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.Media;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace OctopusSurvivors.EditorTools
{
    [InitializeOnLoad]
    public static class TreeSkillChecks
    {
        const string Key = "Octopus.TreeSkillCheck";
        static TreeSkillChecks()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Key, false))
                    new GameObject("Tree skill play capture").AddComponent<TreeSkillCapture>();
                if (state == PlayModeStateChange.EnteredEditMode && SessionState.GetBool(Key, false))
                {
                    SessionState.SetBool(Key, false);
                    if (Application.isBatchMode) EditorApplication.Exit(SessionState.GetInt(Key + ".Exit", 2));
                }
            };
        }

        [MenuItem("문어/나무 A B C 플레이 영상 및 검증 %&t")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
            ShaderUtil.allowAsyncCompilation = false;
            var importer = (TextureImporter)AssetImporter.GetAtPath("Assets/Resources/Vfx/TreeVariants.png");
            importer.textureType = TextureImporterType.Default;
            importer.alphaIsTransparency = true; importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Point; importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 2048; importer.npotScale = TextureImporterNPOTScale.None;
            importer.SaveAndReimport();
            SystemDataChecks.Run();
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/tree-skill-check.status", "RUNNING");
            SessionState.SetBool(Key, true); SessionState.SetInt(Key + ".Exit", 2);
            EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
            EditorApplication.EnterPlaymode();
        }

        internal static void Finish(int code)
        {
            File.WriteAllText("Logs/tree-skill-check.status", code == 0 ? "PASS" : "FAIL");
            SessionState.SetInt(Key + ".Exit", code);
            EditorApplication.isPlaying = false;
        }
    }

    [DefaultExecutionOrder(10000)]
    public sealed class TreeSkillCapture : MonoBehaviour
    {
        const int Width = 1280, Height = 720, Fps = 30, Frames = 180;
        static readonly BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        static object Call(object target, string name, params object[] args) => target.GetType().GetMethod(name, Hidden).Invoke(target, args);
        static List<SimActor> Actors(GameSession s) => (List<SimActor>)typeof(GameSession).GetField("_actors", Hidden).GetValue(s);
        static void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
            Debug.Log("TREE CHECK PASS: " + message);
        }

        IEnumerator run;
        MediaEncoder encoder, comparison;
        RenderTexture target;
        Texture2D frameTexture;
        Camera camera;
        TreePalette originalPalette;
        bool recording, finished;
        int frame, variant;
        int captured;
        float timeout;
        Text title;
        string output;
        float[] damageByVariant = new float[3];
        float leafMotion;

        void Start()
        {
            timeout = Time.realtimeSinceStartup + 240;
            originalPalette = TreeAbilityTuning.Data.palette;
            output = Path.GetFullPath("Build/Validation/TreeSkill");
            Directory.CreateDirectory(output);
            Time.captureDeltaTime = 1f / Fps;
            Application.runInBackground = true;
            run = Run();
        }

        void Update()
        {
            if (finished) return;
            try
            {
                if (Time.realtimeSinceStartup > timeout) throw new TimeoutException("Tree video capture timed out");
                if (!run.MoveNext()) Complete(0);
            }
            catch (Exception e) { Debug.LogError("TREE SKILL CHECK FAILED: " + e); Complete(2); }
        }

        IEnumerator Run()
        {
            yield return null;
            ushort port;
            using (var socket = new System.Net.Sockets.UdpClient(new System.Net.IPEndPoint(System.Net.IPAddress.Loopback, 0)))
                port = (ushort)((System.Net.IPEndPoint)socket.Client.LocalEndPoint).Port;
            typeof(GameBootstrap).GetMethod("StartHost", BindingFlags.Static | BindingFlags.NonPublic,
                null, new[] { typeof(ushort) }, null).Invoke(null, new object[] { port });
            while (GameSession.Instance == null || !GameSession.Instance.IsSpawned) yield return null;
            var session = GameSession.Instance;
            session.SelectStartingElement(ElementId.Grass);
            while (session.HostElement.Value == 0) yield return null;
            session.ToggleLocalReady();
            while (!session.CanStart()) yield return null;
            session.RequestStart();
            while (!session.IsPlaying || OctopusPlayer.Local == null) yield return null;
            var player = OctopusPlayer.Local;
            session.enabled = player.enabled = false;
            player.Slot0.Value = player.Slot1.Value = player.Slot2.Value = player.Slot3.Value = 0;
            player.Combo.Value = (byte)ComboId.Tree; player.Combo1.Value = player.Combo2.Value = 0;
            player.UpElemRank.Value = player.UpAttrRank.Value = 0; player.SkillLevels.Value = 0;

            Check(GameConfig.ActorBytes == 25, "snapshot format preserved");
            ValidateDamageAndBudgets(session, player);
            camera = Camera.main;
            Check(camera != null && SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null, "real game render device available");
            var follower = camera.GetComponent<PlayerCameraFollow>(); if (follower != null) follower.enabled = false;
            camera.orthographicSize = 5.7f; camera.aspect = Width / (float)Height;
            camera.transform.position = new Vector3(0, .5f, -10);
            target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32);
            frameTexture = new Texture2D(Width, Height, TextureFormat.RGBA32, false);
            MakeTitle();
            comparison = NewEncoder(Path.Combine(output, "tree-ABC-comparison.mp4"));
            for (variant = 0; variant < 3; variant++)
            {
                ResetField(session);
                yield return null; // Dispose the previous variant's visuals before reseeding.
                UnityEngine.Random.InitState(4517);
                TreeAbilityTuning.Data.palette = (TreePalette)variant;
                session.Elapsed.Value = 0;
                leafMotion = 0;
                player.transform.position = new Vector3(0, -2.3f, 0); player.NetPos.Value = player.Pos;
                player.Hp.Value = player.MaxHp.Value; player.MoveInput.Value = Vector2.zero;
                player.SkillLevels.Value = 0;
                var cooldowns = (float[])typeof(OctopusPlayer).GetField("comboCooldowns", Hidden).GetValue(player);
                for (int i = 0; i < cooldowns.Length; i++) cooldowns[i] = .4f;
                for (int i = 0; i < 18; i++)
                {
                    float angle = i * 137.5f * Mathf.Deg2Rad;
                    Vector2 pos = new Vector2(Mathf.Cos(angle) * (2.2f + i % 3 * .8f), -1.2f + Mathf.Sin(angle) * 1.5f);
                    var enemy = Target(session, pos, 36f);
                    enemy.Element = (ElementId)(1 + i % 5); enemy.StunTime = 0;
                }
                title.text = new[] { "A", "B", "C" }[variant] + "  |  VERTICAL RAIN / LEAF EDGE TO GROUND";
                encoder = NewEncoder(Path.Combine(output, "tree-" + (char)('A' + variant) + "-gameplay.mp4"));
                captured = 0; recording = true;
                for (frame = 0; frame < Frames; frame++)
                {
                    const float dt = 1f / Fps;
                    float t = frame * dt;
                    UnityEngine.Random.InitState(4517 + frame);
                    player.MoveInput.Value = new Vector2(t < 1.4f ? -.22f : t < 4.2f ? .22f : -.22f, -.025f);
                    player.ServerTick(dt);
                    Call(player, "UpdateVisuals");
                    Call(session, "TickActors", dt);
                    Actors(session).RemoveAll(a => a.Dead);
                    session.Elapsed.Value = t;
                    Call(session, "RenderLocal");
                    var visuals = UnityEngine.Object.FindObjectsByType<TreeSkillVisual>();
                    foreach (var visual in visuals)
                    {
                        leafMotion = Mathf.Max(leafMotion, visual.MaximumLeafOffset);
                        if (!visual.IsTree && (Mathf.Abs(visual.FruitPosition.x - visual.LandingPosition.x) > .001f ||
                            Mathf.Abs(visual.SourcePosition.x - visual.LandingPosition.x) > .001f))
                            throw new InvalidOperationException("Fruit must fall in a fixed vertical column");
                    }
                    yield return null;
                }
                recording = false;
                encoder.Dispose(); encoder = null;
                Check(captured == Frames, "variant " + (char)('A' + variant) + " captured 180 actual Unity frames");
                Check(leafMotion > .001f, "local leaves visibly shake for variant " + variant);
                float remainingHp = 0; int living = 0;
                foreach (var a in Actors(session)) if (a.Kind == ActorKind.Enemy) { remainingHp += a.Hp; living++; }
                damageByVariant[variant] = 18 * 36 - remainingHp;
                Check(damageByVariant[variant] > 0, "fruit rain damages real enemies; survivors " + living);
            }
            comparison.Dispose(); comparison = null;
            Check(Mathf.Abs(damageByVariant[0] - damageByVariant[1]) < .01f && Mathf.Abs(damageByVariant[1] - damageByVariant[2]) < .01f,
                "A/B/C seeded battles have identical total damage: " + string.Join(", ", damageByVariant));
            Debug.Log("TREE SKILL CHECKS PASS: three palettes, leaf-only shake, accelerated fruit fall, real damage, 3x6 second 1280x720 30fps MP4 + comparison");
        }

        static SimActor Target(GameSession session, Vector2 position, float hp)
        {
            var target = (SimActor)Call(session, "Create", ActorKind.Enemy, position, .7f);
            target.Hp = target.MaxHp = hp; target.Life = 99999; target.StunTime = 100;
            return target;
        }

        static void ResetField(GameSession session)
        {
            Call(session, "ClearFieldForNextStage"); Actors(session).Clear(); Call(session, "RenderLocal");
        }

        static void ValidateDamageAndBudgets(GameSession session, OctopusPlayer player)
        {
            foreach (float dt in new[] { 1f / 30, 1f / 60, 1f / 144, .25f })
            for (int rank = 0; rank <= 5; rank++)
            {
                ResetField(session);
                player.transform.position = Vector3.zero;
                player.SkillLevels.Value = (ulong)rank << (((int)UpgradeId.TreeMastery - 10) * 3);
                var target = Target(session, new Vector2(1, 0), 1000000);
                session.CastPlannedCombo(player, ComboId.Tree);
                var trees = Actors(session).FindAll(a => a.Kind == ActorKind.FruitTree);
                Check(trees.Count == rank + 1, "tree count " + trees.Count + " at rank " + rank);
                var shotIds = new HashSet<ushort>();
                float allocatedDamage = 0;
                for (float time = 0; time < 5; time += dt)
                {
                    foreach (var tree in trees) if (!tree.Dead) Call(session, "TickPlannedAbility", tree, dt);
                    var fruits = Actors(session).FindAll(a => a.Kind == ActorKind.Fruit && !a.Dead);
                    foreach (var fruit in fruits)
                    {
                        if (shotIds.Add(fruit.Id)) allocatedDamage += fruit.Damage;
                        Call(session, "TickPlannedAbility", fruit, dt);
                    }
                    Actors(session).RemoveAll(a => a.Dead);
                }
                int shots = 0; foreach (var tree in trees) shots += tree.Mark;
                int expected = (rank == 5 ? 70 : 56) * (rank + 1);
                float expectedDamage = expected * trees[0].Damage;
                Check(shots == expected, "frame-independent fruit budget " + expected + " at dt=" + dt);
                Check(shotIds.Count == expected && Mathf.Abs(allocatedDamage - expectedDamage) < .2f,
                    "all distributed fruit created without actor-cap losses at rank " + rank);
                Check(target.MaxHp - target.Hp <= expectedDamage + .2f, "landing damage cannot exceed fruit budget");
            }
            ValidateDistribution(session);
            ResetField(session); player.SkillLevels.Value = 0;
            var inside = Target(session, Vector2.zero, 1000);
            var outside = Target(session, new Vector2(1.2f, 0), 1000);
            var fruitActor = new SimActor { Kind = ActorKind.Fruit, Pos = Vector2.zero, Size = .5f, Life = .6f, Duration = .6f, Damage = 9, Owner = player };
            Call(session, "TickPlannedAbility", fruitActor, .3f);
            Check(inside.Hp == 1000, "no damage before fruit lands");
            Call(session, "TickPlannedAbility", fruitActor, .31f);
            Call(session, "TickPlannedAbility", fruitActor, .1f);
            Check(inside.Hp == 991 && outside.Hp == 1000, "one landing hit inside fruit radius, no invisible whole-tree damage");
        }

        static void ValidateDistribution(GameSession session)
        {
            ResetField(session);
            var tree = new SimActor { Kind = ActorKind.FruitTree, Pos = Vector2.zero, Size = 6,
                SkillPoint = new Vector2(.123f, .456f) };
            var points = new Vector2[56];
            int[] strips = new int[4];
            float halfWidth = TreeRainGeometry.HalfLandingWidth(tree.Size);
            float height = TreeRainGeometry.Height(tree.Size);
            for (int i = 0; i < points.Length; i++)
            {
                tree.Mark = i + 1;
                var point = (Vector2)Call(session, "TreeRainLandingPoint", tree); points[i] = point;
                Check(Mathf.Abs(point.x) <= halfWidth + .001f && point.y >= -height * .047f - .001f && point.y <= -height * .035f + .001f,
                    "fruit landing stays on marked lower ground strip " + i);
                strips[Mathf.Clamp((int)((point.x + halfWidth) / (halfWidth * 2) * 4), 0, 3)]++;
                Vector3 release = TreeRainGeometry.ReleasePoint(tree.Pos, tree.Size, point);
                Check(Mathf.Abs(release.x - point.x) < .0001f && release.y > point.y + 1,
                    "release is on lower foliage and directly above landing " + i);
            }
            Check(Array.TrueForAll(strips, count => count >= 12 && count <= 16),
                "even left-to-right coverage inside marked side boundaries: " + string.Join(",", strips));
            var enemy = Target(session, new Vector2(1, 0), 1000);
            for (int i = 0; i < points.Length; i++)
            {
                tree.Mark = i + 1; enemy.Pos = new Vector2(i % 5 - 2, i % 7 - 3);
                Check((Vector2)Call(session, "TreeRainLandingPoint", tree) == points[i], "enemy presence and movement cannot steer fruit " + i);
            }
        }

        static MediaEncoder NewEncoder(string path) => new MediaEncoder(path,
            new VideoTrackAttributes { frameRate = new MediaRational(Fps), width = Width, height = Height, includeAlpha = false });

        void MakeTitle()
        {
            var canvasGo = new GameObject("Tree comparison title");
            var canvas = canvasGo.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 1000;
            var scaler = canvasGo.AddComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(Width, Height); scaler.matchWidthOrHeight = 1;
            var go = new GameObject("Palette label"); go.transform.SetParent(canvasGo.transform, false);
            title = go.AddComponent<Text>();
            title.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            title.fontSize = 24; title.alignment = TextAnchor.MiddleCenter; title.color = Color.white;
            var outline = go.AddComponent<Outline>(); outline.effectColor = new Color(0, 0, 0, .85f); outline.effectDistance = new Vector2(2, -2);
            title.rectTransform.anchorMin = title.rectTransform.anchorMax = new Vector2(.5f, 1);
            title.rectTransform.pivot = new Vector2(.5f, 1);
            title.rectTransform.anchoredPosition = new Vector2(0, -18); title.rectTransform.sizeDelta = new Vector2(600, 40);
        }

        void LateUpdate()
        {
            if (!recording || finished) return;
            try
            {
                var changed = new List<Canvas>();
                var previous = RenderTexture.active;
                try
                {
                    foreach (var canvas in UnityEngine.Object.FindObjectsByType<Canvas>())
                        if (canvas.isRootCanvas && canvas.renderMode == RenderMode.ScreenSpaceOverlay)
                        {
                            changed.Add(canvas); canvas.renderMode = RenderMode.ScreenSpaceCamera;
                            canvas.worldCamera = camera; canvas.planeDistance = 1;
                        }
                    Canvas.ForceUpdateCanvases();
                    RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
                    RenderTexture.active = target;
                    frameTexture.ReadPixels(new Rect(0, 0, Width, Height), 0, 0); frameTexture.Apply();
                    if (!encoder.AddFrame(frameTexture) || !comparison.AddFrame(frameTexture)) throw new IOException("MP4 frame encoder rejected frame");
                    if (captured == 50 || captured == 88)
                        File.WriteAllBytes(Path.Combine(output, "tree-" + (char)('A' + variant) + "-" + captured + ".png"), frameTexture.EncodeToPNG());
                    if (variant == 0 && captured % 2 == 0)
                    {
                        string framesDirectory = Path.Combine(output, "GifFrames"); Directory.CreateDirectory(framesDirectory);
                        File.WriteAllBytes(Path.Combine(framesDirectory, "frame-" + (captured / 2).ToString("D3") + ".png"), frameTexture.EncodeToPNG());
                    }
                    captured++;
                }
                finally
                {
                    foreach (var canvas in changed) { canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.worldCamera = null; }
                    RenderTexture.active = previous;
                }
            }
            catch (Exception e) { Debug.LogError("TREE CAPTURE FAILED: " + e); Complete(2); }
        }

        void Complete(int code)
        {
            if (finished) return;
            finished = true; recording = false;
            encoder?.Dispose(); comparison?.Dispose(); encoder = comparison = null;
            TreeAbilityTuning.Data.palette = originalPalette;
            Time.captureDeltaTime = 0;
            if (target != null) { target.Release(); Destroy(target); }
            if (frameTexture != null) Destroy(frameTexture);
            TreeSkillChecks.Finish(code);
        }
        void OnDestroy() { if (!finished) Complete(2); }
    }
}
