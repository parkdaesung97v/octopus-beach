using UnityEngine;

namespace OctopusSurvivors
{
    public enum TreePalette { A_FreshGreen, B_WarmOlive, C_DeepGreen }

    [CreateAssetMenu(menuName = "Octopus/Tree ability tuning")]
    public sealed class TreeAbilityTuning : ScriptableObject
    {
        [Header("Visual size")]
        [Tooltip("나무 그림 크기. 실행 중 변경하면 즉시 반영됩니다.")]
        [Min(.1f)] public float treeVisualScale = 3f;
        [Tooltip("열매 그림 배율 = 나무 그림 배율 × 이 값. 피해 범위와 별개입니다.")]
        [Min(.1f)] public float fruitVisualScaleRatio = 3.75f;
        [Tooltip("나무 시안 A/B/C. 공격 수치에는 영향을 주지 않습니다.")]
        public TreePalette palette = TreePalette.A_FreshGreen;
        [Tooltip("열매가 나오는 잎 부분의 흔들림 크기(월드 단위). 줄기는 고정됩니다.")]
        [Range(0f, .15f)] public float leafShake = .055f;
        [Header("Fruit attack")]
        [Min(.01f)] public float fruitInterval = .0625f, masterFruitInterval = .05f;
        [Min(0)] public float fruitDamageMultiplier = .2f, masterFruitDamageMultiplier = .53333336f;
        [Min(.1f)] public float fruitRadius = .5f, fruitFallSeconds = .6f;

        static TreeAbilityTuning instance;
        public static TreeAbilityTuning Data => instance != null ? instance :
            (instance = Resources.Load<TreeAbilityTuning>("TreeAbilityTuning") ?? CreateInstance<TreeAbilityTuning>());

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset() => instance = null;
    }
}
