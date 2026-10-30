using UnityEngine;
using UnityEngine.UI;

namespace OctopusSurvivors
{
    public sealed partial class GameHud
    {
        GameObject combineRoot;
        Text combineTitle;
        readonly Button[] recipeButtons = new Button[6];
        readonly byte[] firstSlots = { 0, 0, 0, 1, 1, 2 };
        readonly byte[] secondSlots = { 1, 2, 3, 2, 3, 3 };
        void BuildCombine()
        {
            combineRoot = UiUtil.Panel(_root.transform, new Vector2(0, 20), new Vector2(720, 610), new Color(.06f,.1f,.16f,.97f)).gameObject;
            var rt = combineRoot.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(.5f,.5f);
            combineTitle = UiUtil.Label(combineRoot.transform,new Vector2(25,-20),new Vector2(670,80),24,Color.white);
            for (int i=0;i<6;i++)
            {
                int pair=i;
                var button=UiUtil.Button(combineRoot.transform,new Vector2(0,170-i*66),new Vector2(650,56),"");
                var navigation=button.navigation;navigation.mode=Navigation.Mode.None;button.navigation=navigation;
                button.onClick.AddListener(()=>
                {
                    var player=OctopusPlayer.Local;
                    if(player!=null) player.RequestCombine(firstSlots[pair],secondSlots[pair]);
                });
                recipeButtons[i]=button;
            }
            var close=UiUtil.Button(combineRoot.transform,new Vector2(0,-265),new Vector2(220,45),"닫기 (F)");
            close.onClick.AddListener(()=>combineRoot.SetActive(false));
            combineRoot.SetActive(false);
        }
        public void ToggleCombine()
        {
            if (ExitConfirmationOpen) return;
            var session=GameSession.Instance;
            if(combineRoot!=null && session!=null && session.CombatActive) combineRoot.SetActive(!combineRoot.activeSelf);
        }
        bool CloseCombineIfOpen()
        {
            if (combineRoot == null || !combineRoot.activeInHierarchy) return false;
            combineRoot.SetActive(false);
            return true;
        }
        void UpdateCombine(OctopusPlayer player, GameSession session)
        {
            if(combineRoot==null || !combineRoot.activeSelf) return;
            if(session==null || !session.CombatActive || player.Downed.Value) { combineRoot.SetActive(false); return; }
            combineTitle.text=$"요소 합성  {player.ComboCount}/3칸 사용 중\n보유 속성으로 합성 · 재료 속성 2개는 소모됩니다";
            for(int i=0;i<6;i++)
            {
                byte a=firstSlots[i],b=secondSlots[i];
                bool valid=ElementRules.TryCombo(player.SlotElement(a),player.SlotElement(b),out var combo);
                recipeButtons[i].GetComponentInChildren<Text>().text=valid
                    ? $"{ElementRules.Name(player.SlotElement(a))} + {ElementRules.Name(player.SlotElement(b))} → {ElementRules.Name(combo)} (재료 소모)"
                    : $"속성 {a+1} + {b+1} : 서로 다른 속성 2개 필요";
                recipeButtons[i].interactable=player.CanCombine(a,b,out _);
            }
        }
    }
}
