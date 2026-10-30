using UnityEngine;

namespace OctopusSurvivors
{
    // 서버만 판정한다. 기존 CastCombo에서 한 번 호출하며 개별 쿨타임를 변경하지 않는다.
    public sealed partial class GameSession
    {
        SimActor Effect(ActorKind kind, Vector2 pos, Vector2 direction, float radius,
            float duration, float damage, OctopusPlayer owner, int level)
        {
            var a = SpawnPersistent(kind, pos, direction, radius, duration, damage, owner, StatusMask.None);
            a.Duration = duration;
            a.EffectLevel = level;
            a.Anchor = pos;
            return a;
        }

        public void CastElementEffect(OctopusPlayer p, ComboId combo, int level)
        {
            if (!IsServer || p == null || p.Downed.Value) return;
            if (combo == ComboId.Fog || combo == ComboId.Chili || combo == ComboId.Tree || combo >= ComboId.Soda) { CastPlannedCombo(p, combo); return; }
            int l = Mathf.Clamp(level, 1, 5);
            float m = 1f + 0.3f * (l - 1);
            Vector2 dir = p.Facing.Value.sqrMagnitude > 0.01f ? p.Facing.Value.normalized : Vector2.right;
            switch (combo)
            {
                case ComboId.Fog:
                    Effect(ActorKind.SteamField, p.Pos, Vector2.zero, 3.8f + 0.45f * (l - 1), 4f, 14f * m, p, l);
                    break;
                case ComboId.Chili:
                    int count = 8 + 2 * (l - 1);
                    for (int i = 0; i < count; i++)
                    {
                        Vector2 d = AbilityRunner.Rotate(dir, i * 360f / count);
                        Effect(ActorKind.ChiliShot, p.Pos + d * 0.3f, d * 8f, 0.28f, 1.2f, 12f * m, p, l);
                    }
                    break;
                case ComboId.Volcano:
                    Effect(ActorKind.Volcano, p.Pos + dir * 1.5f, dir, 1f, 4f, 40f * m, p, l);
                    break;
                case ComboId.Seaweed:
                    Effect(ActorKind.SeaweedPlant, p.Pos + dir * 1.3f, dir, 1.2f, 4f, 24f * m, p, l);
                    break;
                case ComboId.MossStone:
                    for (int i = 0; i < 5; i++)
                    {
                        var stone = Effect(ActorKind.MossShot, p.Pos + AbilityRunner.Rotate(dir, i * 72f) * 0.6f,
                            dir * (10f + 2f * (l - 1)), 0.3f, 16f, 16f * m, p, l);
                        stone.Bounces = 20 + 5 * (l - 1);
                        stone.Target = ChooseEffectTarget(stone.Pos, null, i);
                    }
                    break;
                case ComboId.Tree:
                    Effect(ActorKind.SeedEmitter, p.Pos, dir, 0.1f, 3.5f, 45f * m, p, l);
                    break;
                case ComboId.Lightning: // ID 7을 유지하고 효과만 전자레인지로 교체.
                    Effect(ActorKind.MicrowaveCharge, p.Pos, dir, 4f + 0.5f * (l - 1),
                        1.5f - 0.12f * (l - 1), 110f * m, p, l);
                    break;
                case ComboId.Mower:
                    Effect(ActorKind.MowerBlade, p.Pos + dir * 2f, dir, 1.2f + 0.25f * (l - 1), 3.5f, 14f * m, p, l);
                    break;
                case ComboId.Jellyfish:
                    var jelly = Effect(ActorKind.Jellyfish, p.Pos, dir, 4.5f + 0.5f * (l - 1),
                        (2 + l) * 0.6f, 25f * m, p, l);
                    jelly.Bounces = 2 + l;
                    break;
                case ComboId.Magnet:
                    Effect(ActorKind.MagnetPair, p.Pos + dir * 2f, dir, 3.2f + 0.4f * (l - 1),
                        3f + 0.4f * (l - 1), 18f * m, p, l);
                    break;
            }
        }

        public void CastDirectionalCurrent(Vector2 pos, Vector2 dir, float radius, float damage, OctopusPlayer owner)
        {
            foreach (var e in TempEnemies(pos, radius))
            {
                if (Vector2.Angle(dir, e.Pos - pos) > 65f) continue;
                ApplyDamage(e, damage, Random.value < 0.2f ? StatusMask.Stun : StatusMask.None, 0.7f, 0f);
            }
            Effect(ActorKind.CurrentFan, pos, dir, radius, 0.25f, 0f, owner, 1);
        }

        public bool InSteamField(Vector2 pos)
        {
            for (int i = 0; i < _actors.Count; i++)
            {
                var a = _actors[i];
                if (!a.Dead && a.Kind == ActorKind.SteamField && a.Life > 0f &&
                    (pos - a.Pos).sqrMagnitude <= a.Size * a.Size) return true;
            }
            return false;
        }

        public bool HasSteamField(OctopusPlayer p)
        {
            return HasEffect(p, ActorKind.SteamField);
        }

        public bool IsMicrowaveCharging(OctopusPlayer p)
        {
            return HasEffect(p, ActorKind.MicrowaveCharge);
        }

        bool HasEffect(OctopusPlayer p, ActorKind kind)
        {
            for (int i = 0; i < _actors.Count; i++)
            {
                var a = _actors[i];
                if (!a.Dead && a.Life > 0f && a.Owner == p && a.Kind == kind) return true;
            }
            return false;
        }

        bool TickElementEffect(SimActor a, float dt)
        {
            if (a.Kind < ActorKind.SteamField || a.Kind > ActorKind.MagnetBlast) return false;
            // Lost/downed owners cannot keep channelled attacks or hidden emitters alive.
            if (a.Kind == ActorKind.MicrowaveCharge || a.Kind == ActorKind.MowerBlade ||
                a.Kind == ActorKind.Jellyfish || a.Kind == ActorKind.SeedEmitter)
            {
                if (a.Owner == null || !a.Owner.IsSpawned || a.Owner.Downed.Value)
                { a.Dead = true; return true; }
            }
            a.Age += dt;
            a.Life -= dt;
            a.Pulse -= dt;
            int l = a.EffectLevel;
            switch (a.Kind)
            {
                case ActorKind.SteamField:
                    DamageCircle(a.Pos, a.Size, a.Damage * dt, StatusMask.Slow, .3f, 0f);
                    break;
                case ActorKind.ChiliShot:
                    StepEffectProjectile(a, dt, false);
                    break;
                case ActorKind.MossShot:
                case ActorKind.HomingFish:
                    StepEffectProjectile(a, dt, true);
                    break;
                case ActorKind.Volcano:
                    if (a.Pulse <= 0f && a.Life > 0f)
                    {
                        a.Pulse = 0.7f;
                        for (int i = 0; i < l; i++)
                        {
                            Vector2 spot = GameConfig.ClampToMap(a.Pos + Random.insideUnitCircle * 5.5f, 0.5f);
                            Effect(ActorKind.LavaWarning, spot, Vector2.zero, 1.2f, 0.8f, a.Damage, a.Owner, l);
                        }
                    }
                    break;
                case ActorKind.LavaWarning:
                    if (a.Life <= 0f)
                    {
                        DamageCircle(a.Pos, a.Size, a.Damage, StatusMask.None, 0f, 0f);
                        Effect(ActorKind.LavaImpact, a.Pos, Vector2.zero, a.Size, 0.45f, 0f, a.Owner, l);
                        Effect(ActorKind.LavaPool, a.Pos, Vector2.zero, a.Size, 2.5f, a.Damage * 0.25f, a.Owner, l);
                    }
                    break;
                case ActorKind.LavaPool:
                    DamageCircle(a.Pos, a.Size, a.Damage * dt, StatusMask.None, 0f, 0f);
                    break;
                case ActorKind.SeaweedPlant:
                    if (a.Pulse <= 0f && a.Life > 0f)
                    {
                        a.Pulse = 0.8f;
                        for (int i = 0; i < l; i++)
                        {
                            var fish = Effect(ActorKind.HomingFish, a.Pos, Vector2.right * 11f, 0.25f, 3f, a.Damage, a.Owner, l);
                            fish.Target = ChooseEffectTarget(a.Pos, null, i);
                        }
                    }
                    break;
                case ActorKind.SeedEmitter:
                    a.Pos = a.Owner.Pos;
                    if (a.Pulse <= 0f && a.Life > 0f)
                    {
                        a.Pulse = 0.5f;
                        for (int i = 0; i < 1 + l; i++)
                        {
                            Vector2 spot = GameConfig.ClampToMap(a.Pos + Random.insideUnitCircle * 4.5f, 0.5f);
                            Effect(ActorKind.Seed, spot, Vector2.zero, 0.22f, 0.85f, a.Damage, a.Owner, l);
                        }
                    }
                    break;
                case ActorKind.Seed:
                    if (a.Life <= 0f && Random.value < 0.25f + 0.1f * (l - 1))
                    {
                        float radius = 1.6f;
                        foreach (var e in TempEnemies(a.Pos, radius))
                        {
                            e.Knockback = (e.Pos - a.Pos).sqrMagnitude > 0.001f ? (e.Pos - a.Pos).normalized * 12f : Vector2.up * 12f;
                            ApplyDamage(e, a.Damage, StatusMask.None, 0f, 0f);
                        }
                        Effect(ActorKind.GrownTree, a.Pos, Vector2.zero, radius, 2.5f, 0f, a.Owner, l);
                    }
                    break;
                case ActorKind.MicrowaveCharge:
                    a.Pos = a.Owner.Pos;
                    if (a.Life <= 0f)
                    {
                        DamageCircle(a.Pos, a.Size, a.Damage, StatusMask.None, 0f, 0f);
                        Effect(ActorKind.MicrowaveBlast, a.Pos, Vector2.zero, a.Size, 0.4f, 0f, a.Owner, l);
                    }
                    break;
                case ActorKind.MowerBlade:
                    a.Vel = a.Owner.Facing.Value.normalized;
                    a.Pos = a.Owner.Pos + a.Vel * (a.Size + 0.65f);
                    if (a.Pulse <= 0f)
                    {
                        a.Pulse = 0.2f;
                        foreach (var e in TempEnemies(a.Pos, a.Size))
                        {
                            ApplyDamage(e, a.Damage, StatusMask.None, 0f, 0f);
                            e.Knockback = a.Vel * 4f;
                            var next = ClosestOther(e.Pos, e, 2.5f);
                            if (next != null)
                            {
                                ApplyDamage(next, a.Damage * 0.45f, StatusMask.None, 0f, 0f);
                                var arc = Effect(ActorKind.CurrentFan, e.Pos, (next.Pos - e.Pos).normalized,
                                    Vector2.Distance(e.Pos, next.Pos), 0.16f, 0f, a.Owner, l);
                                arc.Fallen = true;
                            }
                        }
                    }
                    break;
                case ActorKind.Jellyfish:
                    a.Pos = a.Owner.Pos;
                    a.Vel = a.Owner.Facing.Value.normalized;
                    if (a.Pulse <= 0f && a.Bounces > 0)
                    {
                        a.Pulse = 0.6f;
                        a.Bounces--;
                        foreach (var e in TempEnemies(a.Pos, a.Size))
                        {
                            if (Vector2.Angle(a.Vel, e.Pos - a.Pos) > 65f) continue;
                            bool stun = e.ShockTime > 0f && Random.value < 0.3f;
                            e.ShockTime = 2f;
                            ApplyDamage(e, a.Damage, StatusMask.Slow, 1.5f, 0f);
                            if (stun) e.StunTime = Mathf.Max(e.StunTime, 0.65f);
                        }
                        Effect(ActorKind.CurrentFan, a.Pos, a.Vel, a.Size, 0.3f, 0f, a.Owner, l);
                    }
                    break;
                case ActorKind.MagnetPair:
                    Vector2 axis = AbilityRunner.Rotate(a.Vel, 90f);
                    foreach (var e in TempEnemies(a.Pos, a.Size))
                    {
                        Vector2 rel = e.Pos - a.Pos;
                        // Pull towards the strip between poles, then towards its midpoint.
                        Vector2 strip = a.Pos + axis * Mathf.Clamp(Vector2.Dot(rel, axis), -a.Size * 0.55f, a.Size * 0.55f);
                        Vector2 destination = Vector2.Distance(e.Pos, strip) > 0.3f ? strip : a.Pos;
                        e.Pos = Vector2.MoveTowards(e.Pos, destination, 6f * dt);
                        if (Vector2.Distance(e.Pos, strip) < 0.4f)
                        {
                            e.RootTime = 0.12f;
                            ApplyDamage(e, a.Damage * dt, StatusMask.None, 0f, 0f);
                            if (a.Life <= 0f) ApplyDamage(e, a.Damage * 4f, StatusMask.None, 0f, 0f);
                        }
                    }
                    if (a.Life <= 0f) Effect(ActorKind.MagnetBlast, a.Pos, a.Vel, a.Size, 0.4f, 0f, a.Owner, l);
                    break;
            }
            if (a.Life <= 0f) a.Dead = true;
            return true;
        }

        SimActor ChooseEffectTarget(Vector2 pos, SimActor except, int offset)
        {
            var list = TempEnemies(pos, 18f);
            list.Remove(except);
            list.Sort((a, b) => (a.Pos - pos).sqrMagnitude.CompareTo((b.Pos - pos).sqrMagnitude));
            return list.Count == 0 ? null : list[Mathf.Abs(offset) % list.Count];
        }

        SimActor ClosestOther(Vector2 pos, SimActor except, float range)
        {
            SimActor best = null;
            float distance = range * range;
            for (int i = 0; i < _actors.Count; i++)
            {
                var e = _actors[i];
                if (e == except || e.Dead || (e.Kind != ActorKind.Enemy && e.Kind != ActorKind.Boss)) continue;
                float d = (e.Pos - pos).sqrMagnitude;
                if (d < distance) { best = e; distance = d; }
            }
            return best;
        }

        void StepEffectProjectile(SimActor a, float dt, bool homing)
        {
            float speed = a.Kind == ActorKind.MossShot ? 10f + 2f * (a.EffectLevel - 1) : 11f;
            if (homing)
            {
                if (a.Target == null || a.Target.Dead || a.Target == a.LastHit)
                    a.Target = ClosestOther(a.Pos, a.LastHit, 18f);
                if (a.Target != null) a.Vel = (a.Target.Pos - a.Pos).normalized * speed;
                else { a.Vel = Vector2.zero; return; }
            }
            Vector2 start = a.Pos;
            Vector2 end = start + a.Vel * dt;
            SimActor hit = null;
            float nearest = float.MaxValue;
            // Swept segment prevents fast shots tunnelling through enemies.
            int count = _actors.Count;
            for (int i = 0; i < count; i++)
            {
                var e = _actors[i];
                if (e.Dead || e == a.LastHit || (e.Kind != ActorKind.Enemy && e.Kind != ActorKind.Boss)) continue;
                float radius = a.Size + (e.Kind == ActorKind.Boss ? e.Size * 0.5f : GameConfig.EnemyRadius);
                if (DistToSegment(e.Pos, start, end) > radius) continue;
                float d = (e.Pos - start).sqrMagnitude;
                if (d < nearest) { hit = e; nearest = d; }
            }
            a.Pos = GameConfig.ClampToMap(end, 0.1f);
            if (hit == null) return;
            a.Pos = hit.Pos;
            ApplyDamage(hit, a.Damage, StatusMask.None, 0f, 0f);
            if (a.Kind == ActorKind.ChiliShot) ApplySpicy(hit, 1, 1f + UpgradeTuning.Data.elementDamagePerLevel * a.Owner.UpElemRank.Value);
            if (a.Kind != ActorKind.MossShot) { a.Dead = true; return; }
            a.LastHit = hit;
            a.Target = null;
            if (--a.Bounces <= 0) a.Dead = true;
        }

        void ApplySpicy(SimActor enemy, int level, float multiplier = 1f)
        {
            if (enemy.Dead) return;
            enemy.SpicyTime = Mathf.Max(enemy.SpicyTime, 2f + 0.4f * (level - 1));
            enemy.SpicyDps = Mathf.Max(enemy.SpicyDps, (10f + 4f * (level - 1)) * multiplier);
            enemy.SpicyDir = Random.insideUnitCircle.normalized;
        }
    }
}
