using System;
using UnityEditor;
using UnityEngine;

namespace OctopusSurvivors.EditorTools
{
    public static class SystemDataChecks
    {
        // Batch editor play-mode validation, without building a Windows player.
        public static void RunBalancePlayMode()
        {
            ShaderUtil.allowAsyncCompilation = false;
            Run();
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
            EditorApplication.EnterPlaymode();
        }

        [MenuItem("문어/2안 시스템 데이터 검증")]
        public static void Run()
        {
            ElementEffectChecks.VerifyCombinations();
            FiniteMapChecks.Verify();
            if (GameplayRules.Data.recipes.Length != 6 || GameplayRules.Data.abilities.Length != 6)
                throw new Exception("Expected six recipes and effects");
            if (Upgrades.All.Length != 23 || Array.IndexOf(Upgrades.All, UpgradeId.MaxHp) < 0 || Array.IndexOf(Upgrades.All, UpgradeId.MoveSpeed) >= 0)
                throw new Exception("Expected 23 stage upgrades including health and excluding movement speed");
            for (int stage=1;stage<=15;stage++) if (GameConfig.RequiredPieces(stage)<=0) throw new Exception("Invalid stage requirement");
            foreach(var tuning in GameplayRules.Data.abilities)
                if(tuning.damage<=0 || tuning.range<=0 || tuning.duration<=0 || tuning.cooldown<=0)
                    throw new Exception("Invalid effect tuning: "+tuning.combo);
            if(ElementRules.TryCombo(ElementId.Stone,ElementId.Fire,out _)) throw new Exception("Stone remains in active recipes");
            if (GameplayRules.Data.attributeDropChance < 0f || GameplayRules.Data.attributeDropChance >= .05f ||
                GameplayRules.Data.waterProjectileCount < 4 || GameplayRules.Data.attributeRepeatInterval <= 0f)
                throw new Exception("Invalid attribute drop or volley configuration");
            var unique = new System.Collections.Generic.HashSet<UpgradeId>(Upgrades.All);
            if (unique.Count != Upgrades.All.Length) throw new Exception("Duplicate upgrade IDs");
            foreach (var id in Upgrades.All)
                if (Upgrades.MaxPicks(id) != (Upgrades.IsHidden(id) ? 1 : 5)) throw new Exception("Invalid upgrade cap");
            if (Resources.Load<UpgradeTuning>("UpgradeTuning") == null) throw new Exception("Missing Inspector tuning asset");
            Debug.Log("SYSTEM DATA CHECKS PASS");
        }
    }
}
