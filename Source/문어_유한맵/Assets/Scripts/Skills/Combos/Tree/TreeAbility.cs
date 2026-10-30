using UnityEngine;

namespace OctopusSurvivors
{
    public sealed partial class GameSession
    {
        void CastTreeAbility(OctopusPlayer player, int rank, int level, float damage, Vector2 direction)
        {
            var tuning = GameplayRules.Tuning(ComboId.Tree);
            var treeTuning = TreeAbilityTuning.Data;
            float fruitDamage = damage * (rank == 5
                ? treeTuning.masterFruitDamageMultiplier
                : treeTuning.fruitDamageMultiplier);

            for (int i = 0; i <= rank; i++)
            {
                Vector2 offset = i == 0
                    ? Vector2.zero
                    : AbilityRunner.Rotate(direction, i * 137.5f) * 2f;
                Effect(ActorKind.FruitTree,
                    GameConfig.ClampToMap(player.Pos + offset, .5f), Vector2.zero,
                    tuning.range, tuning.duration, fruitDamage, player, level);
            }
        }

        void TickTreeAbility(SimActor tree)
        {
            var tuning = TreeAbilityTuning.Data;
            float interval = Mathf.Max(.01f, tree.EffectLevel == 6
                ? tuning.masterFruitInterval
                : tuning.fruitInterval);

            // Indexed schedule keeps the same budget at 30/60/144 FPS and excludes a shot at expiry.
            while (tree.Mark * interval < tree.Duration - .0001f && tree.Mark * interval < tree.Age)
            {
                tree.Mark++;
                Vector2 landing = TreeLandingPoint(tree, tuning.fruitRadius);
                Effect(ActorKind.Fruit, landing, Vector2.zero, tuning.fruitRadius,
                    tuning.fruitFallSeconds, tree.Damage, tree.Owner, tree.EffectLevel);
            }
        }

        Vector2 TreeLandingPoint(SimActor tree, float fruitRadius)
        {
            // Reservoir sampling chooses a live target without allocating a list per fruit.
            SimActor target = null;
            int count = 0;
            float radius = Mathf.Max(0f, tree.Size - fruitRadius);
            foreach (var enemy in _actors)
            {
                if (enemy.Dead || (enemy.Kind != ActorKind.Enemy && enemy.Kind != ActorKind.Boss) ||
                    (enemy.Pos - tree.Pos).sqrMagnitude > radius * radius) continue;
                if (Random.Range(0, ++count) == 0) target = enemy;
            }

            return target != null
                ? target.Pos
                : GameConfig.ClampToMap(tree.Pos + Random.insideUnitCircle * radius, .5f);
        }
    }
}
