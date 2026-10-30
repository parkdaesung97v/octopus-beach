using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace OctopusSurvivors.EditorTools
{
    public static class CharacterVisualChecks
    {
        [MenuItem("문어/캐릭터 및 진주 표시 검증")]
        public static void Verify()
        {
            CheckPlayerPoses();
            var animations = new[] { OctopusAnim.Idle, OctopusAnim.Move, OctopusAnim.Stop, OctopusAnim.Hit, OctopusAnim.Victory };
            var counts = new[] { 12, 8, 3, 3, 16 };
            for (int a = 0; a < animations.Length; a++)
            {
                if (OctopusSheet.FrameCount(animations[a]) != counts[a]) throw new Exception("Character frame count mismatch");
                for (int i = 0; i < counts[a]; i++)
                {
                    var sprite = OctopusSheet.Frame(animations[a], i);
                    if (sprite == null || sprite.bounds.size.y < 1f) throw new Exception("Invalid character frame");
                    if (sprite.texture.GetPixel(0, 0).a != 0f) throw new Exception("Character padding missing");
                }
            }
            if (System.Array.IndexOf(GameplayRules.BasicElements, ElementId.Electric) < 0 ||
                Resources.Load<TextAsset>("Art/Element_Electric") == null || RuntimeArt.ElementIcon(ElementId.Electric) == null)
                throw new Exception("Electric selection/HUD asset missing");
            Debug.Log("CHARACTER_ASSETS_PASSED: 42 supplied frames and electric UI resources loaded.");
            CheckSheet("Basic", 6, (r, c) => RuntimeArt.Basic(r, c));
            Vector2[] directions = { Vector2.down, Vector2.left, Vector2.right, Vector2.up };
            CheckSheet("Attack", 4, (r, c) => RuntimeArt.Attack(directions[r], c));
            for (int i = 0; i < 8; i++)
                if (RuntimeArt.Whip(i).bounds.size.x <= 0f) throw new Exception("Invalid whip bounds");
            var root = new GameObject("PearlCheck");
            var stack = root.AddComponent<PearlStackVisual>();
            foreach (int count in new[] { 0, 1, 2, 7, 21, 30, 2, 0 })
            {
                stack.Refresh(count, 1);
                if (root.GetComponentInChildren<TextMesh>().text != count + " / 2")
                    throw new Exception("Pearl counter mismatch");
                if (root.GetComponentsInChildren<SpriteRenderer>().Length != Mathf.Min(count, 21))
                    throw new Exception("Pearl stack size mismatch");
            }
            stack.Refresh(5, 2);
            stack.Refresh(0, 3);
            if (root.GetComponentsInChildren<SpriteRenderer>().Length != 0)
                throw new Exception("Pearl stack was not reset for the next stage");
            UnityEngine.Object.DestroyImmediate(root);
            Debug.Log("CHARACTER_VISUAL_CHECKS_PASSED: all 80 character frames retain every opaque pixel, padding is intact, pearl counter handles reset and overflow.");
        }

        static void CheckPlayerPoses()
        {
            const BindingFlags hidden = BindingFlags.Instance | BindingFlags.NonPublic;
            var instance = typeof(GameSession).GetField("<Instance>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic);
            var previous = instance.GetValue(null);
            var root = new GameObject("CharacterPoseCheck");
            try
            {
                var session = root.AddComponent<GameSession>();
                var player = root.AddComponent<OctopusPlayer>();
                var renderer = root.AddComponent<SpriteRenderer>();
                instance.SetValue(null, session);
                typeof(OctopusPlayer).GetField("_bodySr", hidden).SetValue(player, renderer);
                var pose = typeof(OctopusPlayer).GetMethod("PoseSprite", hidden);
                var visualPose = typeof(OctopusPlayer).GetField("_visualPose", hidden);
                var facing = typeof(OctopusPlayer).GetMethod("UpdateSpriteFacing", hidden);
                session.Phase.Value = (byte)GamePhase.Playing;
                foreach (int stage in new[] { 1, 4, 5, 6, 10 })
                foreach (var flow in new[] { StageState.Combat, StageState.Choosing, StageState.Countdown })
                {
                    session.Stage.Value = stage;
                    session.StageFlow.Value = (byte)flow;
                    var expected = stage % GameConfig.BossEvery == 0 && flow != StageState.Combat
                        ? OctopusAnim.Victory : OctopusAnim.Idle;
                    pose.Invoke(player, new object[] { false });
                    if ((OctopusAnim)visualPose.GetValue(player) != expected)
                        throw new Exception($"Unexpected pose at stage {stage}, {flow}");
                }
                // A living boss, defeat, or lobby must never trigger victory.
                foreach (int blocked in new[] { 0, 1, 2 })
                {
                    session.BossAlive.Value = blocked == 0;
                    session.GameOver.Value = blocked == 1;
                    session.Phase.Value = (byte)(blocked == 2 ? GamePhase.Waiting : GamePhase.Playing);
                    pose.Invoke(player, new object[] { false });
                    if ((OctopusAnim)visualPose.GetValue(player) != OctopusAnim.Idle)
                        throw new Exception("Victory outside boss clear");
                }
                session.Phase.Value = (byte)GamePhase.Playing;
                session.Stage.Value = 1;
                pose.Invoke(player, new object[] { true });
                if ((OctopusAnim)visualPose.GetValue(player) != OctopusAnim.Move)
                    throw new Exception("Ordinary stage clear must allow walking");
                Vector2[] directions = { Vector2.left, Vector2.up, Vector2.zero, Vector2.right, Vector2.down };
                bool[] flipped = { false, false, false, true, true };
                for (int i = 0; i < directions.Length; i++)
                {
                    player.Facing.Value = directions[i];
                    facing.Invoke(player, null);
                    if (renderer.flipX != flipped[i]) throw new Exception("Horizontal facing was lost");
                }
                Debug.Log("CHARACTER_POSES_PASSED: boss-only victory, ordinary-stage walking, and persistent left/right facing.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
                instance.SetValue(null, previous);
            }
        }

        static void CheckSheet(string name, int rows, Func<int, int, Sprite> frame)
        {
            var original = new Texture2D(2, 2);
            ImageConversion.LoadImage(original, Resources.Load<TextAsset>("Art/" + name).bytes);
            int before = 0, after = 0;
            foreach (var p in original.GetPixels32()) if (p.a != 0) before++;
            for (int r = 0; r < rows; r++)
            for (int c = 0; c < 8; c++)
            {
                var sprite = frame(r, c);
                var texture = sprite.texture;
                var pixels = texture.GetPixels32();
                for (int y = 0; y < texture.height; y++)
                for (int x = 0; x < texture.width; x++)
                {
                    if (pixels[y * texture.width + x].a == 0) continue;
                    after++;
                    if (x == 0 || y == 0 || x == texture.width - 1 || y == texture.height - 1)
                        throw new Exception(sprite.name + ": clipped edge");
                }
            }
            UnityEngine.Object.DestroyImmediate(original);
            if (before != after) throw new Exception(name + ": lost or duplicated pixels " + before + " != " + after);
        }
    }
}
