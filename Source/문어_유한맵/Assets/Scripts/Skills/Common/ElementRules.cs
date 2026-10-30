using UnityEngine;

namespace OctopusSurvivors
{
    public static class ElementRules
    {
        public static readonly Color Fire = new Color(1f, 0.38f, 0.12f);
        public static readonly Color Water = new Color(0.25f, 0.55f, 1f);
        public static readonly Color Grass = new Color(0.28f, 0.82f, 0.32f);
        public static readonly Color Stone = new Color(0.62f, 0.55f, 0.42f);
        public static readonly Color Electric = new Color(1f, 0.92f, 0.2f);

        public static Color Of(ElementId id)
        {
            switch (id)
            {
                case ElementId.Fire: return Fire;
                case ElementId.Water: return Water;
                case ElementId.Grass: return Grass;
                case ElementId.Stone: return Stone;
                case ElementId.Electric: return Electric;
                default: return Color.white;
            }
        }

        public static Color Of(ComboId id)
        {
            switch (id)
            {
                case ComboId.Fog: return new Color(1f, 1f, 1f, 0.65f);
                case ComboId.Chili: return new Color(1f, 0.2f, 0.15f);
                case ComboId.Volcano: return new Color(0.85f, 0.25f, 0.05f);
                case ComboId.Tree: return new Color(0.35f, 0.55f, 0.18f);
                case ComboId.MossStone: return new Color(0.4f, 0.7f, 0.45f);
                case ComboId.Seaweed: return new Color(0.15f, 0.45f, 0.55f);
                case ComboId.Lightning: return new Color(1f, 0.55f, 0.2f);
                case ComboId.Mower: return new Color(0.2f, 0.9f, 0.65f);
                case ComboId.Jellyfish: return new Color(0.2f, 0.75f, 1f);
                case ComboId.Magnet: return new Color(0.7f, 0.3f, 0.8f);
                default: return Color.white;
            }
        }

        public static Color Of(SpecialId id)
        {
            switch (id)
            {
                case SpecialId.Meteor: return new Color(0.85f, 0.5f, 0.25f);
                case SpecialId.Hellfire: return new Color(1f, 0.25f, 0.05f);
                case SpecialId.Spirit: return new Color(0.6f, 1f, 0.9f);
                case SpecialId.DarkMatter: return new Color(0.45f, 0.2f, 0.7f);
                default: return Color.white;
            }
        }

        public static string Name(ElementId id)
        {
            switch (id)
            {
                case ElementId.Fire: return "불";
                case ElementId.Water: return "물";
                case ElementId.Grass: return "풀";
                case ElementId.Stone: return "돌";
                case ElementId.Electric: return "전기";
                default: return "-";
            }
        }

        public static string Name(ComboId id)
        {
            switch (id)
            {
                case ComboId.Fog: return "안개";
                case ComboId.Chili: return "고추";
                case ComboId.Volcano: return "화산";
                case ComboId.Tree: return "나무";
                case ComboId.Soda: return "탄산수";
                case ComboId.Lemon: return "레몬";
                case ComboId.BlueFlame: return "청염";
                case ComboId.MossStone: return "이끼석";
                case ComboId.Seaweed: return "수초";
                case ComboId.Lightning: return "전자레인지";
                case ComboId.Mower: return "예초기";
                case ComboId.Jellyfish: return "해파리";
                case ComboId.Magnet: return "자석";
                default: return "-";
            }
        }

        public static string Name(SpecialId id)
        {
            switch (id)
            {
                case SpecialId.Meteor: return "운석파편";
                case SpecialId.Hellfire: return "헬파이어";
                case SpecialId.Spirit: return "정령";
                case SpecialId.DarkMatter: return "암흑물질";
                default: return "-";
            }
        }

        public static ElementId FromPickup(ActorKind kind)
        {
            switch (kind)
            {
                case ActorKind.PickupFire: return ElementId.Fire;
                case ActorKind.PickupWater: return ElementId.Water;
                case ActorKind.PickupGrass: return ElementId.Grass;
                case ActorKind.PickupStone: return ElementId.Stone;
                case ActorKind.PickupElectric: return ElementId.Electric;
                default: return ElementId.None;
            }
        }

        public static SpecialId SpecialFromPickup(ActorKind kind)
        {
            switch (kind)
            {
                case ActorKind.PickupMeteor: return SpecialId.Meteor;
                case ActorKind.PickupHellfire: return SpecialId.Hellfire;
                case ActorKind.PickupSpirit: return SpecialId.Spirit;
                case ActorKind.PickupDarkMatter: return SpecialId.DarkMatter;
                default: return SpecialId.None;
            }
        }

        public static ActorKind PickupKind(ElementId id)
        {
            switch (id)
            {
                case ElementId.Fire: return ActorKind.PickupFire;
                case ElementId.Water: return ActorKind.PickupWater;
                case ElementId.Grass: return ActorKind.PickupGrass;
                case ElementId.Stone: return ActorKind.PickupStone;
                case ElementId.Electric: return ActorKind.PickupElectric;
                default: return ActorKind.PickupFire;
            }
        }

        public static ActorKind PickupKind(SpecialId id)
        {
            switch (id)
            {
                case SpecialId.Meteor: return ActorKind.PickupMeteor;
                case SpecialId.Hellfire: return ActorKind.PickupHellfire;
                case SpecialId.Spirit: return ActorKind.PickupSpirit;
                case SpecialId.DarkMatter: return ActorKind.PickupDarkMatter;
                default: return ActorKind.PickupMeteor;
            }
        }

        public static bool IsAttributePickup(ActorKind kind) =>
            kind >= ActorKind.PickupFire && kind <= ActorKind.PickupElectric;

        public static bool IsSpecialPickup(ActorKind kind) =>
            kind >= ActorKind.PickupMeteor && kind <= ActorKind.PickupDarkMatter;

        public static bool TryCombo(ElementId a, ElementId b, out ComboId combo)
        {
            return GameplayRules.TryRecipe(a, b, out combo);
        }
    }
}
