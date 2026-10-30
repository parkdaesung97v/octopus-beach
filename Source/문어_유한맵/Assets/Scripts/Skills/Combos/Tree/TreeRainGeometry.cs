using UnityEngine;

namespace OctopusSurvivors
{
    // Shared server/render geometry, measured from the user's marked tree preview.
    public static class TreeRainGeometry
    {
        public static float Width(float radius) => radius * TreeAbilityTuning.Data.treeVisualScale * .6f;
        public static float Height(float radius) => Width(radius) * .74f;
        public static float FruitSize => TreeAbilityTuning.Data.treeVisualScale * TreeAbilityTuning.Data.fruitVisualScaleRatio * .045f;
        public static float HalfLandingWidth(float radius) => Mathf.Max(0, Width(radius) * .4f -
            Mathf.Max(TreeAbilityTuning.Data.fruitRadius, FruitSize * .6f));

        public static Vector2 LandingPoint(Vector2 root, float radius, int index, Vector2 phase)
        {
            float across = Mathf.Repeat(Mathf.Max(1, index) * .618033989f + phase.x, 1f);
            float depth = Mathf.Repeat(Mathf.Max(1, index) * .754877666f + phase.y, 1f);
            return root + new Vector2(Mathf.Lerp(-HalfLandingWidth(radius), HalfLandingWidth(radius), across),
                Height(radius) * (-.035f - depth * .012f));
        }

        public static Vector3 ReleasePoint(Vector2 root, float radius, Vector2 landing)
        {
            float u = (landing.x - root.x) / Mathf.Max(.01f, Width(radius)) + .5f;
            float edge = Mathf.Clamp01(Mathf.Abs(u - .5f) * 2f);
            float foliageEdge = .52f - edge * .11f;
            // The apple's upper edge starts on the marked lower foliage contour.
            return new Vector3(landing.x, root.y + (foliageEdge - .06f) * Height(radius) - FruitSize * .5f, 0);
        }
    }
}
