using UnityEngine;

namespace OctopusSurvivors
{
    public sealed partial class GameSession
    {
        void CastTreeRain(OctopusPlayer player, int rank, int level, float damage, Vector2 direction)
        {
            var tuning = GameplayRules.Tuning(ComboId.Tree);
            var treeTuning = TreeAbilityTuning.Data;
            float fruitDamage = damage * (rank == 5 ? treeTuning.masterFruitDamageMultiplier : treeTuning.fruitDamageMultiplier);
            for (int i = 0; i <= rank; i++)
            {
                Vector2 offset = i == 0 ? Vector2.zero : AbilityRunner.Rotate(direction, i * 137.5f) * 2f;
                var tree = Effect(ActorKind.FruitTree, GameConfig.ClampToMap(player.Pos + offset, .5f),
                    Vector2.zero, tuning.range, tuning.duration, fruitDamage, player, level);
                tree.SkillPoint = new Vector2(Random.value, Random.value);
            }
        }

        void TickTreeRain(SimActor tree)
        {
            var tuning = TreeAbilityTuning.Data;
            float interval = Mathf.Max(.01f, tree.EffectLevel == 6 ? tuning.masterFruitInterval : tuning.fruitInterval);
            while (tree.Mark * interval < tree.Duration - .0001f && tree.Mark * interval < tree.Age)
            {
                tree.Mark++;
                var fruit = Effect(ActorKind.Fruit, TreeRainLandingPoint(tree), Vector2.zero, tuning.fruitRadius,
                    tuning.fruitFallSeconds, tree.Damage, tree.Owner, tree.EffectLevel);
                fruit.SkillPhase = unchecked((byte)tree.Id);
            }
        }

        void LandTreeRain(SimActor fruit)
        {
            if (fruit.Life > 0f || fruit.Dead) return;
            DamageCircle(fruit.Pos, fruit.Size, fruit.Damage, StatusMask.None, 0f, 0f);
            fruit.Dead = true;
        }

        Vector2 TreeRainLandingPoint(SimActor tree) =>
            GameConfig.ClampToMap(TreeRainGeometry.LandingPoint(tree.Pos, tree.Size, tree.Mark, tree.SkillPoint), .5f);
    }
}
