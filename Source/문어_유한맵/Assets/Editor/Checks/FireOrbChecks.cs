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
    public static class FireOrbChecks
    {
        const string Key = "Octopus.FireOrbCheck";
        static FireOrbChecks()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Key, false))
                    new GameObject("Fire orb integration checks").AddComponent<FireOrbCapture>();
                if (state == PlayModeStateChange.EnteredEditMode) SessionState.SetBool(Key, false);
            };
        }
        [MenuItem("문어/불 왕복 검증")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
            Directory.CreateDirectory("Logs"); File.WriteAllText("Logs/fire-orb-check.status", "RUNNING");
            ShaderUtil.allowAsyncCompilation = false;
            SessionState.SetBool(Key, true); EditorApplication.EnterPlaymode();
        }
        internal static void Finish(bool pass)
        {
            File.WriteAllText("Logs/fire-orb-check.status", pass ? "PASS" : "FAIL");
            EditorApplication.isPlaying = false;
        }
    }

    public sealed class FireOrbCapture : MonoBehaviour
    {
        static readonly BindingFlags Hidden = BindingFlags.NonPublic | BindingFlags.Instance;
        IEnumerator run; float deadline; bool done;
        static object Call(object o, string method, params object[] args) => o.GetType().GetMethod(method, Hidden).Invoke(o, args);
        static List<SimActor> Actors(GameSession s) => (List<SimActor>)typeof(GameSession).GetField("_actors", Hidden).GetValue(s);
        static void Check(bool pass, string text) { if (!pass) throw new Exception(text); Debug.Log("FIRE ORB PASS: " + text); }
        static void Clear(GameSession s) { Call(s, "ClearFieldForNextStage"); Actors(s).RemoveAll(a => a.Dead); }
        static SimActor Target(GameSession s, Vector2 pos)
        {
            var e = (SimActor)Call(s, "Create", ActorKind.Enemy, pos, .7f);
            e.Pos = pos; e.Hp = e.MaxHp = 10000; e.StunTime = 100; e.Element = ElementId.Grass; return e;
        }
        void Start() { deadline = Time.realtimeSinceStartup + 120; run = Run(); }
        void Update()
        {
            if (done) return;
            try
            {
                if (Time.realtimeSinceStartup > deadline) throw new TimeoutException("Fire checks timed out");
                if (!run.MoveNext()) { done = true; FireOrbChecks.Finish(true); }
            }
            catch (Exception e) { Debug.LogError("FIRE ORB CHECK FAILED: " + e); done = true; FireOrbChecks.Finish(false); }
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
            s.SelectStartingElement(ElementId.Fire);
            while (s.HostElement.Value == 0) yield return null;
            s.ToggleLocalReady(); while (!s.CanStart()) yield return null; s.RequestStart();
            while (!s.IsPlaying || OctopusPlayer.Local == null) yield return null;
            var p = OctopusPlayer.Local; p.enabled = s.enabled = false;
            p.UpAttrRank.Value = 0; p.Facing.Value = Vector2.right; p.transform.position = Vector3.zero; p.NetPos.Value = Vector2.zero;
            p.Slot0.Value = p.Slot1.Value = p.Slot2.Value = p.Slot3.Value = 0;
            Debug.Log("FIRE ORB CHECKS BEGIN");
            float baseline = -1;
            foreach (float dt in new[] { 1f/30, 1f/60, 1f/144, .25f })
            {
                Clear(s);
                var near = Target(s, new Vector2(3, 0));
                var distant = Target(s, new Vector2(-20, 15));
                s.CastBasicAbility(p, ElementId.Fire, 1);
                var orbs = Actors(s).FindAll(a => a.Kind == ActorKind.FlameAura);
                Check(orbs.Count == 3, "exactly three shots at dt=" + dt);
                int guard = 0;
                while (orbs.Exists(a => !a.Dead) && guard++ < 2000)
                    foreach (var orb in orbs) if (!orb.Dead) Call(s, "TickPlannedAbility", orb, dt);
                float damage = near.MaxHp - near.Hp;
                Check(damage > 0 && distant.Hp == distant.MaxHp, "contact damage and untouched remote enemy at dt=" + dt);
                Check(orbs.TrueForAll(a => a.Dead && Vector2.Distance(a.Pos, p.Pos) < .01f), "all shots return and finish at dt=" + dt);
                if (baseline < 0) baseline = damage;
                Check(Mathf.Abs(damage - baseline) < 1.1f, "damage remains stable across frame rates at dt=" + dt);
            }
            Clear(s); s.CastBasicAbility(p, ElementId.Fire, 1);
            var center = Actors(s).Find(a => a.Kind == ActorKind.FlameAura && a.Bounces == 1);
            var waiting = Target(s, p.Pos);
            Call(s, "TickPlannedAbility", center, .08f);
            Check(waiting.Hp == waiting.MaxHp, "delayed shot does not damage before launch");
            waiting.Dead = true;
            Call(s, "TickPlannedAbility", center, .52f);
            Vector2 tailPos = FireOrbGeometry.Position(center.Anchor, center.SkillPoint, center.SkillTime, 1, .38f, center.Duration, p.Pos);
            var tailOnly = Target(s, tailPos);
            var samePlace = Target(s, tailPos + new Vector2(0, .05f));
            var boss = Target(s, tailPos); boss.Kind = ActorKind.Boss;
            Check(Vector2.Distance(tailPos, center.Pos) > center.Size + GameConfig.EnemyRadius, "tail target is outside the head");
            Call(s, "TickPlannedAbility", center, .02f);
            Check(tailOnly.Hp < tailOnly.MaxHp && samePlace.Hp < samePlace.MaxHp && boss.Hp < boss.MaxHp, "tail hits all overlapping enemies and boss");
            Check(tailOnly.MaxHp - tailOnly.Hp <= center.Damage * .02f + .01f, "overlapping tail segments do not multiply DPS");
            var far = Target(s, new Vector2(200, 200));
            Call(s, "TickPlannedAbility", center, .1f);
            Vector2 expected = FireOrbGeometry.Position(center.Anchor, center.SkillPoint, center.SkillTime, 1, center.Age, center.Duration, p.Pos);
            Check(Vector2.Distance(center.Pos, expected) < .001f, "initial fan throw stays unchanged before targeting");
            far.Dead = true;
            while (center.Age < 3.9f) Call(s, "TickPlannedAbility", center, Mathf.Min(.03f, 3.9f - center.Age));
            var returning = Target(s, new Vector2(1.4f, 0));
            while (!center.Dead) Call(s, "TickPlannedAbility", center, .02f);
            Check(returning.Hp < returning.MaxHp, "recall path still damages enemies");
            Clear(s); s.CastBasicAbility(p, ElementId.Fire, 1);
            var moving = Actors(s).Find(a => a.Kind == ActorKind.FlameAura && a.Bounces == 1);
            Call(s, "TickPlannedAbility", moving, 3.9f);
            p.transform.position = new Vector3(1, 1, 0); p.NetPos.Value = p.Pos;
            while (!moving.Dead) Call(s, "TickPlannedAbility", moving, .03f);
            Check(Vector2.Distance(moving.Pos, p.Pos) < .01f, "recall returns to the moving owner rather than the cast point");
            Check(GameConfig.ActorBytes == 25, "existing snapshot record preserved");
            ValidateInitialAim(s, p);
            ValidateAutoTargets(s, p);

            Clear(s); p.transform.position = new Vector3(-5, 0, 0); p.NetPos.Value = p.Pos;
            for (int i = 0; i < 22; i++)
            {
                var enemy = Target(s, new Vector2(-1 + (i % 6) * 1.2f, -2.5f + (i / 6) * 1.55f));
                enemy.Hp = enemy.MaxHp = 120;
            }
            s.CastBasicAbility(p, ElementId.Fire, 1);
            var preview = Actors(s).FindAll(a => a.Kind == ActorKind.FlameAura);
            var camera = Camera.main; var follow = camera.GetComponent<PlayerCameraFollow>(); if (follow != null) follow.enabled = false;
            camera.orthographicSize = 5; camera.aspect = 16f / 9f; camera.transform.position = new Vector3(0, 0, -10);
            string output = Path.GetFullPath("Build/Validation/FireOrbFrames"); Directory.CreateDirectory(output);
            Time.captureDeltaTime = 1f / 30;
            for (int frame = 0; frame < 150; frame++)
            {
                foreach (var orb in preview) if (!orb.Dead) Call(s, "TickPlannedAbility", orb, 1f/30);
                Call(s, "RenderLocal");
                yield return null;
                if (frame % 3 == 0) Capture(camera, Path.Combine(output, "frame-" + (frame / 3).ToString("D3") + ".png"));
            }
            Check(UnityEngine.Object.FindObjectsByType<FireOrbVisual>(FindObjectsSortMode.None).Length == 0, "visuals retire with recalled actors");
            Time.captureDeltaTime = 0;
            Debug.Log("FIRE ORB CHECKS PASS: fan launch / distributed auto-targets / target rotation / head and tail union / recall / stable FPS / live capture");
        }
        static void ValidateAutoTargets(GameSession s, OctopusPlayer p)
        {
            Clear(s); p.transform.position = Vector3.zero; p.NetPos.Value = Vector2.zero;
            for (int i = 0; i < 6; i++) Target(s, new Vector2(4 + i % 2 * 2, (i / 2 - 1) * 2));
            s.CastBasicAbility(p, ElementId.Fire, 1);
            var orbs = Actors(s).FindAll(a => a.Kind == ActorKind.FlameAura);
            foreach (var orb in orbs)
            {
                Call(s, "TickPlannedAbility", orb, .5f);
                float initialReach = Mathf.Min(orb.SkillTime, Vector2.Distance(orb.Target.Pos, orb.Anchor));
                Vector2 initial = FireOrbGeometry.Position(orb.Anchor, orb.SkillPoint, initialReach, orb.Bounces, orb.Age, orb.Duration, p.Pos);
                Check(Vector2.Distance(orb.Pos, initial) < .001f, "enemy-directed initial throw retained for shot " + orb.Bounces);
            }
            Check(Mathf.Abs(FireOrbGeometry.Launch(1) - FireOrbGeometry.Launch(0) - .16f) < .001f,
                "three shots retain the 0.16 second stagger");
            foreach (var orb in orbs) Call(s, "TickPlannedAbility", orb, .55f);
            var chosen = new HashSet<ushort>();
            foreach (var orb in orbs) if (orb.Target != null) chosen.Add(orb.Target.Id);
            Check(chosen.Count == 3, "three orbs acquire distinct monsters when available");
            var tracking = orbs[2]; var movingTarget = tracking.Target;
            movingTarget.Pos += new Vector2(.5f, .4f);
            float distance = Vector2.Distance(tracking.Pos, movingTarget.Pos);
            Call(s, "TickPlannedAbility", tracking, .03f);
            Check(tracking.Target == movingTarget && Vector2.Distance(tracking.Pos, movingTarget.Pos) < distance,
                "locked orb follows a moving monster");
            var histories = new Dictionary<ushort, HashSet<ushort>>();
            foreach (var orb in orbs) histories[orb.Id] = new HashSet<ushort>();
            for (int frame = 0; frame < 40; frame++) foreach (var orb in orbs)
            {
                Call(s, "TickPlannedAbility", orb, 1f/30);
                if (orb.Target != null) histories[orb.Id].Add(orb.Target.Id);
            }
            var all = new HashSet<ushort>();
            foreach (var orb in orbs)
            { Check(histories[orb.Id].Count >= 2, "shot " + orb.Bounces + " alternates between monsters"); all.UnionWith(histories[orb.Id]); }
            Check(all.Count >= 5, "attacks are distributed across the group rather than focused on one monster");
            var dead = orbs[0].Target; dead.Dead = true;
            Call(s, "TickPlannedAbility", orbs[0], .02f);
            Check(orbs[0].Target != null && orbs[0].Target != dead && !orbs[0].Target.Dead,
                "dead targets are replaced immediately");
            foreach (var actor in Actors(s)) if (actor.Kind == ActorKind.Enemy || actor.Kind == ActorKind.Boss) actor.Dead = true;
            foreach (var orb in orbs) Call(s, "TickPlannedAbility", orb, .1f);
            Check(orbs.TrueForAll(a => a.Target == null && !a.Dead), "empty battlefield keeps the skill alive safely");
            var only = Target(s, new Vector2(2, 0));
            foreach (var orb in orbs) Call(s, "TickPlannedAbility", orb, .1f);
            Check(orbs.TrueForAll(a => a.Target == only), "one remaining enemy is still attacked by all three shots");
            foreach (int count in new[] { 2, 3 })
            {
                Clear(s);
                for (int i = 0; i < count; i++) Target(s, new Vector2(4, -2 + i * 2));
                s.CastBasicAbility(p, ElementId.Fire, 1);
                var smallGroup = Actors(s).FindAll(a => a.Kind == ActorKind.FlameAura);
                var visited = new Dictionary<ushort, HashSet<ushort>>();
                foreach (var orb in smallGroup) visited[orb.Id] = new HashSet<ushort>();
                for (int frame = 0; frame < 90; frame++) foreach (var orb in smallGroup)
                {
                    Call(s, "TickPlannedAbility", orb, 1f/30);
                    if (orb.Target != null) visited[orb.Id].Add(orb.Target.Id);
                }
                foreach (var orb in smallGroup) Check(visited[orb.Id].Count >= 2,
                    "shot " + orb.Bounces + " rotates even with only " + count + " monsters");
            }
        }
        static void ValidateInitialAim(GameSession s, OctopusPlayer p)
        {
            Clear(s); p.transform.position = Vector3.zero; p.NetPos.Value = Vector2.zero; p.Facing.Value = Vector2.right;
            Target(s, new Vector2(-2, 0)); Target(s, new Vector2(-3, -1)); Target(s, new Vector2(-3, 1));
            s.CastBasicAbility(p, ElementId.Fire, 1);
            var orbs = Actors(s).FindAll(a => a.Kind == ActorKind.FlameAura);
            var initial = new HashSet<ushort>();
            foreach (var orb in orbs)
            {
                Check(orb.Target != null, "shot " + orb.Bounces + " reserves an enemy before launch");
                initial.Add(orb.Target.Id);
                Call(s, "TickPlannedAbility", orb, FireOrbGeometry.Launch(orb.Bounces) + .2f);
                Check(orb.Pos.x < 0 && Vector2.Dot(orb.SkillPoint, (orb.Target.Pos - orb.Anchor).normalized) > .999f,
                    "shot " + orb.Bounces + " launches toward enemies behind the player rather than facing direction");
            }
            Check(initial.Count == 3, "initial shots reserve three distinct targets");
            Clear(s);
            var close = Target(s, new Vector2(-1, 0));
            s.CastBasicAbility(p, ElementId.Fire, 1);
            var first = Actors(s).Find(a => a.Kind == ActorKind.FlameAura && a.Bounces == 0);
            Call(s, "TickPlannedAbility", first, UpgradeTuning.Data.fireOrbThrowTime);
            Check(Vector2.Distance(first.Pos, close.Pos) < .01f, "close enemy is not overshot to maximum throw distance");
            Clear(s);
            var dying = Target(s, new Vector2(-2, 0));
            s.CastBasicAbility(p, ElementId.Fire, 1);
            var delayed = Actors(s).Find(a => a.Kind == ActorKind.FlameAura && a.Bounces == 2);
            dying.Dead = true; var replacement = Target(s, new Vector2(0, -2));
            Call(s, "TickPlannedAbility", delayed, FireOrbGeometry.Launch(2) + .1f);
            Check(delayed.Target == replacement && delayed.Pos.y < 0 && Mathf.Abs(delayed.Pos.x) < .01f,
                "delayed shot re-aims when its reserved enemy dies before launch");
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
