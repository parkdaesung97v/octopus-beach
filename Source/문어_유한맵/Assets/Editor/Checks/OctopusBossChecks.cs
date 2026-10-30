using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Unity.Collections;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace OctopusSurvivors.EditorTools
{
    [InitializeOnLoad]
    public static class OctopusBossChecks
    {
        const string Key = "Octopus.Stage5BossCheck";
        internal static bool MapOnly => SessionState.GetBool(Key + ".MapOnly", false);
        static string StatusPath => MapOnly ? "Logs/Stage5MapChecks.status" : "Logs/OctopusBossChecks.status";
        static OctopusBossChecks()
        {
            EditorApplication.delayCall += () =>
            {
                const string request = "Logs/Stage5MapChecks.request";
                if (!File.Exists(request) || EditorApplication.isPlayingOrWillChangePlaymode) return;
                File.Delete(request);
                RunMapOnly();
            };
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Key, false))
                    new GameObject("Stage 5 octopus boss checks").AddComponent<OctopusBossCapture>();
                if (state == PlayModeStateChange.EnteredEditMode && SessionState.GetBool(Key, false))
                {
                    SessionState.SetBool(Key, false);
                    if (Application.isBatchMode) EditorApplication.Exit(SessionState.GetInt(Key + ".Exit", 2));
                }
            };
        }
        [MenuItem("문어/5스테이지 문어 보스 검증")]
        public static void Run()
        {
            SessionState.SetBool(Key + ".MapOnly", false);
            StartCheck();
        }
        [MenuItem("문어/5스테이지 맵 표시 검증")]
        public static void RunMapOnly()
        {
            SessionState.SetBool(Key + ".MapOnly", true);
            StartCheck();
        }
        static void StartCheck()
        {
            Directory.CreateDirectory("Logs");
            File.WriteAllText(StatusPath, "RUNNING");
            EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
            ShaderUtil.allowAsyncCompilation = false;
            SessionState.SetBool(Key, true); EditorApplication.EnterPlaymode();
        }
        internal static void Finish(bool pass)
        {
            File.WriteAllText(StatusPath, pass ? "PASS" : "FAIL");
            SessionState.SetInt(Key + ".Exit", pass ? 0 : 2); EditorApplication.isPlaying = false;
        }
    }
    public sealed class OctopusBossCapture : MonoBehaviour
    {
        const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        IEnumerator routine;
        float deadline;
        bool finished;
        static object Call(object o, string name, params object[] args) => o.GetType().GetMethod(name, Hidden).Invoke(o, args);
        static void Check(bool condition, string label)
        { if (!condition) throw new Exception(label); Debug.Log("OCTOPUS BOSS PASS: " + label); }
        void Start() { routine = Run(); deadline = Time.realtimeSinceStartup + 240; }
        void Update()
        {
            if (finished) return;
            try
            {
                if (Time.realtimeSinceStartup > deadline) throw new TimeoutException("Octopus boss check timeout");
                if (routine.MoveNext()) return;
                finished = true; OctopusBossChecks.Finish(true);
            }
            catch (Exception e)
            { Debug.LogError("OCTOPUS BOSS FAIL: " + e); finished = true; OctopusBossChecks.Finish(false); }
        }
        IEnumerator Run()
        {
            CheckGeometry();
            yield return null;
            ushort port;
            using (var socket = new System.Net.Sockets.UdpClient(new System.Net.IPEndPoint(System.Net.IPAddress.Loopback, 0)))
                port = (ushort)((System.Net.IPEndPoint)socket.Client.LocalEndPoint).Port;
            typeof(GameBootstrap).GetMethod("StartHost", BindingFlags.Static | BindingFlags.NonPublic, null,
                new[] { typeof(ushort) }, null).Invoke(null, new object[] { port });
            while (GameSession.Instance == null || !GameSession.Instance.IsSpawned) yield return null;
            var s = GameSession.Instance; s.SelectStartingElement(ElementId.Water);
            while (s.HostElement.Value == 0) yield return null;
            s.ToggleLocalReady(); while (!s.CanStart()) yield return null; s.RequestStart();
            while (!s.IsPlaying || OctopusPlayer.Local == null) yield return null;
            var p = OctopusPlayer.Local; p.enabled = s.enabled = false;
            p.Invincible.Value = true; p.transform.position = Vector3.zero; p.NetPos.Value = Vector2.zero;
            Call(s, "ClearFieldForNextStage");
            s.Stage.Value = 5; Call(s, "SpawnBoss", 5);
            var actors = (List<SimActor>)typeof(GameSession).GetField("_actors", Hidden).GetValue(s);
            var boss = actors.Find(a => !a.Dead && a.Kind == ActorKind.Boss);
            Check(boss != null && boss.Duration == OctopusBossPattern.VisualMarker && boss.Pos == OctopusBossPattern.Head,
                "stage 5 spawns the stationary octopus, not the former charging boss");
            foreach (float dt in new[] { 1f / 120, 1f / 30, .27f })
            {
                s.OctopusBoss.Value = new OctopusBossSnapshot { Pattern = 2, Seed = 12345 };
                int safety = 0;
                while (s.OctopusBoss.Value.Time < 5.6f && safety++ < 1000) Call(s, "TickBossPattern", boss, dt, 0f);
                Check(s.OctopusBoss.Value.Contacts == 30, "30 actual contacts, including crossed contacts at dt=" + dt);
            }
            s.OctopusBoss.Value = new OctopusBossSnapshot { Pattern = 2, Seed = 17, Time = .74f };
            Call(s, "TickBossPattern", boss, .009f, 0f);
            Check(s.OctopusBoss.Value.Contacts == 0, "counter does not increment during windup");
            Call(s, "TickBossPattern", boss, .002f, 0f);
            Check(s.OctopusBoss.Value.Contacts == 1, "counter increments at the first physical contact");
            s.OctopusBoss.Value = new OctopusBossSnapshot { Pattern = 2, Time = 6.64f, Contacts = 30, Seed = 9 };
            Call(s, "TickBossPattern", boss, .02f, 0f);
            Check(s.OctopusBoss.Value.Pattern == 4, "relay transitions into half-area burrow pattern");
            s.OctopusBoss.Value = new OctopusBossSnapshot { Pattern = 4, Time = 7.29f, Seed = 9 };
            Call(s, "TickBossPattern", boss, .02f, 0f);
            Check(s.OctopusBoss.Value.Pattern == 2 && s.OctopusBoss.Value.Contacts == 0,
                "return completes before the next relay resets its counter");
            var wire = new OctopusBossSnapshot { Pattern = 4, Half = 3, Contacts = 30, Seed = 0x1234abcd, Time = 2.345f,
                Vulnerable = 7.25f, ArmMaxHp = 200, Hp0 = 255, Hp1 = 182, Hp2 = 117, Hp3 = 80, Hp4 = 15, Hp5 = 0 };
            using (var writer = new FastBufferWriter(64, Allocator.Temp))
            {
                writer.WriteNetworkSerializable(wire);
                using (var reader = new FastBufferReader(writer, Allocator.Temp))
                { reader.ReadNetworkSerializable(out OctopusBossSnapshot copy); Check(copy.Equals(wire), "host/client snapshot round trip"); }
            }
            var camera = Camera.main; var follow = camera != null ? camera.GetComponent<PlayerCameraFollow>() : null;
            var mapRenderer = ArenaBuilder.BeachRenderer;
            Check(camera != null && follow != null && follow.enabled && mapRenderer != null,
                "runtime camera follow and the actual beach SpriteRenderer are available");
            s.OctopusBoss.Value = new OctopusBossSnapshot { Pattern = 2, Seed = 12345 };
            Call(s, "RenderLocal");
            yield return null;
            yield return null;
            Bounds mapBounds = mapRenderer.bounds;
            float expectedSize = PlayerCameraFollow.BossOrthographicSize(mapBounds);
            Vector2 expectedCenter = PlayerCameraFollow.BossCameraCenter(mapBounds);
            Check(camera.orthographic && Mathf.Abs(camera.orthographicSize - expectedSize) < .001f &&
                Mathf.Abs(camera.aspect - FiniteBeach.Aspect) < .001f &&
                Vector2.Distance(camera.transform.position, expectedCenter) < .001f,
                "stage 5 camera fills the 16:9 width and keeps the boss-map top edge");
            VerifyBossCameraFrame(mapRenderer, camera);
            VerifyBossViewportAspects(mapBounds);
            if (OctopusBossChecks.MapOnly)
            {
                var modeChecks = VerifyBossMapModeTransitions(s, camera, mapRenderer);
                while (modeChecks.MoveNext()) yield return modeChecks.Current;
                var art = mapRenderer.sprite;
                Check(art.texture.width == 7373 && art.texture.height == 5100,
                    "stage 5 uses the exact 7373x5100 bottom-ocean artwork");
                var originalCenter = mapRenderer.transform.TransformPoint(
                    new Vector3((7373f * .5f - art.pivot.x) / art.pixelsPerUnit,
                        (953f + 4147f * .5f - art.pivot.y) / art.pixelsPerUnit, 0));
                Check(Vector2.Distance(originalCenter, new Vector2(.4f, 3.7f)) < .001f &&
                    Vector3.Distance(mapRenderer.transform.localScale,
                        new Vector3(1.357988f, 1.357988f, 1f)) < .001f,
                    "stage 5 map keeps the authored position and uniform scale");
                Check(boss.Pos == OctopusBossPattern.Head,
                    "map replacement preserves the simulation boss world position");
                var resizeChecks = VerifyLiveGameViewSizes(mapRenderer, camera);
                while (resizeChecks.MoveNext()) yield return resizeChecks.Current;
            }
            VerifyLegGate(s, boss, actors);
            Directory.CreateDirectory("Logs/OctopusBossFrames");
            for (int shot = 0; shot < 8; shot++)
            {
                var snap = new OctopusBossSnapshot { Pattern = (byte)(shot < 2 ? 2 : 4), Seed = 12345,
                    Half = (byte)Mathf.Clamp(shot - 2, 0, 3), Time = shot == 0 ? 0 : shot == 1 ? 2.04f : shot < 6 ? 2.4f : shot == 6 ? .8f : 6.5f };
                if (shot == 1) snap.Contacts = 9;
                s.OctopusBoss.Value = snap; Call(s, "RenderLocal"); yield return null;
                var visual = FindFirstObjectByType<OctopusBossVisual>();
                Check(visual != null, "supplied sprite frames are connected to the actual boss actor");
                visual.Preview(snap);
                Call(follow, "LateUpdate");
                var armViews = (Array)typeof(OctopusBossVisual).GetField("arms", Hidden).GetValue(visual);
                int visible = 0;
                for (int arm = 0; arm < 6; arm++)
                {
                    var entry = armViews.GetValue(arm);
                    var go = (GameObject)entry.GetType().GetField("Root").GetValue(entry);
                    if (!go.activeSelf) continue;
                    visible++;
                    Vector2 expected = shot >= 2 && shot < 6 ? OctopusBossPattern.HalfRoot(snap.Half, arm) : OctopusBossPattern.RestRoot(arm);
                    Check(Vector2.Distance(go.transform.position, expected) < .001f, "fixed root / selected-half emergence / original-root return");
                }
                Check(visible == (shot == 6 ? 0 : 6), "all six burrow, emerge and return together");
                Capture(camera, "Logs/OctopusBossFrames/shot-" + shot + ".png");
                if (shot == 0)
                {
                    VerifyHeadInsideCamera(visual, camera);
                    VerifyHeadDoesNotOverlapBossHud(visual, camera);
                    if (OctopusBossChecks.MapOnly)
                        Check(Vector2.Distance(visual.BodyRenderer.transform.position,
                            new Vector2(0f, 17.16f)) < .001f &&
                            Vector3.Distance(visual.BodyRenderer.transform.localScale,
                                Vector3.one * .8753439f) < .001f,
                            "boss portrait keeps the authored position and uniform scale");
                    if (!OctopusBossChecks.MapOnly) VerifyCompleteDownFrames(visual);
                }
                if (shot == 4 && !OctopusBossChecks.MapOnly) VerifySideContactPose(visual);
            }
            if (OctopusBossChecks.MapOnly)
            {
                File.WriteAllText("Logs/Stage5MapChecks.summary.txt",
                    "PASS: 7373x5100 map at (0.4,3.7) and uniform 1.357988 scale; boss portrait at (0,17.16) and uniform 0.8753439 scale; 16:9 camera fills the map width without stretching and crops only lower ocean; Pattern 2/0/4/0/2 keeps camera/map; lifecycle restores player follow; four live Game view sizes; unchanged boss/arm roots; HUD separation; existing limb/core combat checks; eight captured poses.");
                yield break;
            }
            // Continuous captures expose stepping/teleporting that isolated poses can hide.
            for (int f = 0; f < 75; f++)
            {
                var snap = new OctopusBossSnapshot { Pattern = 4, Half = 2, Seed = 12345, Time = 1.65f + f / 25f };
                s.OctopusBoss.Value = snap; Call(s, "RenderLocal");
                FindFirstObjectByType<OctopusBossVisual>().Preview(snap);
                Capture(camera, "Logs/OctopusBossFrames/half-" + f.ToString("D3") + ".png");
                if (f % 10 == 0) yield return null;
            }
            Call(s, "KillEnemy", boss);
            Check(!s.BossAlive.Value && s.SpecialUnlocked.Value && s.OctopusBoss.Value.Pattern == 0,
                "defeat cleans up the pattern and preserves the stage 5 special unlock");
            int rewards = 0;
            foreach (var actor in actors) if (!actor.Dead && ElementRules.IsSpecialPickup(actor.Kind))
            { rewards++; Check(FiniteBeach.Walkable(actor.Pos, .4f), "special reward remains reachable on the beach"); }
            Check(rewards == 2, "two original stage 5 special rewards retained");
            File.WriteAllText("Logs/OctopusBossChecks.summary.txt",
                "PASS: stage5 replacement; complete-map camera fit; combat arena and boss visible; fixed-aspect letterboxing; boss head and HUD non-overlap; 30 contacts/4 half areas; six independent limb HP actors; protected core/target filtering; projectile and area limb damage; all-six break opens 10-second stun; core damage only during window; HP restore/relock/attack restart; snapshot HP+stun roundtrip; complete downward poses; continued ocean/sand backdrop; defeat/unlock/reachable rewards; eight screenshots and75 continuous frames.");
        }
        static void VerifyCompleteDownFrames(OctopusBossVisual visual)
        {
            var frames = (Array)typeof(OctopusBossVisual).GetField("slam", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            var whip = (Array)typeof(OctopusBossVisual).GetField("whip", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            Check(frames.Length == 9 && whip.Length == 21, "original 3x3 vertical and 5x5 side sheets use their authored frame counts");
            var firstWhip = whip.GetValue(0);
            var whipTexture = (Texture2D)firstWhip.GetType().GetField("Texture").GetValue(firstWhip);
            Check(whipTexture.width == 3000 && whipTexture.height == 2500, "side sheet is sliced as 5 columns by 5 rows of 600x500 cells");
            for (int frame = 0; frame < 21; frame++)
            {
                var pose = whip.GetValue(frame);
                var sideRect = (Rect)pose.GetType().GetField("Rect").GetValue(pose);
                int sourceIndex = frame == 0 ? 0 : frame + 4;
                Check(sideRect.x == sourceIndex % 5 * 600 && sideRect.y == (4 - sourceIndex / 5) * 500 &&
                    sideRect.width == 600 && sideRect.height == 500,
                    "side playback frame maps to populated source cell " + sourceIndex);
                Check(((Vector2)pose.GetType().GetField("Tip").GetValue(pose)).sqrMagnitude > .01f,
                    "side source frame is populated, not a neighboring/blank slice " + frame);
            }
            var arms = (Array)typeof(OctopusBossVisual).GetField("arms", Hidden).GetValue(visual);
            var arm = arms.GetValue(0);
            var mesh = (Mesh)arm.GetType().GetField("Mesh").GetValue(arm);
            for (int frame = 6; frame < 9; frame++)
            for (int mirror = 0; mirror < 2; mirror++)
            {
                var source = frames.GetValue(frame);
                var rect = (Rect)source.GetType().GetField("Rect").GetValue(source);
                var sourceTexture = (Texture2D)source.GetType().GetField("Texture").GetValue(source);
                int cellWidth = sourceTexture.width / 3, cellHeight = sourceTexture.height / 3;
                int cellX = frame % 3 * cellWidth, cellY = (2 - frame / 3) * cellHeight;
                var pixels = sourceTexture.GetPixels32(); bool unclipped = true;
                for (int y = 0; y < cellHeight && unclipped; y++) for (int x = 0; x < cellWidth; x++)
                {
                    if (pixels[(cellY + y) * sourceTexture.width + cellX + x].a <= 100) continue;
                    if (cellX + x < rect.x || cellX + x >= rect.xMax || cellY + y < rect.y || cellY + y >= rect.yMax)
                    { unclipped = false; break; }
                }
                Check(unclipped, "downward source alpha remains fully inside its sliced rectangle " + frame);
                Call(visual, "DrawMesh", arm, source, Vector2.zero, new Vector2(1, -5), 1f, mirror != 0);
                Check(Mathf.Abs(mesh.bounds.size.x / mesh.bounds.size.y - rect.width / rect.height) < .0001f,
                    "downward frame keeps the complete source aspect and base " + frame + "/" + mirror);
                float ppu = (float)source.GetType().GetField("Ppu").GetValue(source);
                Check(Mathf.Abs(mesh.bounds.size.x - rect.width / ppu) < .001f &&
                    Mathf.Abs(mesh.bounds.size.y - rect.height / ppu) < .001f,
                    "downward frame keeps its original dimensions without target-based scaling " + frame + "/" + mirror);
                var vertices = mesh.vertices; var indices = mesh.triangles; float sign = 0;
                for (int t = 0; t < indices.Length; t += 3)
                {
                    var a = vertices[indices[t + 1]] - vertices[indices[t]];
                    var b = vertices[indices[t + 2]] - vertices[indices[t]];
                    float cross = a.x * b.y - a.y * b.x;
                    if (t == 0) sign = Mathf.Sign(cross);
                    if (cross * sign <= .000001f) throw new Exception("downward frame has a folded/collapsed triangle");
                }
                Check(true, "downward mesh has no folded or collapsed triangles " + frame + "/" + mirror);
            }
            var arena = GameObject.Find("FiniteBeachArena");
            var cap = arena.transform.Find("Upper beach continuation").GetComponent<MeshRenderer>();
            Check(cap.bounds.min.y <= FiniteBeach.Height * .5f,
                "background continuation meets the original upper beach without a gap");
        }
        static void VerifyHeadInsideCamera(OctopusBossVisual visual, Camera camera)
        {
            var body = (SpriteRenderer)typeof(OctopusBossVisual).GetField("body", Hidden).GetValue(visual);
            var b = body.bounds;
            for (int x = 0; x < 2; x++) for (int y = 0; y < 2; y++)
            {
                var p = camera.WorldToViewportPoint(new Vector3(x == 0 ? b.min.x : b.max.x,
                    y == 0 ? b.min.y : b.max.y, body.transform.position.z - camera.transform.position.z));
                Check(p.z > 0 && p.x >= 0 && p.x <= 1 && p.y >= 0 && p.y <= 1,
                    "all four body bounds corners stay inside the live camera viewport");
            }
        }
        static void VerifyBossCameraFrame(SpriteRenderer map, Camera camera)
        {
            float expectedSize = PlayerCameraFollow.BossOrthographicSize(map.bounds);
            Check(camera.orthographic && Mathf.Abs(camera.orthographicSize - expectedSize) < .001f,
                "boss camera width exactly matches the map without stretching");
            Check(Mathf.Abs(camera.aspect - FiniteBeach.Aspect) < .001f,
                "boss camera uses the full 16:9 gameplay rectangle without side borders");
            Check(Vector2.Distance(camera.transform.position,
                    PlayerCameraFollow.BossCameraCenter(map.bounds)) < .001f,
                "boss camera keeps the map top edge and crops only lower ocean");
            var arena = OctopusBossPattern.Arena;
            foreach (var point in new[] { new Vector3(arena.xMin, arena.yMin, 0), new Vector3(arena.xMax, arena.yMin, 0),
                new Vector3(arena.xMin, arena.yMax, 0), new Vector3(arena.xMax, arena.yMax, 0) })
            {
                var p = camera.WorldToViewportPoint(point);
                Check(p.z > 0 && p.x >= 0 && p.x <= 1 && p.y >= 0 && p.y <= 1,
                    "combat arena corner remains inside the complete-map view");
            }
            Debug.Log($"[Stage5FixedZoomValidation] size={camera.orthographicSize:F4}; ratio={camera.orthographicSize / GameConfig.CameraSize:F2}; " +
                $"center={camera.transform.position}; mapBounds={map.bounds}; fullArtworkFit=true.");
        }
        static void VerifyBossViewportAspects(Bounds bounds)
        {
            foreach (float screenAspect in new[] { 4f / 3f, 16f / 9f, 21f / 9f, 9f / 16f })
            {
                var viewport = PlayerCameraFollow.AspectFitViewport(screenAspect, FiniteBeach.Aspect);
                float viewportAspect = screenAspect * viewport.width / viewport.height;
                float viewWidth = PlayerCameraFollow.BossOrthographicSize(bounds) * 2f * viewportAspect;
                Check(Mathf.Abs(viewportAspect - FiniteBeach.Aspect) < .001f &&
                    Mathf.Abs(viewWidth - bounds.size.x) < .001f,
                    "boss map fills the 16:9 camera width at screen aspect " + screenAspect.ToString("F3"));
            }
        }
        static Type EditorType(string name)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var type = assembly.GetType("UnityEditor." + name);
                if (type != null) return type;
            }
            throw new Exception("Unity editor type unavailable: " + name);
        }
        static IEnumerator VerifyBossMapModeTransitions(GameSession session, Camera camera, SpriteRenderer map)
        {
            var follow = camera.GetComponent<PlayerCameraFollow>();
            var original = session.OctopusBoss.Value;
            var bossArt = map.sprite;
            // Simulate a play-mode reload/Inspector edit while the boss-map state is
            // already active. The layout must be restored even without a mode change.
            map.transform.localPosition = Vector3.zero;
            map.transform.localScale = Vector3.one;
            ArenaBuilder.UseBossArtwork(true);
            Check(Vector3.Distance(map.transform.localPosition, new Vector3(.4f, 3.7f, 0f)) < .001f &&
                Vector3.Distance(map.transform.localScale, new Vector3(1.357988f, 1.357988f, 1f)) < .001f,
                "active boss-map mode reapplies the authored transform after runtime state drift");
            var scale = map.transform.localScale;
            var bounds = map.bounds;
            Vector2 cameraCenter = camera.transform.position;
            foreach (byte pattern in new byte[] { 2, 0, 4, 0, 2 })
            {
                var next = original; next.Pattern = pattern; session.OctopusBoss.Value = next;
                yield return null;
                Debug.Log($"[Stage5ModeTransition] Stage={session.Stage.Value}; BossAlive={session.BossAlive.Value}; " +
                    $"Pattern={pattern}; mode={(follow.BossMapCameraActive ? "BossMap" : "PlayerFollow")}; " +
                    $"size={camera.orthographicSize:F4}; center={camera.transform.position}; " +
                    $"map={map.sprite.texture.name}; bounds={map.bounds}.");
                Debug.Log($"[Stage5ModeComparison] previousPredicate={(session.OctopusBossActive ? "BossMap" : "PlayerFollow")}; " +
                    $"actualMode={(follow.BossMapCameraActive ? "BossMap" : "PlayerFollow")}; Pattern={pattern}. " +
                    "previousPredicate is the former condition evaluated against the same live state.");
                Check(map.sprite == bossArt, "BossBeach remains active for boss pattern " + pattern);
                Check(follow.BossMapCameraActive, "boss camera remains active for boss pattern " + pattern);
                Check(map.transform.localScale == scale && map.bounds == bounds,
                    "map scale, pivot and bounds remain unchanged for boss pattern " + pattern);
                VerifyBossCameraFrame(map, camera);
                Check(Vector2.Distance(camera.transform.position, cameraCenter) < .001f,
                    "boss combat framing remains fixed for boss pattern " + pattern);
            }
            session.BossAlive.Value = false;
            yield return null;
            Check(!follow.BossMapCameraActive && map.sprite != bossArt && map.sprite.texture.name == "Beach",
                "boss defeat restores player tracking and the regular map");
            session.BossAlive.Value = true;
            session.Stage.Value = 6;
            yield return null;
            Check(!follow.BossMapCameraActive && map.sprite != bossArt, "other stages retain player tracking and the regular map");
            session.Stage.Value = 5;
            session.OctopusBoss.Value = original;
            yield return null;
            Check(map.sprite == bossArt, "stage 5 live boss restores BossBeach without changing actor positions");
            byte phase = session.Phase.Value;
            session.Phase.Value = (byte)GamePhase.Waiting;
            yield return null;
            Check(!follow.BossMapCameraActive && map.sprite != bossArt, "leaving gameplay exits boss camera and map together");
            session.Phase.Value = phase;
            Call(session, "RenderLocal");
            yield return null;
            Check(follow.BossMapCameraActive && map.sprite == bossArt, "restoring gameplay restores the matching boss camera and map");
        }
        static IEnumerator VerifyLiveGameViewSizes(SpriteRenderer map, Camera camera)
        {
            const BindingFlags any = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            var viewType = EditorType("GameView");
            var sizesType = EditorType("GameViewSizes");
            var sizeType = EditorType("GameViewSize");
            var sizeKind = EditorType("GameViewSizeType");
            var groupKind = EditorType("GameViewSizeGroupType");
            var sizes = sizesType.BaseType.GetProperty("instance", BindingFlags.Public | BindingFlags.Static).GetValue(null);
            var group = sizesType.GetMethod("GetGroup", any).Invoke(sizes, new[] { Enum.Parse(groupKind, "Standalone") });
            var groupType = group.GetType();
            int builtins = (int)groupType.GetMethod("GetBuiltinCount", any).Invoke(group, null);
            int custom = (int)groupType.GetMethod("GetCustomCount", any).Invoke(group, null);
            var view = EditorWindow.GetWindow(viewType, false, "Game", false);
            var selection = viewType.GetProperty("selectedSizeIndex", any);
            int prior = (int)selection.GetValue(view);
            int added = 0;
            try
            {
                foreach (var dimensions in new[] { new Vector2Int(960, 720), new Vector2Int(1280, 720),
                    new Vector2Int(1680, 720), new Vector2Int(720, 1280) })
                {
                    var size = Activator.CreateInstance(sizeType, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                        null, new object[] { Enum.Parse(sizeKind, "FixedResolution"), dimensions.x, dimensions.y, "Stage5 map validation" }, null);
                    groupType.GetMethod("AddCustomSize", any).Invoke(group, new[] { size });
                    selection.SetValue(view, builtins + custom + added++);
                    view.Repaint();
                    for (int frame = 0; frame < 5; frame++) yield return null;
                    Check(Screen.width == dimensions.x && Screen.height == dimensions.y,
                        "live Game view resolution changed to " + dimensions);
                    VerifyBossCameraFrame(map, camera);
                    var visual = FindAnyObjectByType<OctopusBossVisual>();
                    Check(visual != null, "boss remains present after live resolution change");
                    VerifyHeadInsideCamera(visual, camera);
                    VerifyHeadDoesNotOverlapBossHud(visual, camera);
                    Debug.Log($"[Stage5LiveResize] screen={Screen.width}x{Screen.height}; rect={camera.rect}; " +
                        $"cameraSize={camera.orthographicSize:F2}; fixedZoom=2x; combatArenaInside=true.");
                }
            }
            finally
            {
                selection.SetValue(view, prior);
                for (int i = added - 1; i >= 0; i--)
                    groupType.GetMethod("RemoveCustomSize", any).Invoke(group, new object[] { custom + i });
                view.Repaint();
            }
            for (int frame = 0; frame < 5; frame++) yield return null;
        }
        static void VerifyHeadDoesNotOverlapBossHud(OctopusBossVisual visual, Camera camera)
        {
            var body = (SpriteRenderer)typeof(OctopusBossVisual).GetField("body", Hidden).GetValue(visual);
            var bossBar = GameObject.Find("BossBar");
            Check(bossBar != null && bossBar.activeInHierarchy, "stage 5 boss HUD is active for overlap check");
            var bar = bossBar.GetComponent<RectTransform>();
            var canvas = bossBar.GetComponentInParent<Canvas>();
            var uiCorners = new Vector3[4]; bar.GetWorldCorners(uiCorners);
            var uiRect = Rect.MinMaxRect(float.PositiveInfinity, float.PositiveInfinity,
                float.NegativeInfinity, float.NegativeInfinity);
            for (int i = 0; i < 4; i++)
            {
                Vector2 p = RectTransformUtility.WorldToScreenPoint(
                    canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera, uiCorners[i]);
                uiRect.xMin = Mathf.Min(uiRect.xMin, p.x); uiRect.xMax = Mathf.Max(uiRect.xMax, p.x);
                uiRect.yMin = Mathf.Min(uiRect.yMin, p.y); uiRect.yMax = Mathf.Max(uiRect.yMax, p.y);
            }
            var b = body.bounds;
            var corners = new[] { new Vector3(b.min.x, b.min.y, body.transform.position.z),
                new Vector3(b.min.x, b.max.y, body.transform.position.z),
                new Vector3(b.max.x, b.min.y, body.transform.position.z),
                new Vector3(b.max.x, b.max.y, body.transform.position.z) };
            var bodyRect = Rect.MinMaxRect(float.PositiveInfinity, float.PositiveInfinity,
                float.NegativeInfinity, float.NegativeInfinity);
            foreach (var corner in corners)
            {
                Vector3 p = camera.WorldToScreenPoint(corner);
                bodyRect.xMin = Mathf.Min(bodyRect.xMin, p.x); bodyRect.xMax = Mathf.Max(bodyRect.xMax, p.x);
                bodyRect.yMin = Mathf.Min(bodyRect.yMin, p.y); bodyRect.yMax = Mathf.Max(bodyRect.yMax, p.y);
            }
            bool overlaps = bodyRect.Overlaps(uiRect);
            Debug.Log($"[Stage5BossHudValidation] bodyScreen={bodyRect} hudScreen={uiRect} " +
                $"bodyWorld={b} cameraSize={camera.orthographicSize:F2} cameraRect={camera.rect} overlaps={overlaps}.");
            Check(!overlaps, "octopus body does not overlap the top boss HUD");
        }
        static void VerifySideContactPose(OctopusBossVisual visual)
        {
            var snap = new OctopusBossSnapshot { Pattern = 4, Half = 2, Seed = 12345,
                Time = OctopusBossPattern.HalfAttackStart + .32f };
            var root = OctopusBossPattern.HalfRoot(2, 0);
            Call(visual, "DrawSideHalfArm", snap, 0, root);
            var arms = (Array)typeof(OctopusBossVisual).GetField("arms", Hidden).GetValue(visual);
            var arm = arms.GetValue(0); var mesh = (Mesh)arm.GetType().GetField("Mesh").GetValue(arm);
            var contact = typeof(OctopusBossVisual).GetField("contact", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            var rect = (Rect)contact.GetType().GetField("Rect").GetValue(contact);
            float ppu = (float)contact.GetType().GetField("Ppu").GetValue(contact);
            var go = (GameObject)arm.GetType().GetField("Root").GetValue(arm);
            Check(Vector2.Distance(go.transform.position, root) < .001f, "side contact preserves the authored sand-mouth root");
            Check(Mathf.Abs(mesh.bounds.size.x - rect.width / ppu) < .001f &&
                Mathf.Abs(mesh.bounds.size.y - rect.height / ppu) < .001f,
                "side contact uses the full unwarped long-contact source frame");
        }
        static void VerifyLegGate(GameSession s, SimActor boss, List<SimActor> actors)
        {
            Call(s, "StartOctopusBoss", boss);
            float hp = boss.Hp;
            Call(s, "ApplyDamage", boss, 100f, StatusMask.Burn, 5f, 20f);
            Check(boss.Hp == hp && boss.BurnTime <= 0, "protected core rejects direct damage and damage-over-time status");
            Check(!(bool)Call(s, "CanTargetActor", boss), "automatic attacks exclude the protected core");
            var limbs = (SimActor[])typeof(GameSession).GetField("octopusArms", Hidden).GetValue(s);
            Check(limbs.Length == 6 && limbs[0].Id != limbs[1].Id, "six independent damageable limb actors");
            float before = limbs[0].Hp;
            s.DamageCircle(limbs[0].Pos, .2f, 10f, StatusMask.None, 0, 0);
            Check(Mathf.Abs(limbs[0].Hp - before + 10) < .01f && limbs[1].Hp == limbs[1].MaxHp,
                "area damage reduces only the contacted limb HP");
            var projectile = (SimActor)Call(s, "Create", ActorKind.Droplet, limbs[0].Pos + Vector2.right * 4, .3f);
            projectile.Damage = 7;
            bool hit = (bool)Call(s, "HitProjectileSegment", projectile, limbs[0].Pos - Vector2.right * 4);
            Check(hit && Mathf.Abs(limbs[0].Hp - before + 17) < .01f, "projectiles hit the visible limb collider");
            projectile.Dead = true;
            Call(s, "ApplyDamage", limbs[0], 100000f, StatusMask.None, 0f, 0f);
            Check(s.OctopusBoss.Value.LivingArms == 5 && s.OctopusBoss.Value.Vulnerable == 0, "one broken limb does not expose the core");
            Call(s, "ApplyDamage", limbs[1], limbs[1].MaxHp * .55f, StatusMask.None, 0f, 0f);
            Call(s, "RenderLocal"); FindFirstObjectByType<OctopusBossVisual>().Preview(s.OctopusBoss.Value);
            Capture(Camera.main, "Logs/OctopusBossFrames/limb-hp.png");
            for (int i = 1; i < 6; i++) Call(s, "ApplyDamage", limbs[i], 100000f, StatusMask.None, 0f, 0f);
            Check(s.OctopusBoss.Value.LivingArms == 0 && s.OctopusBoss.Value.Pattern == 5 &&
                Mathf.Abs(s.OctopusBoss.Value.Vulnerable - 10) < .001f, "last limb immediately interrupts attacks for exactly ten seconds");
            Call(s, "TickBossPattern", boss, 3f, 0f);
            Check(Mathf.Abs(s.OctopusBoss.Value.Vulnerable - 7) < .001f, "stun timer counts combat seconds");
            FindFirstObjectByType<OctopusBossVisual>().Preview(s.OctopusBoss.Value);
            Capture(Camera.main, "Logs/OctopusBossFrames/core-open.png");
            Call(s, "ApplyDamage", boss, 13f, StatusMask.None, 0f, 0f);
            Check(Mathf.Abs(boss.Hp - hp + 13) < .01f, "core becomes damageable during stun");
            Call(s, "TickBossPattern", boss, 6.99f, 0f);
            Check(s.OctopusBoss.Value.Vulnerable > 0, "core remains open just before ten seconds");
            Call(s, "TickBossPattern", boss, .02f, 0f);
            Check(s.OctopusBoss.Value.Vulnerable == 0 && s.OctopusBoss.Value.LivingArms == 6 &&
                s.OctopusBoss.Value.Pattern == 2, "ten-second expiry restores all limbs and restarts attacks");
            for (int i = 0; i < 6; i++) Check(s.OctopusBoss.Value.ArmHealth(i) == 1, "restored limb HP is full");
            float after = boss.Hp; Call(s, "ApplyDamage", boss, 100f, StatusMask.None, 0f, 0f);
            Check(boss.Hp == after, "restored limbs protect the core again without healing core HP");
            Check(boss.Hp < hp, "core damage persists between vulnerability cycles");
        }
        static void CheckGeometry()
        {
            int[] perArm = new int[6];
            for (int n = 0; n < 30; n++)
            {
                perArm[OctopusBossPattern.RelayArm(n)]++;
                Check(OctopusBossPattern.Arena.Contains(OctopusBossPattern.RelayTarget(12345, n)), "relay target inside arena " + n);
                if (n > 0) Check(Mathf.Abs(OctopusBossPattern.RelayContact(n) - OctopusBossPattern.RelayContact(n - 1) - .16f) < .00001f, "0.16s contact interval");
            }
            for (int a = 0; a < 6; a++) Check(perArm[a] == 5, "five contacts for arm " + a);
            for (int half = 0; half < 4; half++)
            {
                var r = OctopusBossPattern.HalfRect(half);
                Check(Mathf.Abs(r.width * r.height * 2 - OctopusBossPattern.Arena.width * OctopusBossPattern.Arena.height) < .01f, "exact half area " + half);
                if (half < 2)
                {
                    Check(Mathf.Abs(OctopusBossPattern.HalfCell(half, 0).width * 6 - r.width) < .01f &&
                        Mathf.Abs(OctopusBossPattern.HalfCell(half, 0).height - r.height) < .01f,
                        "vertical barrage uses one row of six horizontal lanes " + half);
                }
                else
                {
                    Check(Mathf.Abs(OctopusBossPattern.HalfCell(half, 0).width - r.width) < .01f &&
                        Mathf.Abs(OctopusBossPattern.HalfCell(half, 0).height * 6 - r.height) < .01f,
                        "side barrage uses six vertical lanes " + half);
                }
                for (int a = 0; a < 6; a++)
                {
                    Check(r.Contains(OctopusBossPattern.HalfRoot(half, a)), "all six emerge inside selected half");
                    if (a > 0 && half < 2)
                        Check(OctopusBossPattern.HalfRoot(half, a).x > OctopusBossPattern.HalfRoot(half, a - 1).x,
                            "vertical lanes progress left-to-right " + half + "/" + a);
                    if (a > 0 && half >= 2)
                        Check(OctopusBossPattern.HalfRoot(half, a).y < OctopusBossPattern.HalfRoot(half, a - 1).y,
                            "side roots progress down the lane " + half + "/" + a);
                    if (half >= 2)
                    {
                        float inset = r.width * .12f;
                        float expectedX = half == 2
                            ? (a % 2 == 0 ? r.xMin + inset : r.xMax - inset)
                            : (a % 2 == 0 ? r.xMax - inset : r.xMin + inset);
                        Check(Mathf.Abs(OctopusBossPattern.HalfRoot(half, a).x - expectedX) < .001f,
                            "side roots alternate across both edges in a zigzag " + half + "/" + a);
                    }
                    int contacts = 0;
                    for (int cycle = 0; cycle < 13; cycle++)
                    {
                        float t = OctopusBossPattern.HalfContact(half, a, cycle);
                        if (half >= 2)
                            Check(Mathf.Abs(t - (OctopusBossPattern.HalfAttackStart + .32f + a * .035f + cycle * OctopusBossPattern.SideSlamInterval)) < .00001f,
                                "side damage timing matches long-contact pose " + half + "/" + a + "/" + cycle);
                        if (t >= 4.65f) continue;
                        Check(t >= 1.65f, "no damage before the 3-second active window"); contacts++;
                        var target = OctopusBossPattern.HalfTarget(12345, half, a, cycle);
                        Check(r.Contains(target) && OctopusBossPattern.HalfCanHit(target, half, target), "broad physical footprint in half");
                        if (half >= 2)
                            Check(Mathf.Abs(target.x - r.center.x) < r.width * .05f,
                                "side strikes stay in the central zigzag contact band " + half + "/" + a);
                        var safe = OctopusBossPattern.Arena.center * 2 - target;
                        Check(!OctopusBossPattern.HalfCanHit(safe, half, target), "safe half cannot receive slam damage");
                    }
                    Check(contacts >= (half < 2 ? 11 : 6), "every arm slams throughout the 3-second window");
                }
            }
        }
        static void Capture(Camera camera, string path)
        {
            var prior = RenderTexture.active; var rt = RenderTexture.GetTemporary(1280, 720, 24);
            var tex = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            try
            {
                RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = rt });
                RenderTexture.active = rt; tex.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); tex.Apply();
                File.WriteAllBytes(path, tex.EncodeToPNG());
            }
            finally { RenderTexture.active = prior; RenderTexture.ReleaseTemporary(rt); Destroy(tex); }
        }
    }
}
