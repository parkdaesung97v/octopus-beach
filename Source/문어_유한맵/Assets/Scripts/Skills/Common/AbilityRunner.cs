using UnityEngine;

namespace OctopusSurvivors
{
    /// <summary>
    /// 속성은 주기마다 한 번씩 "발동"하고, 발동한 기술은 자기 지속시간 동안 단계적으로 진행된다.
    /// </summary>
    public static class AbilityRunner
    {
        public static void CastCombo(OctopusPlayer player, GameSession session, ComboId combo, int level)
        {
            session.CastElementEffect(player, combo, Mathf.Clamp(level, 1, 5));
        }

        public static Vector2 Rotate(Vector2 v, float degrees)
        {
            float r = degrees * Mathf.Deg2Rad;
            float c = Mathf.Cos(r);
            float s = Mathf.Sin(r);
            return new Vector2(c * v.x - s * v.y, s * v.x + c * v.y).normalized;
        }
    }

    [System.Flags]
    public enum StatusMask
    {
        None = 0,
        Burn = 1,
        Slow = 2,
        Stun = 4,
        Spicy = 8,
        Execute = 16,
        Knockback = 32,
        Pull = 64
    }
}
