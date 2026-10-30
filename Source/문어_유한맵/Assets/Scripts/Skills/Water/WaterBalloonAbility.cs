using UnityEngine;

namespace OctopusSurvivors
{
    public static class WaterBalloonGeometry
    {
        public const int Count = 5;
        public const float ReadyTime = .55f, LaunchGap = .22f, FlightTime = .5f;
        public const float BurstRadius = 1.9f, BurstTime = .95f, Diameter = .7f;
        public static bool Supports(ActorKind kind) => kind == ActorKind.WaterBalloonShot || kind == ActorKind.WaterBalloonBurst;
        public static float Launch(int index) => ReadyTime + index * LaunchGap;
        public static int CountForLevel(int level) => Count + Mathf.Clamp(level, 1, 6) - 1;
        public static Vector2 Slot(int index, int count = Count)
        {
            float angle = Mathf.Lerp(150f, 30f, Mathf.Clamp01(index / (float)Mathf.Max(1, count - 1))) * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(angle) * 1.25f, .1f + Mathf.Sin(angle) * 1.25f);
        }
        public static Vector2 Position(Vector2 from, Vector2 to, float progress)
        {
            float u = Mathf.Clamp01(progress);
            return Vector2.Lerp(from, to, u) + Vector2.up * (Mathf.Sin(u * Mathf.PI) * 1.3f);
        }
    }

    public sealed partial class GameSession
    {
        void CastWaterBalloons(OctopusPlayer owner, Vector2 fallback, int level, float multiplier)
        {
            // Five independent actors keep staggered casts and network snapshots self-contained.
            for (int i = 0; i < WaterBalloonGeometry.CountForLevel(level); i++)
            {
                var shot = Effect(ActorKind.WaterBalloonShot, owner.Pos + WaterBalloonGeometry.Slot(i, WaterBalloonGeometry.CountForLevel(level)), fallback,
                    WaterBalloonGeometry.Diameter, WaterBalloonGeometry.Launch(i) + WaterBalloonGeometry.FlightTime,
                    27f * multiplier, owner, level);
                shot.Bounces = i;
            }
        }

        Vector2 WaterBalloonTarget(Vector2 origin, Vector2 fallback, float range, int index)
        {
            // Choose a dense splash-sized group; a single nearby enemy cannot outweigh a distant crowd.
            SimActor best = null;
            int bestCount = 0;
            float nearest = float.PositiveInfinity;
            float range2 = range * range, cluster2 = WaterBalloonGeometry.BurstRadius * WaterBalloonGeometry.BurstRadius;
            foreach (var candidate in _actors)
            {
                if (candidate.Dead || (candidate.Kind != ActorKind.Enemy && candidate.Kind != ActorKind.Boss)) continue;
                float distance = (candidate.Pos - origin).sqrMagnitude;
                if (distance > range2) continue;
                int count = 0;
                foreach (var other in _actors)
                    if (!other.Dead && (other.Kind == ActorKind.Enemy || other.Kind == ActorKind.Boss) &&
                        (other.Pos - origin).sqrMagnitude <= range2 && (other.Pos - candidate.Pos).sqrMagnitude <= cluster2) count++;
                if (count > bestCount || (count == bestCount && distance < nearest))
                { best = candidate; bestCount = count; nearest = distance; }
            }
            if (fallback.sqrMagnitude < .001f) fallback = Vector2.right;
            if (best == null) return GameConfig.ClampToMap(origin + fallback.normalized * range, .2f);
            Vector2 center = Vector2.zero;
            int total = 0;
            foreach (var other in _actors)
                if (!other.Dead && (other.Kind == ActorKind.Enemy || other.Kind == ActorKind.Boss) &&
                    (other.Pos - origin).sqrMagnitude <= range2 && (other.Pos - best.Pos).sqrMagnitude <= cluster2)
                { center += other.Pos; total++; }
            center /= Mathf.Max(1, total);
            Vector2 heading = center - origin;
            float spread = index == 0 ? -.4f : index == 1 ? .35f : index == 2 ? -.18f : index == 3 ? .48f : 0f;
            center += new Vector2(-heading.y, heading.x).normalized * spread;
            center = origin + Vector2.ClampMagnitude(center - origin, range);
            return GameConfig.ClampToMap(center, .2f);
        }

        bool TickWaterBalloon(SimActor shot, float dt)
        {
            if (shot.Dead) return true;
            if (shot.Owner == null || !shot.Owner.IsSpawned || shot.Owner.Downed.Value)
            { shot.Dead = true; return true; }
            shot.Age += Mathf.Max(0, dt);
            shot.Life -= Mathf.Max(0, dt);
            if (shot.Kind == ActorKind.WaterBalloonBurst)
            { if (shot.Age >= WaterBalloonGeometry.BurstTime) shot.Dead = true; return true; }
            float launch = shot.Duration - WaterBalloonGeometry.FlightTime;
            if (shot.Age < launch)
            {
                shot.Pos = shot.Owner.Pos + WaterBalloonGeometry.Slot(shot.Bounces, WaterBalloonGeometry.CountForLevel(shot.EffectLevel)) +
                    Vector2.up * (Mathf.Sin(shot.Age * 4f + shot.Bounces) * .035f);
                return true;
            }
            if (!shot.Fallen)
            {
                shot.Fallen = true;
                shot.Anchor = shot.Owner.Pos + WaterBalloonGeometry.Slot(shot.Bounces, WaterBalloonGeometry.CountForLevel(shot.EffectLevel));
                shot.SkillPoint = WaterBalloonTarget(shot.Owner.Pos, shot.Vel, 7f + .25f * (shot.EffectLevel - 1), shot.Bounces);
            }
            float progress = Mathf.Clamp01((shot.Age - launch) / WaterBalloonGeometry.FlightTime);
            shot.Pos = WaterBalloonGeometry.Position(shot.Anchor, shot.SkillPoint, progress);
            shot.Vel = shot.SkillPoint - shot.Anchor + Vector2.up * (Mathf.Cos(progress * Mathf.PI) * Mathf.PI * 1.3f);
            if (progress < 1f) return true;
            // Landing applies one AOE hit, with no extra direct-hit damage and no per-frame DOT.
            shot.Pos = shot.SkillPoint;
            DamageCircle(shot.Pos, WaterBalloonGeometry.BurstRadius, shot.Damage, StatusMask.Slow, 1.3f, 0);
            SplitWater(shot);
            float remainingAge = Mathf.Max(0, shot.Age - shot.Duration);
            shot.Kind = ActorKind.WaterBalloonBurst;
            shot.Size = WaterBalloonGeometry.BurstRadius;
            shot.Age = remainingAge;
            shot.Duration = WaterBalloonGeometry.BurstTime;
            shot.Life = Mathf.Max(0, shot.Duration - remainingAge);
            shot.Dead = shot.Life <= 0;
            return true;
        }
    }
}
