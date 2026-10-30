using UnityEngine;

namespace OctopusSurvivors
{
    public static class SpecialAbilities
    {
        public static string Description(SpecialId id)
        {
            switch (id)
            {
                case SpecialId.Meteor: return "필드에 운석을 쏟아붓는다";
                case SpecialId.Hellfire: return "필드를 불태운다 (지속딜+둔화)";
                case SpecialId.Spirit: return "전방 충격파 (넉백+기절)";
                case SpecialId.DarkMatter: return "블랙홀로 적을 끌어당긴다";
                default: return string.Empty;
            }
        }

        public static void Cast(OctopusPlayer player, GameSession session, SpecialId id, int level)
        {
            float mul = 1f + 0.3f * (level - 1);
            Vector2 pos = player.Pos;
            Vector2 facing = player.Facing.Value.sqrMagnitude > 0.01f ? player.Facing.Value.normalized : Vector2.right;

            switch (id)
            {
                case SpecialId.Meteor:
                    session.QueueMeteors(player, 14 + 4 * level, 110f * mul);
                    break;

                case SpecialId.Hellfire:
                    for (int i = 0; i < 6; i++)
                    {
                        Vector2 spot = pos + Random.insideUnitCircle * (5f + level);
                        session.SpawnPersistent(ActorKind.HellfireZone, spot, Vector2.zero, 3.4f, 4f + 0.5f * level,
                            30f * mul, player, StatusMask.Burn | StatusMask.Slow);
                    }

                    break;

                case SpecialId.Spirit:
                    session.DamageCone(pos, facing, 9f + level, 150f, 160f * mul, StatusMask.Stun | StatusMask.Knockback, 1.6f, 0f);
                    session.SpawnTransient(ActorKind.SpiritWave, pos + facing * 3.5f, 9f, 0.4f);
                    break;

                case SpecialId.DarkMatter:
                    var hole = session.SpawnPersistent(ActorKind.BlackHole, pos + facing * 3.5f, Vector2.zero,
                        3.2f + 0.3f * level, 5f, 26f * mul, player, StatusMask.Pull);
                    hole.Bounces = level;
                    break;
            }
        }

        public static void CastPair(GameSession session, Vector2 center, SpecialId a, SpecialId b, int level)
        {
            if (a > b)
            {
                SpecialId swap = a;
                a = b;
                b = swap;
            }

            float teamMul = 1f + 0.35f * (level - 1);
            float specialMul = 1f + 0.3f * (level - 1);

            if (a == SpecialId.Meteor && b == SpecialId.Hellfire)
            {
                var meteor = session.SpawnTransient(ActorKind.Meteorite, center, 34f, 0.45f);
                meteor.Damage = 900f * teamMul;
                meteor.Bounces = -1;
            }
            else if (a == SpecialId.Meteor && b == SpecialId.Spirit)
            {
                float life = 4f + 0.5f * level;
                float damage = 160f * specialMul;
                for (int i = 0; i < GameConfig.TeamSpiritCount; i++)
                {
                    float deg = 360f * i / Mathf.Max(1, GameConfig.TeamSpiritCount);
                    Vector2 pos = center + AbilityRunner.Rotate(Vector2.right, deg) * (GameConfig.PlayerRadius * 3f);
                    session.SpawnAllySpirit(pos, damage, life);
                }
            }
            else if (a == SpecialId.Meteor && b == SpecialId.DarkMatter)
            {
                Vector2 aim = session.DensestPoint(center, GameConfig.MapSize);
                Vector2 dir = aim - center;
                if (dir.sqrMagnitude < 0.25f)
                    dir = Vector2.right;
                session.SpawnPiercingMeteor(center, dir.normalized, 110f * specialMul);
            }
            else if (a == SpecialId.Hellfire && b == SpecialId.Spirit)
            {
                session.SpawnPersistent(ActorKind.HellfireZone, center, Vector2.zero, 3.4f, 4f + 0.5f * level,
                    30f * specialMul, null, StatusMask.Burn | StatusMask.Slow);
            }
            else if (a == SpecialId.Hellfire && b == SpecialId.DarkMatter)
            {
                session.CastBlackFlame(center, 5f + level, GameConfig.BlackFlameSpread, 30f * specialMul, 1.6f, 14f);
            }
            else if (a == SpecialId.Spirit && b == SpecialId.DarkMatter)
            {
                var hole = session.SpawnPersistent(ActorKind.BlackHole, center, Vector2.zero,
                    3.2f + 0.3f * level, 5f, 26f * specialMul, null, StatusMask.Pull);
                hole.Bounces = -1;
            }
        }
    }
}
