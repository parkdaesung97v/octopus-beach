using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace OctopusSurvivors
{
    public static class SodaGeometry
    {
        public const float Speed = 10f;
        public static float BottleLength(float size) => Mathf.Max(.8f, size * 1.8f);
        public static float TrailRadius(float size) => Mathf.Max(.15f, size * .5f);
        public static Vector2 Mouth(Vector2 position, Vector2 direction, float size) =>
            position - direction.normalized * BottleLength(size) * .48f;
    }

    public sealed partial class GameSession
    {
        // Scoped to the actor; its complete deposited path dies with the cast, not with sample age.
        sealed class SodaTrail { public readonly List<Vector2> Points = new List<Vector2>(400); }
        readonly ConditionalWeakTable<SimActor, SodaTrail> sodaTrails = new ConditionalWeakTable<SimActor, SodaTrail>();

        void TickSoda(SimActor bottle, float dt)
        {
            var trail = sodaTrails.GetValue(bottle, _ => new SodaTrail());
            Vector2 direction = bottle.Vel.sqrMagnitude > .001f ? bottle.Vel.normalized : Vector2.right;
            if (trail.Points.Count == 0) trail.Points.Add(SodaGeometry.Mouth(bottle.Pos, direction, bottle.Size));
            // Swept movement, bounded speed and frame-independent DPS even after a delayed frame.
            float remaining = dt;
            while (remaining > .00001f)
            {
                float step = Mathf.Min(1f / 60f, remaining); remaining -= step;
                bottle.ContactCooldown -= step;
                if (bottle.ContactCooldown <= 0)
                {
                    bottle.ContactCooldown += Random.Range(.35f, .7f);
                    bottle.SkillPoint = AbilityRunner.Rotate(direction, Random.Range(-95f, 95f)).normalized;
                }
                if(bottle.SkillPoint.sqrMagnitude>.01f)
                    direction=((Vector2)Vector3.RotateTowards(direction,bottle.SkillPoint,270f*Mathf.Deg2Rad*step,0)).normalized;
                Vector2 next = bottle.Pos + direction * (SodaGeometry.Speed * step);
                if (!FiniteBeach.Walkable(next, bottle.Size))
                {
                    direction = AbilityRunner.Rotate(-direction, Random.Range(-35f,35f)).normalized;
                    bottle.SkillPoint = direction;
                    next = bottle.Pos + direction * (SodaGeometry.Speed * step);
                }
                if (FiniteBeach.Walkable(next, bottle.Size)) bottle.Pos = next;
                bottle.Vel = direction * SodaGeometry.Speed;
                Vector2 mouth = SodaGeometry.Mouth(bottle.Pos, direction, bottle.Size);
                if ((mouth-trail.Points[trail.Points.Count-1]).sqrMagnitude >= .0225f) trail.Points.Add(mouth);
                DamageSodaTrail(bottle, trail.Points, step);
            }
        }

        void DamageSodaTrail(SimActor bottle, List<Vector2> trail, float dt)
        {
            float radius = SodaGeometry.TrailRadius(bottle.Size);
            for (int i=0, count=_actors.Count; i<count; i++)
            {
                var enemy = _actors[i];
                if (enemy.Dead || (enemy.Kind != ActorKind.Enemy && enemy.Kind != ActorKind.Boss)) continue;
                float body = enemy.Giant != null ? enemy.Size * .4f : GameConfig.EnemyRadius;
                bool hit = (enemy.Pos-bottle.Pos).sqrMagnitude <= (bottle.Size*.45f+body)*(bottle.Size*.45f+body);
                Vector2 mouth = SodaGeometry.Mouth(bottle.Pos,bottle.Vel,bottle.Size);
                hit |= DistToSegment(enemy.Pos,mouth,bottle.Pos+bottle.Vel.normalized*bottle.Size*.86f) <= bottle.Size*.2f+body;
                hit |= DistToSegment(enemy.Pos,mouth-bottle.Vel.normalized*bottle.Size*1.7f,mouth) <= radius*.65f+body;
                for (int j=trail.Count-1; j>0 && !hit; j--)
                    hit = DistToSegment(enemy.Pos, trail[j-1], trail[j]) <= radius+body;
                // Preserve the previous 0.25-second pulse budget (four hits per second).
                // Head, intersecting old trails and fresh water form one union per bottle.
                if (hit) ApplyDamage(enemy, bottle.Damage * 4f * dt, StatusMask.None, 0, 0);
            }
        }
    }
}
