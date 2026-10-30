using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace OctopusSurvivors
{
    // Initial throw aims at a selected enemy; this also supplies the no-target fallback.
    public static class FireOrbGeometry
    {
        public const float SampleStep = 1f / 60f;
        public static float Launch(int index) => index * UpgradeTuning.Data.fireOrbLaunchGap;
        public static float ReturnStart(float duration) => duration - UpgradeTuning.Data.fireOrbReturnTime;
        public static Vector2 Position(Vector2 origin, Vector2 direction, float reach, int index,
            float age, float duration, Vector2 owner)
        {
            var t = UpgradeTuning.Data;
            float launch = Launch(index), arrival = launch + t.fireOrbThrowTime;
            float back = ReturnStart(duration);
            Vector2 goal = origin + direction * reach;
            if (age <= launch) return origin;
            if (age < arrival) return Vector2.Lerp(origin, goal, Mathf.SmoothStep(0, 1, (age - launch) / t.fireOrbThrowTime));
            if (age < back) return Patrol(goal, direction, age - arrival);
            Vector2 start = Patrol(goal, direction, back - arrival);
            return Vector2.Lerp(start, owner, Mathf.SmoothStep(0, 1, (age - back) / t.fireOrbReturnTime));
        }
        static Vector2 Patrol(Vector2 goal, Vector2 direction, float age) => goal + direction *
            (Mathf.Sin(age * UpgradeTuning.Data.fireOrbPatrolSpeed) * UpgradeTuning.Data.fireOrbPatrolDistance);
        public static float TrailRadius(float radius, float sampleAge) => radius *
            Mathf.Lerp(.9f, .12f, Mathf.Clamp01(sampleAge / UpgradeTuning.Data.fireOrbTrailTime));
    }

    public sealed partial class GameSession
    {
        sealed class FireTrail
        {
            public struct Sample { public Vector2 Position; public float Age; }
            public readonly List<Sample> Samples = new List<Sample>(40);
            public float Age;
            public Vector2 Owner;
            public List<SimActor> Group;
            public readonly HashSet<ushort> Visited = new HashSet<ushort>();
            public float SwitchAt, ContactAt = -1, SearchAt;
            public bool Recalling;
            public Vector2 RecallFrom;
            public bool Launched;
            public float ThrowDistance;
        }
        readonly ConditionalWeakTable<SimActor, FireTrail> fireOrbTrails = new ConditionalWeakTable<SimActor, FireTrail>();

        void CastFireOrbs(OctopusPlayer player, Vector2 direction, int level, float multiplier)
        {
            var tuning = UpgradeTuning.Data;
            float reach = (4.3f + tuning.fireRangePerLevel * (level - 1)) * tuning.fireSizeMultiplier;
            var group = new List<SimActor>(3);
            for (int i = 0; i < 3; i++)
            {
                Vector2 heading = AbilityRunner.Rotate(direction, (i - 1) * tuning.fireOrbSpread);
                var orb = Effect(ActorKind.FlameAura, player.Pos, heading, tuning.fireOrbRadius,
                    tuning.fireOrbDuration, tuning.fireOrbDps * multiplier, player, level);
                orb.SkillPoint = heading; orb.SkillTime = reach; orb.Bounces = i;
                orb.Target = FindFireLaunchTarget(player.Pos, reach, group, null);
                group.Add(orb);
                fireOrbTrails.Add(orb, new FireTrail { Owner = player.Pos, Group = group });
            }
        }

        SimActor FindFireLaunchTarget(Vector2 owner, float reach, List<SimActor> group, SimActor self)
        {
            float range = reach * UpgradeTuning.Data.fireOrbTargetRangeMultiplier;
            SimActor chosen = null; int best = int.MaxValue; float nearest = float.PositiveInfinity;
            for (int i = 0; i < _actors.Count; i++)
            {
                var enemy = _actors[i];
                if (enemy.Dead || (enemy.Kind != ActorKind.Enemy && enemy.Kind != ActorKind.Boss)) continue;
                float distance = (enemy.Pos - owner).sqrMagnitude;
                if (distance > range * range) continue;
                int reservations = 0;
                if (group != null) foreach (var other in group)
                    if (other != self && !other.Dead && other.Target == enemy) reservations++;
                if (reservations > best || (reservations == best && distance >= nearest)) continue;
                chosen = enemy; best = reservations; nearest = distance;
            }
            return chosen;
        }

        void TickFireOrb(SimActor orb, float dt)
        {
            if (!fireOrbTrails.TryGetValue(orb, out var trail))
            { trail = new FireTrail { Owner = orb.Owner.Pos }; fireOrbTrails.Add(orb, trail); }
            float end = Mathf.Min(orb.Age, orb.Duration);
            float start = trail.Age;
            Vector2 currentOwner = orb.Owner.Pos;
            while (trail.Age < end - .000001f)
            {
                float step = Mathf.Min(FireOrbGeometry.SampleStep, end - trail.Age);
                float nextAge = trail.Age + step;
                Vector2 owner = Vector2.Lerp(trail.Owner, currentOwner, Mathf.InverseLerp(start, end, nextAge));
                Vector2 previous = orb.Pos;
                float launch = FireOrbGeometry.Launch(orb.Bounces);
                if (!trail.Launched && nextAge >= launch)
                {
                    trail.Launched = true; orb.Anchor = owner; orb.Pos = previous = owner;
                    float range = orb.SkillTime * UpgradeTuning.Data.fireOrbTargetRangeMultiplier;
                    if (orb.Target == null || orb.Target.Dead || (orb.Target.Pos - owner).sqrMagnitude > range * range)
                        orb.Target = FindFireLaunchTarget(owner, orb.SkillTime, trail.Group, orb);
                    trail.ThrowDistance = orb.SkillTime;
                    if (orb.Target != null)
                    {
                        Vector2 delta = orb.Target.Pos - owner;
                        if (delta.sqrMagnitude > .00001f) orb.SkillPoint = orb.Vel = delta.normalized;
                        trail.ThrowDistance = Mathf.Min(orb.SkillTime, delta.magnitude);
                        trail.Visited.Add(orb.Target.Id);
                    }
                    trail.SwitchAt = launch + UpgradeTuning.Data.fireOrbThrowTime + UpgradeTuning.Data.fireOrbRetargetInterval;
                }
                float arrival = FireOrbGeometry.Launch(orb.Bounces) + UpgradeTuning.Data.fireOrbThrowTime;
                float recall = FireOrbGeometry.ReturnStart(orb.Duration);
                if (nextAge <= arrival)
                    orb.Pos = FireOrbGeometry.Position(orb.Anchor, orb.SkillPoint,
                        trail.Launched ? trail.ThrowDistance : orb.SkillTime, orb.Bounces, nextAge, orb.Duration, owner);
                else if (nextAge >= recall)
                {
                    if (!trail.Recalling) { trail.Recalling = true; trail.RecallFrom = orb.Pos; orb.Target = null; }
                    orb.Pos = Vector2.Lerp(trail.RecallFrom, owner,
                        Mathf.SmoothStep(0, 1, (nextAge - recall) / UpgradeTuning.Data.fireOrbReturnTime));
                }
                else MoveFireOrbToTarget(orb, trail, nextAge, step, owner);
                if ((orb.Pos - previous).sqrMagnitude > .000001f) orb.Vel = (orb.Pos - previous).normalized;
                if (nextAge >= FireOrbGeometry.Launch(orb.Bounces))
                {
                    if (trail.Samples.Count == 0) trail.Samples.Add(new FireTrail.Sample { Position = previous, Age = trail.Age });
                    trail.Samples.Add(new FireTrail.Sample { Position = orb.Pos, Age = nextAge });
                    while (trail.Samples.Count > 1 && nextAge - trail.Samples[1].Age > UpgradeTuning.Data.fireOrbTrailTime)
                        trail.Samples.RemoveAt(0);
                    DamageFireTrail(orb, trail, nextAge, step);
                }
                trail.Age = nextAge;
            }
            trail.Owner = currentOwner;
        }

        void MoveFireOrbToTarget(SimActor orb, FireTrail trail, float age, float dt, Vector2 owner)
        {
            var tuning = UpgradeTuning.Data;
            float range = orb.SkillTime * tuning.fireOrbTargetRangeMultiplier;
            bool valid = orb.Target != null && !orb.Target.Dead && (orb.Target.Pos - owner).sqrMagnitude <= range * range;
            bool contactDone = trail.ContactAt >= 0 && age - trail.ContactAt >= tuning.fireOrbTargetHold;
            if ((!valid || age >= trail.SwitchAt || contactDone) && (!valid || age >= trail.SearchAt))
            {
                SimActor next = null; int bestPriority = int.MaxValue; float nearest = float.PositiveInfinity;
                for (int i = 0; i < _actors.Count; i++)
                {
                    var enemy = _actors[i];
                    if (enemy.Dead || (enemy.Kind != ActorKind.Enemy && enemy.Kind != ActorKind.Boss) ||
                        (enemy.Pos - owner).sqrMagnitude > range * range) continue;
                    bool reserved = false;
                    if (trail.Group != null) foreach (var other in trail.Group)
                        if (other != orb && !other.Dead && other.Target == enemy) { reserved = true; break; }
                    int priority = (reserved ? 4 : 0) + (trail.Visited.Contains(enemy.Id) ? 2 : 0) + (orb.Target == enemy ? 8 : 0);
                    float distance = (enemy.Pos - orb.Pos).sqrMagnitude;
                    if (priority > bestPriority || (priority == bestPriority && distance >= nearest)) continue;
                    next = enemy; bestPriority = priority; nearest = distance;
                }
                orb.Target = next; trail.ContactAt = -1; trail.SearchAt = age + .05f;
                if (next != null)
                {
                    // A completed round may revisit enemies; the previous target remains last choice.
                    if (trail.Visited.Contains(next.Id)) trail.Visited.Clear();
                    trail.Visited.Add(next.Id); orb.Mark++;
                    trail.SwitchAt = age + Mathf.Max(tuning.fireOrbRetargetInterval,
                        Mathf.Sqrt(nearest) / tuning.fireOrbSeekSpeed + tuning.fireOrbTargetHold);
                }
            }
            if (orb.Target != null && !orb.Target.Dead)
            {
                orb.Pos = Vector2.MoveTowards(orb.Pos, orb.Target.Pos, tuning.fireOrbSeekSpeed * dt);
                if (trail.ContactAt < 0 && Vector2.Distance(orb.Pos, orb.Target.Pos) <= orb.Size * .7f)
                    trail.ContactAt = age;
            }
            else orb.Pos = Vector2.MoveTowards(orb.Pos,
                FireOrbGeometry.Position(orb.Anchor, orb.SkillPoint, orb.SkillTime, orb.Bounces, age, orb.Duration, owner),
                tuning.fireOrbSeekSpeed * dt);
        }

        void DamageFireTrail(SimActor orb, FireTrail trail, float age, float dt)
        {
            // Union of swept head and tapered trail: each enemy receives DPS once per substep.
            int count = _actors.Count;
            for (int index = 0; index < count; index++)
            {
                var enemy = _actors[index];
                if (enemy.Dead || (enemy.Kind != ActorKind.Enemy && enemy.Kind != ActorKind.Boss)) continue;
                float body = enemy.Giant != null ? enemy.Size * .4f : GameConfig.EnemyRadius;
                bool hit = (enemy.Pos - orb.Pos).sqrMagnitude <= (orb.Size + body) * (orb.Size + body);
                // The sprite itself includes a flame tongue behind its head, even on reversals.
                hit |= DistToSegment(enemy.Pos, orb.Pos - orb.Vel * (orb.Size * 2.3f), orb.Pos) <= orb.Size * .45f + body;
                for (int j = trail.Samples.Count - 1; j > 0 && !hit; j--)
                {
                    var a = trail.Samples[j - 1]; var b = trail.Samples[j];
                    if (age - b.Age > UpgradeTuning.Data.fireOrbTrailTime) break;
                    float radius = FireOrbGeometry.TrailRadius(orb.Size, age - b.Age);
                    hit = DistToSegment(enemy.Pos, a.Position, b.Position) <= radius + body;
                }
                if (hit) ApplyDamage(enemy, orb.Damage * dt, StatusMask.Burn, .5f,
                    UpgradeTuning.Data.fireOrbBurnDps * (1f + UpgradeTuning.Data.attributeDamagePerLevel * orb.Owner.UpAttrRank.Value));
            }
        }
    }
}
