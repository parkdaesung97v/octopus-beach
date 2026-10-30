using System;
using UnityEditor;
using UnityEngine;

namespace OctopusSurvivors.EditorTools
{
    public sealed class ElementEffectChecks : EditorWindow
    {
        int _level = 1;
        [MenuItem("문어/요소 효과 테스트 창")]
        static void Open() => GetWindow<ElementEffectChecks>("요소 테스트");

        [MenuItem("문어/조합표 검증")]
        public static void VerifyCombinations()
        {
            Check(ElementId.Fire, ElementId.Water, ComboId.Fog);
            Check(ElementId.Fire, ElementId.Grass, ComboId.Chili);
            Check(ElementId.Water, ElementId.Grass, ComboId.Tree);
            Check(ElementId.Fire, ElementId.Electric, ComboId.BlueFlame);
            Check(ElementId.Grass, ElementId.Electric, ComboId.Lemon);
            Check(ElementId.Water, ElementId.Electric, ComboId.Soda);
            foreach (ElementId e in Enum.GetValues(typeof(ElementId)))
            {
                if (ElementRules.TryCombo(e, e, out _) || ElementRules.TryCombo(e, ElementId.None, out _))
                    throw new Exception("동일/빈 속성의 조합을 허용했습니다.");
            }
            if (GameConfig.ActorBytes != 25) throw new Exception("이펙트 동기화 레코드 크기 불일치");
            Debug.Log("조합표 검증 통과: 6종 × 정방향/역방향, 동일/빈 속성 거부, 네트워크 레코드 크기");
        }

        static void Check(ElementId a, ElementId b, ComboId expected)
        {
            if (!ElementRules.TryCombo(a, b, out var x) || x != expected ||
                !ElementRules.TryCombo(b, a, out var y) || y != expected)
                throw new Exception($"조합 불일치: {a}+{b} → {expected}");
        }

        void OnGUI()
        {
            EditorGUILayout.HelpBox("플레이 모드에서 호스트로 게임을 시작한 뒤 사용하세요. 테스트 버튼은 현재 조합/개별 쿨타임을 바꾸지 않고 효과를 한 번 발동합니다.", MessageType.Info);
            _level = EditorGUILayout.IntSlider("테스트 강화 단계", _level, 1, 5);
            var session = GameSession.Instance;
            var player = OctopusPlayer.Local;
            bool enabled = Application.isPlaying && session != null && session.IsServer && session.IsPlaying &&
                player != null && !player.Downed.Value;
            using (new EditorGUI.DisabledScope(!enabled))
            {
                foreach (ComboId id in Enum.GetValues(typeof(ComboId)))
                {
                    if (id == ComboId.None) continue;
                    if (GUILayout.Button(ElementRules.Name(id))) session.CastElementEffect(player, id, _level);
                }
                if (GUILayout.Button("전방 전류 VFX/범위"))
                    session.CastDirectionalCurrent(player.Pos, player.Facing.Value, 6f, 20f, player);
                if (GUILayout.Button("일반 적 피해 20 테스트")) player.ApplyDamage(20f, DamageSource.NormalEnemy);
                if (GUILayout.Button("보스 스킬 피해 20 테스트")) player.ApplyDamage(20f, DamageSource.BossSkill);
            }
            if (GUILayout.Button("조합표 검증")) VerifyCombinations();
        }
    }
}
