using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace OctopusSurvivors
{
    /// <summary>
    /// 접속은 됐지만 아직 게임을 시작하지 않은 상태에서 보여 주는 대기실.
    /// 접속한 플레이어가 준비하면 호스트가 시작한다. 1인 테스트도 지원한다.
    /// </summary>
    public sealed class WaitingRoom : MonoBehaviour
    {
        public static WaitingRoom Instance { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Instance = null;
        }

        public static bool IsVisible => Instance != null && Instance._root.activeSelf;

        const float PanelW = 880f;
        const float ContentW = 800f;

        static readonly Color ReadyOn = new Color(0.12f, 0.58f, 0.55f);
        static readonly Color ReadyDone = new Color(0.22f, 0.66f, 0.34f);
        static readonly Color StartOn = new Color(0.96f, 0.56f, 0.14f); // 시작 화면 "혼자 하기"와 같은 주황색
        static readonly Color ButtonOff = new Color(0.38f, 0.41f, 0.45f);
        static readonly Color SoftText = new Color(0.72f, 0.82f, 0.9f);

        sealed class SlotView
        {
            public RectTransform Rect;
            public Image Bg;
            public Text Name;
            public Text Element;
            public Text Ready;
        }

        sealed class ElementCardView
        {
            public ElementId Element;
            public RectTransform Rect;
            public Image Border;
            public Image Face;
            public Text Label;
        }

        GameObject _root;
        RectTransform _panel;
        Text _title;
        Text _count;
        Text _codeLine;
        Text _codeInfo;
        SlotView _hostSlot;
        SlotView _guestSlot;
        Text _choose;
        ElementCardView[] _cards;
        Button _readyButton;
        Image _readyImage;
        Text _readyLabel;
        Button _startButton;
        Image _startImage;
        Text _startLabel;
        Text _reason;
        Text _netInfo;
        Text _keys;
        int _layoutKey = -1;

        public static void Show()
        {
            var room = Ensure();
            room._layoutKey = -1;
            room._root.SetActive(true);
        }

        public static void Hide()
        {
            if (Instance != null)
                Instance._root.SetActive(false);
        }

        static WaitingRoom Ensure()
        {
            if (Instance != null)
                return Instance;
            var canvas = UiUtil.CreateOverlay("WaitingRoomCanvas");
            canvas.sortingOrder = 120;
            // 시작 화면과 같이 화면 높이 기준으로 크기를 맞춘다.
            canvas.GetComponent<CanvasScaler>().matchWidthOrHeight = 1f;
            var room = canvas.gameObject.AddComponent<WaitingRoom>();
            room.Build(canvas.transform);
            Instance = room;
            return room;
        }

        void Build(Transform canvas)
        {
            _root = new GameObject("WaitingRoomRoot", typeof(RectTransform));
            _root.transform.SetParent(canvas, false);
            LanLobby.Stretch(_root.GetComponent<RectTransform>());

            // 시작 화면과 같은 바다 배경 + 어두운 남색 둥근 패널.
            var bg = new GameObject("Background", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            bg.transform.SetParent(_root.transform, false);
            LanLobby.Stretch(bg.GetComponent<RectTransform>());
            bg.GetComponent<Image>().sprite = LanLobby.BackgroundSprite();

            var panelImage = UiUtil.Panel(_root.transform, Vector2.zero, new Vector2(PanelW, 700), new Color(0.03f, 0.08f, 0.2f, 0.85f));
            panelImage.sprite = LanLobby.PanelSprite();
            panelImage.type = Image.Type.Sliced;
            panelImage.raycastTarget = false;
            _panel = panelImage.rectTransform;
            _panel.anchorMin = _panel.anchorMax = new Vector2(0.5f, 0.5f);
            _panel.pivot = new Vector2(0.5f, 0.5f);

            _title = LanLobby.MakeLabel(_panel, 400, 60, 48, Color.white, TextAnchor.MiddleLeft);
            _title.text = "대기실";
            _title.fontStyle = FontStyle.Bold;
            _count = LanLobby.MakeLabel(_panel, 380, 60, 30, new Color(0.95f, 0.85f, 0.4f), TextAnchor.MiddleRight);

            _codeLine = LanLobby.MakeLabel(_panel, ContentW, 70, 52, Color.white, TextAnchor.MiddleCenter);
            _codeLine.fontStyle = FontStyle.Bold;
            _codeLine.resizeTextForBestFit = true;
            _codeLine.resizeTextMinSize = 28;
            _codeLine.resizeTextMaxSize = 52;
            _codeLine.verticalOverflow = VerticalWrapMode.Truncate;
            _codeInfo = LanLobby.MakeLabel(_panel, ContentW, 26, 20, SoftText, TextAnchor.MiddleCenter);
            _codeInfo.resizeTextForBestFit = true;
            _codeInfo.resizeTextMinSize = 14;
            _codeInfo.resizeTextMaxSize = 20;
            _codeInfo.verticalOverflow = VerticalWrapMode.Truncate;

            _hostSlot = BuildSlot();
            _guestSlot = BuildSlot();

            _choose = LanLobby.MakeLabel(_panel, ContentW, 32, 24, Color.white, TextAnchor.MiddleCenter);
            _choose.text = "시작 속성을 선택하세요";

            var elements = GameplayRules.BasicElements;
            _cards = new ElementCardView[elements.Length];
            for (int i = 0; i < elements.Length; i++)
            {
                ElementId element = elements[i];
                _cards[i] = BuildElementCard(element);
            }

            _readyButton = UiUtil.Button(_panel, Vector2.zero, new Vector2(390, 84), "준비 (Space)");
            LanLobby.Style(_readyButton, ReadyOn, 32);
            _readyButton.onClick.AddListener(ToggleReady);
            _readyLabel = _readyButton.GetComponentInChildren<Text>();
            _readyImage = _readyButton.GetComponent<Image>();
            DisableKeyboardSubmit(_readyButton);

            _startButton = UiUtil.Button(_panel, Vector2.zero, new Vector2(390, 84), "게임 시작 (Enter)");
            LanLobby.Style(_startButton, StartOn, 32);
            _startButton.onClick.AddListener(RequestStart);
            _startLabel = _startButton.GetComponentInChildren<Text>();
            _startImage = _startButton.GetComponent<Image>();
            DisableKeyboardSubmit(_startButton);

            _reason = LanLobby.MakeLabel(_panel, ContentW, 30, 22, new Color(1f, 0.9f, 0.5f), TextAnchor.MiddleCenter);
            _netInfo = LanLobby.MakeLabel(_panel, ContentW, 26, 18, SoftText, TextAnchor.MiddleCenter);
            _keys = LanLobby.MakeLabel(_panel, ContentW, 26, 18, new Color(0.8f, 0.9f, 0.95f), TextAnchor.MiddleCenter);

            Layout(false, false, false, true);
            _root.SetActive(false);
        }

        SlotView BuildSlot()
        {
            var image = UiUtil.Panel(_panel, Vector2.zero, new Vector2(390, 150), new Color(0.1f, 0.2f, 0.34f, 0.95f));
            image.sprite = LanLobby.PanelSprite();
            image.type = Image.Type.Sliced;
            image.raycastTarget = false;

            var view = new SlotView { Rect = image.rectTransform, Bg = image };
            view.Name = LanLobby.MakeLabel(view.Rect, 360, 36, 28, Color.white, TextAnchor.MiddleCenter);
            view.Name.fontStyle = FontStyle.Bold;
            view.Element = LanLobby.MakeLabel(view.Rect, 360, 36, 26, Color.white, TextAnchor.MiddleCenter);
            view.Ready = LanLobby.MakeLabel(view.Rect, 360, 36, 26, Color.white, TextAnchor.MiddleCenter);
            LanLobby.Place(view.Name.rectTransform, 0f, 14f, 360f, 36f);
            LanLobby.Place(view.Element.rectTransform, 0f, 58f, 360f, 36f);
            LanLobby.Place(view.Ready.rectTransform, 0f, 102f, 360f, 36f);
            return view;
        }

        ElementCardView BuildElementCard(ElementId element)
        {
            var go = new GameObject("ElementCard_" + element, typeof(RectTransform));
            go.transform.SetParent(_panel, false);
            var view = new ElementCardView { Element = element, Rect = go.GetComponent<RectTransform>() };

            // 고른 속성은 흰 테두리가 켜진다.
            var border = new GameObject("Border", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            border.transform.SetParent(go.transform, false);
            LanLobby.Stretch(border.GetComponent<RectTransform>());
            view.Border = border.GetComponent<Image>();
            view.Border.sprite = LanLobby.PanelSprite();
            view.Border.type = Image.Type.Sliced;
            view.Border.raycastTarget = false;
            view.Border.color = new Color(1f, 1f, 1f, 0f);

            var button = UiUtil.Button(go.transform, Vector2.zero, new Vector2(175, 102), ElementRules.Name(element));
            var brt = button.GetComponent<RectTransform>();
            brt.anchorMin = Vector2.zero;
            brt.anchorMax = Vector2.one;
            brt.offsetMin = new Vector2(5f, 5f);
            brt.offsetMax = new Vector2(-5f, -5f);
            view.Face = button.GetComponent<Image>();
            view.Face.sprite = LanLobby.PanelSprite();
            view.Face.type = Image.Type.Sliced;
            LanLobby.Style(button, ElementRules.Of(element), 34);
            view.Label = button.GetComponentInChildren<Text>();
            view.Label.fontStyle = FontStyle.Bold;
            button.onClick.AddListener(() => GameSession.Instance?.SelectStartingElement(element));
            DisableKeyboardSubmit(button);
            return view;
        }

        // 위에서 아래로 차례로 쌓는다. 안내가 없는 줄은 자리를 차지하지 않는다.
        void Layout(bool hasCode, bool hasCodeInfo, bool hasNet, bool isHost)
        {
            float y = 26f;
            LanLobby.Place(_title.rectTransform, -200f, y, 400f, 60f);
            LanLobby.Place(_count.rectTransform, 210f, y, 380f, 60f);
            y += 60f + 16f;

            _codeLine.gameObject.SetActive(hasCode);
            _codeInfo.gameObject.SetActive(hasCode && hasCodeInfo);
            if (hasCode)
            {
                LanLobby.Place(_codeLine.rectTransform, 0f, y, ContentW, 70f);
                y += 70f;
                if (hasCodeInfo)
                {
                    LanLobby.Place(_codeInfo.rectTransform, 0f, y, ContentW, 26f);
                    y += 26f;
                }

                y += 14f;
            }

            LanLobby.Place(_hostSlot.Rect, -205f, y, 390f, 150f);
            LanLobby.Place(_guestSlot.Rect, 205f, y, 390f, 150f);
            y += 150f + 22f;

            LanLobby.Place(_choose.rectTransform, 0f, y, ContentW, 32f);
            y += 32f + 8f;

            const float cardW = 185f;
            const float cardGap = 20f;
            for (int i = 0; i < _cards.Length; i++)
            {
                float cx = -ContentW * 0.5f + cardW * 0.5f + i * (cardW + cardGap);
                LanLobby.Place(_cards[i].Rect, cx, y, cardW, 112f);
            }

            y += 112f + 22f;

            _startButton.gameObject.SetActive(isHost);
            if (isHost)
            {
                LanLobby.Place(_readyButton.GetComponent<RectTransform>(), -205f, y, 390f, 84f);
                LanLobby.Place(_startButton.GetComponent<RectTransform>(), 205f, y, 390f, 84f);
            }
            else
            {
                LanLobby.Place(_readyButton.GetComponent<RectTransform>(), 0f, y, ContentW, 84f);
            }

            y += 84f + 8f;
            LanLobby.Place(_reason.rectTransform, 0f, y, ContentW, 30f);
            y += 30f + 14f;

            _netInfo.gameObject.SetActive(hasNet);
            if (hasNet)
            {
                LanLobby.Place(_netInfo.rectTransform, 0f, y, ContentW, 26f);
                y += 26f;
            }

            LanLobby.Place(_keys.rectTransform, 0f, y, ContentW, 26f);
            y += 26f + 22f;

            _panel.anchoredPosition = Vector2.zero;
            _panel.sizeDelta = new Vector2(PanelW, y);
        }

        // Space/Enter를 직접 읽기 때문에, 버튼이 선택되어 같은 키로 두 번 눌리는 걸 막는다.
        static void DisableKeyboardSubmit(Button button)
        {
            var navigation = button.navigation;
            navigation.mode = Navigation.Mode.None;
            button.navigation = navigation;
        }

        void ToggleReady()
        {
            var session = GameSession.Instance;
            if (session != null)
                session.ToggleLocalReady();
        }

        void RequestStart()
        {
            var session = GameSession.Instance;
            if (session != null)
                session.RequestStart();
        }

        void Update()
        {
            if (_root == null || !_root.activeSelf)
                return;

            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.spaceKey.wasPressedThisFrame)
                    ToggleReady();
                if (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame)
                    RequestStart();
                if (keyboard.escapeKey.wasPressedThisFrame && (GameSession.Instance == null || !GameSession.Instance.IsPlaying))
                    GameBootstrap.LeaveToLobby();
            }

            var session = GameSession.Instance;
            if (session == null)
            {
                _count.text = "연결 중...";
                _readyButton.interactable = false;
                _readyImage.color = ButtonOff;
                _startButton.gameObject.SetActive(false);
                _layoutKey = -1;
                return;
            }

            bool isHost = session.IsServer;
            int players = session.PlayerCount.Value;
            bool hostReady = session.HostReady.Value;
            bool guestReady = session.GuestReady.Value;
            bool guestHere = players >= 2;

            _count.text = $"접속 인원 {players} / {GameConfig.MaxPlayers}";

            ShowSlot(_hostSlot, "호스트" + (isHost ? " (나)" : ""), (ElementId)session.HostElement.Value, hostReady, true);
            ShowSlot(_guestSlot, "참가자" + (isHost ? "" : " (나)"), (ElementId)session.GuestElement.Value, guestReady, guestHere);

            byte mine = isHost ? session.HostElement.Value : session.GuestElement.Value;
            for (int i = 0; i < _cards.Length; i++)
            {
                var card = _cards[i];
                bool selected = mine == (byte)card.Element;
                Color c = ElementRules.Of(card.Element);
                card.Face.color = selected ? c : new Color(c.r * 0.45f, c.g * 0.45f, c.b * 0.45f, 0.95f);
                card.Border.color = selected ? Color.white : new Color(1f, 1f, 1f, 0f);
                card.Label.color = selected && Luminance(c) > 0.6f ? new Color(0.08f, 0.1f, 0.14f) : Color.white;
            }

            bool myReady = isHost ? hostReady : guestReady;
            bool canReady = mine != 0;
            _readyButton.interactable = canReady;
            _readyImage.color = !canReady ? ButtonOff : (myReady ? ReadyDone : ReadyOn);
            _readyLabel.text = myReady ? "준비 취소 (Space)" : "준비 (Space)";

            if (isHost)
            {
                bool canStart = session.CanStart();
                _startButton.interactable = canStart;
                _startImage.color = canStart ? StartOn : ButtonOff;
                _startLabel.text = guestHere ? "게임 시작 (Enter)" : "혼자 시작 (Enter)";
                _reason.text = canStart
                    ? string.Empty
                    : (guestHere ? "모두 속성 선택 후 준비하세요" : "속성 선택 후 준비하면 혼자 시작 가능");
            }
            else
            {
                _reason.text = string.Empty;
            }

            // 안내: 인터넷 방이면 참가 코드를 위쪽에 크게 보여 준다. LAN 접속 안내(IP/포트)는 대기실에 두지 않는다.
            bool relay = RelaySession.UsingRelay && !string.IsNullOrEmpty(RelaySession.JoinCode);
            string codeText = string.Empty;
            string codeInfo = string.Empty;
            string netInfo = string.Empty;
            if (relay)
            {
                if (isHost)
                {
                    codeText = $"참가 코드:  {RelaySession.JoinCode}   <size=22>(클립보드에 복사됨)</size>";
                    if (!guestHere)
                        codeInfo = "상대는 로비에서 이 코드를 넣고 [인터넷] 참가";
                }
                else
                {
                    codeText = $"참가 코드:  {RelaySession.JoinCode}";
                    codeInfo = $"인터넷 참가 코드 {RelaySession.JoinCode} 로 들어왔습니다. 호스트가 시작하면 같이 시작됩니다.";
                }
            }
            else if (!isHost)
            {
                netInfo = "호스트가 시작하면 같이 시작됩니다.";
            }

            _codeLine.text = codeText;
            _codeInfo.text = codeInfo;
            _netInfo.text = netInfo;
            _keys.text = isHost ? "Space 준비 / Enter 시작 / Esc 나가기" : "Space 준비 / Esc 나가기";

            bool hasCode = codeText.Length > 0;
            bool hasCodeInfo = codeInfo.Length > 0;
            bool hasNet = netInfo.Length > 0;
            int key = (hasCode ? 1 : 0) | (hasCodeInfo ? 2 : 0) | (hasNet ? 4 : 0) | (isHost ? 8 : 0);
            if (key != _layoutKey)
            {
                _layoutKey = key;
                Layout(hasCode, hasCodeInfo, hasNet, isHost);
            }
        }

        static float Luminance(Color c)
        {
            return 0.299f * c.r + 0.587f * c.g + 0.114f * c.b;
        }

        static void ShowSlot(SlotView slot, string name, ElementId element, bool ready, bool present)
        {
            slot.Name.text = name;
            if (!present)
            {
                // 아직 참가자가 없는 칸.
                slot.Bg.color = new Color(0.1f, 0.14f, 0.2f, 0.8f);
                slot.Name.color = new Color(0.6f, 0.65f, 0.7f);
                slot.Element.text = string.Empty;
                slot.Ready.text = "대기 중";
                slot.Ready.color = new Color(0.6f, 0.65f, 0.7f);
                return;
            }

            slot.Bg.color = ready ? new Color(0.1f, 0.38f, 0.24f, 0.95f) : new Color(0.1f, 0.2f, 0.34f, 0.95f);
            slot.Name.color = Color.white;
            bool chosen = element != ElementId.None;
            slot.Element.text = chosen ? "속성: " + ElementRules.Name(element) : "속성: 미선택";
            slot.Element.color = chosen ? ElementRules.Of(element) : new Color(0.7f, 0.75f, 0.8f);
            slot.Ready.text = ready ? "준비 완료" : "대기 중";
            slot.Ready.color = ready ? new Color(0.45f, 0.9f, 0.55f) : Color.white;
        }
    }
}
