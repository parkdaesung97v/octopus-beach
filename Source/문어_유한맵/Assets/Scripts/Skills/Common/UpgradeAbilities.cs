using UnityEngine;

namespace OctopusSurvivors
{
    public sealed partial class GameSession
    {
        void CastHiddenAbilities(OctopusPlayer p, ElementId element, Vector2 dir, float multiplier)
        {
            var t = UpgradeTuning.Data;
            if (element == ElementId.Water && p.HasUpgrade(UpgradeId.WaterBalloon))
                Effect(ActorKind.Balloon, p.Pos, dir * t.balloonSpeed, .8f, t.hiddenProjectileLife, t.balloonDamage * multiplier, p, 1);
            if (element == ElementId.Fire)
            {
                dir = DensestFireDirection(p.Pos, dir, 8f);
                if (p.HasUpgrade(UpgradeId.FireTornado))
                    Effect(ActorKind.Tornado, p.Pos + dir, dir * t.tornadoSpeed, t.tornadoRadius, t.tornadoDuration + 3f, t.tornadoDps * multiplier, p, 1);
                if (p.HasUpgrade(UpgradeId.Fireworks))
                {
                    var target = NearestEnemy(p.Pos, 15f);
                    Vector2 point = target != null ? target.Pos : p.Pos + dir * 8f;
                    var shot = Effect(ActorKind.Firework, p.Pos, (point - p.Pos).normalized * t.fireworkSpeed, .3f, t.hiddenProjectileLife, t.fireworkDamage * multiplier, p, 1);
                    shot.Anchor = point;
                }
            }
            if (element == ElementId.Electric && p.HasUpgrade(UpgradeId.SkyLightning))
            {
                var target = NearestEnemy(p.Pos, 14f);
                if (target != null) Effect(ActorKind.LightningWarning, target.Pos, Vector2.zero, t.lightningRadius, t.lightningWarning, t.lightningDamage * multiplier, p, 1);
            }
        }

        void StrikeUpgradedVines(SimActor source)
        {
            var targets = TempEnemies(source.Pos, source.Size);
            targets.Sort((a, b) => (a.Pos - source.Pos).sqrMagnitude.CompareTo((b.Pos - source.Pos).sqrMagnitude));
            for (int i = 0; i < Mathf.Min(source.EffectLevel, targets.Count); i++)
            {
                var target = targets[i];
                Vector2 point = target.Pos;
                GrassDamage(target, source.Damage, source.Owner, true);
                SpawnTransient(ActorKind.Vine, point, .9f, .18f);
                if (source.Owner.HasUpgrade(UpgradeId.LingeringThorns))
                {
                    // Refresh the same caster's patch at this target instead of stacking four DOT zones per cast.
                    SimActor patch = null;
                    foreach (var a in _actors)
                        if (!a.Dead && a.Kind == ActorKind.ThornPatch && a.Owner == source.Owner && (a.Pos - point).sqrMagnitude < .5f)
                        { patch = a; break; }
                    if (patch != null) patch.Life = UpgradeTuning.Data.thornDuration;
                    else Effect(ActorKind.ThornPatch, point, Vector2.zero, UpgradeTuning.Data.thornRadius, UpgradeTuning.Data.thornDuration,
                        source.Damage * UpgradeTuning.Data.thornDamageMultiplier, source.Owner, 1);
                }
            }
        }

        void GrassDamage(SimActor target, float damage, OctopusPlayer owner, bool allowBurst)
        {
            if (target.Dead) return;
            Vector2 point = target.Pos;
            if (!ApplyDamage(target, damage, StatusMask.None, 0, 0) || !allowBurst || owner == null || !owner.HasUpgrade(UpgradeId.ThornBurst)) return;
            // Secondary thorns deliberately bypass GrassDamage: a kill can never recurse.
            for (int i = 0; i < 8; i++)
                Effect(ActorKind.ThornShot, point, AbilityRunner.Rotate(Vector2.right, i * 45f) * UpgradeTuning.Data.thornSpeed, .2f, UpgradeTuning.Data.thornProjectileLife, damage, owner, 1);
        }

        void SplitWater(SimActor water)
        {
            if (water.Mark != 0 || water.Owner == null || !water.Owner.HasUpgrade(UpgradeId.WaterSplit)) return;
            water.Mark = 1;
            for (int i = 0; i < 4; i++)
            {
                var child = SpawnProjectile(ActorKind.Droplet, water.Pos,
                    AbilityRunner.Rotate(Vector2.right, i * 90f + 45f) * 7f, .16f, .7f,
                    water.Damage * UpgradeTuning.Data.splitDamageMultiplier, water.Owner, StatusMask.Slow);
                child.Mark = 1;
            }
        }

        bool TickUpgradeAbility(SimActor a, float dt)
        {
            if (a.Kind < ActorKind.ThornPatch || a.Kind > ActorKind.LemonWheel) return false;
            if (a.Owner == null || !a.Owner.IsSpawned || a.Owner.Downed.Value) { a.Dead = true; return true; }
            a.Age += dt; a.Life -= dt; a.Pulse -= dt;
            var t = UpgradeTuning.Data;
            switch (a.Kind)
            {
                case ActorKind.ThornPatch:
                    if (a.Pulse <= 0)
                    {
                        a.Pulse = t.thornTick;
                        foreach (var enemy in TempEnemies(a.Pos, a.Size)) GrassDamage(enemy, a.Damage, a.Owner, true);
                    }
                    break;
                case ActorKind.ThornShot:
                    Vector2 before = a.Pos;
                    a.Pos = GameConfig.ClampToMap(a.Pos + a.Vel * dt, .1f);
                    if (HitProjectileSegment(a, before)) a.Dead = true;
                    break;
                case ActorKind.Balloon:
                case ActorKind.Firework:
                    if (a.Kind == ActorKind.Firework && a.Mark != 0)
                    {
                        Vector2 start = a.Pos;
                        a.Pos = GameConfig.ClampToMap(a.Pos + a.Vel * dt, .1f);
                        if (a.Mark == 2 && HitProjectileSegment(a, start)) a.Dead = true;
                        break;
                    }
                    Vector2 previous = a.Pos;
                    a.Pos = GameConfig.ClampToMap(a.Pos + a.Vel * dt, .1f);
                    if (HitProjectileSegment(a, previous) || a.Life <= 0 ||
                        (a.Kind == ActorKind.Firework && DistToSegment(a.Anchor, previous, a.Pos) < .3f))
                    {
                        float radius = a.Kind == ActorKind.Balloon ? t.balloonRadius : t.fireworkRadius;
                        DamageCircle(a.Pos, radius, a.Damage, StatusMask.None, 0, 0);
                        SpawnTransient(a.Kind == ActorKind.Balloon ? ActorKind.WaterImpact : ActorKind.Explosion, a.Pos, radius * 2f, .3f);
                        if (a.Kind == ActorKind.Firework)
                            for (int i = 0; i < 8; i++)
                            {
                                var spark = Effect(ActorKind.Firework, a.Pos, AbilityRunner.Rotate(Vector2.right, i * 45f) * 5f, .18f, .25f, 0, a.Owner, 1);
                                spark.Mark = 1;
                            }
                        a.Dead = true;
                    }
                    break;
                case ActorKind.Tornado:
                    if (a.Age >= t.tornadoDuration) a.Pos = GameConfig.ClampToMap(a.Pos + a.Vel * Mathf.Min(dt, Mathf.Max(0, a.Age - t.tornadoDuration)), a.Size);
                    if (a.Pulse <= 0)
                    {
                        a.Pulse = t.hiddenTickInterval;
                        DamageCircle(a.Pos, a.Size, a.Damage * t.hiddenTickInterval, StatusMask.None, 0, 0);
                        for (int i = 0; i < 4; i++)
                        {
                            var flame = Effect(ActorKind.Firework, a.Pos, AbilityRunner.Rotate(Vector2.right, a.Age * 180 + i * 90) * 5f,
                                .2f, .35f, a.Damage * .1f, a.Owner, 1);
                            flame.Mark = 2;
                        }
                    }
                    break;
                case ActorKind.LightningWarning:
                    if (a.Life <= 0)
                    {
                        DamageCircle(a.Pos, a.Size, a.Damage, a.Owner.HasUpgrade(UpgradeId.Paralysis) ? StatusMask.Stun : StatusMask.None, t.paralysisDuration, 0);
                        SpawnTransient(ActorKind.LightningBolt, a.Pos, a.Size * 2f, .25f);
                    }
                    break;
                case ActorKind.ComboRepeater:
                    if (a.Pulse <= 0 && a.Bounces > 0)
                    {
                        a.Pulse = t.repeatInterval; a.Bounces--;
                        FireComboVolley(a.Owner, (ComboId)a.Mark, a.EffectLevel, a.Damage, a.Vel);
                    }
                    if (a.Bounces == 0) a.Dead = true;
                    break;
                case ActorKind.ChiliField:
                    if (a.Pulse <= 0)
                    {
                        a.Pulse = t.chiliFieldInterval;
                        var target = NearestEnemy(a.Pos, 15f);
                        if (target != null) Effect(ActorKind.ChiliShot, a.Pos, (target.Pos - a.Pos).normalized * 8f, .2f, 2f, a.Damage, a.Owner, a.EffectLevel);
                    }
                    break;
                case ActorKind.LemonWheel:
                    Vector2 next = a.Pos + a.Vel * dt;
                    if (!FiniteBeach.Walkable(next, a.Size)) a.Vel = AbilityRunner.Rotate(-a.Vel, Random.Range(-45f, 45f)) * 6f;
                    else a.Pos = next;
                    if (a.Pulse <= 0)
                    {
                        a.Pulse = t.lemonWheelTick;
                        DamageCircle(a.Pos, a.Size, a.Damage, StatusMask.None, 0, 0);
                    }
                    break;
            }
            if (a.Life <= 0) a.Dead = true;
            return true;
        }
    }
}
