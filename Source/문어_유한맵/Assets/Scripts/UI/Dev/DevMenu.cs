using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace OctopusSurvivors
{
    /// <summary>
    /// 테스트용. 게임 중에 스테이지, 특수요소, 패시브, 레벨을 바로 바꾼다.
    /// </summary>
    public sealed class DevMenu : MonoBehaviour
    {
        static DevMenu _instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => _instance = null;

        GameObject _panel;
        GameObject _toggle;
        Text _status;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (_instance != null)
                return;
            var canvas = UiUtil.CreateOverlay("DevMenuCanvas");
            canvas.sortingOrder = 200;
            var menu = canvas.gameObject.AddComponent<DevMenu>();
            menu.Build(canvas.transform);
            _instance = menu;
        }

        void Build(Transform canvas)
        {
            _toggle = UiUtil.Button(canvas, new Vector2(-28f, 28f), new Vector2(120f, 44f), "개발").gameObject;
            var toggleRt = _toggle.GetComponent<RectTransform>();
            toggleRt.anchorMin = toggleRt.anchorMax = new Vector2(1f, 0f);
            toggleRt.pivot = new Vector2(1f, 0f);
            _toggle.GetComponent<Button>().onClick.AddListener(Toggle);
            DisableKeys(_toggle.GetComponent<Button>());

            _panel = UiUtil.Panel(canvas, new Vector2(-24f, -24f), new Vector2(500f, 900f), new Color(0.06f, 0.08f, 0.1f, 0.94f)).gameObject;
            var rt = _panel.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 1f);

            var title = UiUtil.Label(_panel.transform, new Vector2(20f, -16f), new Vector2(420f, 36f), 26, new Color(1f, 0.86f, 0.35f));
            title.text = "개발자 메뉴  (F1)";

            LabelAt(54f, "스테이지");
            Row(92f, ("1", 1), ("5", 5), ("10", 10), ("15", 15), RequestStage);

            LabelAt(168f, "특수요소 (5보스 처치 후)");
            SpecialButton(206f, 20f, "없음", 0);
            SpecialButton(206f, 250f, "운석", (byte)SpecialId.Meteor);
            SpecialButton(258f, 20f, "헬파이어", (byte)SpecialId.Hellfire);
            SpecialButton(258f, 250f, "정령", (byte)SpecialId.Spirit);
            SpecialButton(310f, 20f, "암흑", (byte)SpecialId.DarkMatter);
            var resetCd = TopButton(250f, 310f, 220f, 46f, "쿨 초기화");
            resetCd.onClick.AddListener(ResetSpecialCooldown);

            LabelAt(372f, "속성1  (누른 사람만)");
            ElementRow(408f, 0);
            LabelAt(462f, "속성2  (누른 사람만)");
            ElementRow(498f, 1);

            LabelAt(560f, "10단계 패시브");
            var on = TopButton(20f, 596f, 220f, 46f, "켜기");
            on.onClick.AddListener(() => ApplyPassive(true));
            var off = TopButton(250f, 596f, 220f, 46f, "끄기");
            off.onClick.AddListener(() => ApplyPassive(false));

            LabelAt(660f, "속성3  (누른 사람만)");
            ElementRow(696f, 2);

            LabelAt(752f, "속성4  (누른 사람만)");
            ElementRow(784f, 3);
            _status = UiUtil.Label(_panel.transform, new Vector2(20f, -846f), new Vector2(460f, 48f), 18, new Color(0.75f, 0.9f, 0.75f));
            _status.text = "게임 시작 후 사용할 수 있습니다.";
            _panel.SetActive(false);
        }

        void LabelAt(float y, string text)
        {
            var label = UiUtil.Label(_panel.transform, new Vector2(20f, -y), new Vector2(420f, 28f), 20, new Color(0.8f, 0.86f, 0.92f));
            label.text = text;
        }

        void Row(float y, (string caption, int stage) a, (string caption, int stage) b, (string caption, int stage) c, (string caption, int stage) d, System.Action<int> click)
        {
            Place(a, 20f, y, click);
            Place(b, 130f, y, click);
            Place(c, 240f, y, click);
            Place(d, 350f, y, click);
        }

        void Place((string caption, int stage) item, float x, float y, System.Action<int> click)
        {
            var button = TopButton(x, y, 96f, 48f, item.caption);
            int stage = item.stage;
            button.onClick.AddListener(() => click(stage));
        }

        void SpecialButton(float y, float x, string caption, byte id)
        {
            var button = TopButton(x, y, 220f, 46f, caption);
            button.onClick.AddListener(() => ApplySpecial(id));
        }

        void ElementRow(float y, byte slot)
        {
            ElementId[] elements =
            {
                ElementId.Fire, ElementId.Water, ElementId.Grass, ElementId.Electric
            };
            for (int i = 0; i < elements.Length; i++)
            {
                var id = elements[i];
                var button = TopButton(16f + i * 94f, y, 88f, 44f, ElementRules.Name(id));
                byte slotIndex = slot;
                byte elementId = (byte)id;
                button.onClick.AddListener(() => ApplyElement(slotIndex, elementId));
            }
        }

        Button TopButton(float x, float y, float width, float height, string caption)
        {
            var button = UiUtil.Button(_panel.transform, Vector2.zero, new Vector2(width, height), caption);
            var rt = button.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, -y);
            rt.sizeDelta = new Vector2(width, height);
            DisableKeys(button);
            return button;
        }

        static void DisableKeys(Button button)
        {
            var navigation = button.navigation;
            navigation.mode = Navigation.Mode.None;
            button.navigation = navigation;
        }

        void Update()
        {
            var session = GameSession.Instance;
            bool playing = session != null && session.IsPlaying;
            if (_toggle != null && _toggle.activeSelf != playing)
            {
                _toggle.SetActive(playing);
                if (!playing && _panel != null)
                    _panel.SetActive(false);
            }

            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.f1Key.wasPressedThisFrame && playing)
                Toggle();
        }

        void Toggle()
        {
            if (GameHud.Instance != null && GameHud.Instance.ExitConfirmationOpen) return;
            if (_panel == null)
                return;
            _panel.SetActive(!_panel.activeSelf);
        }

        public static bool CloseIfOpen()
        {
            if (_instance == null || _instance._panel == null || !_instance._panel.activeInHierarchy) return false;
            _instance._panel.SetActive(false);
            return true;
        }

        void RequestStage(int stage)
        {
            var session = GameSession.Instance;
            if (session == null)
                return;
            session.RequestDevStage(stage);
            string boss = stage % 5 == 0 ? "  보스 등장" : "";
            SetStatus($"스테이지 {stage}. 필요 조각 {stage * 2}개{boss}");
        }

        void ApplySpecial(byte id)
        {
            var session = GameSession.Instance;
            if (session == null)
                return;
            session.RequestDevSpecial(id);
            SetStatus(id == 0 ? "특수요소 제거" : $"특수요소 {ElementRules.Name((SpecialId)id)}");
        }

        void ResetSpecialCooldown()
        {
            var session = GameSession.Instance;
            if (session == null)
                return;
            session.RequestDevResetSpecialCooldown();
            SetStatus("특수요소 쿨타임 초기화");
        }

        void ApplyElement(byte slot, byte elementId)
        {
            var session = GameSession.Instance;
            if (session == null)
                return;
            session.RequestDevElement(slot, elementId);
            SetStatus($"내 속성{slot + 1} {ElementRules.Name((ElementId)elementId)}");
        }

        void ApplyPassive(bool on)
        {
            var session = GameSession.Instance;
            if (session == null)
                return;
            session.RequestDevPassive(on);
            SetStatus(on ? "패시브 켜짐. 특수요소가 있으면 금색" : "패시브 꺼짐");
        }

        void SetStatus(string text)
        {
            if (_status != null)
                _status.text = text;
        }
    }
}
