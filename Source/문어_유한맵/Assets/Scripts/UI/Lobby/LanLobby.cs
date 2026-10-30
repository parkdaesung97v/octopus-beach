using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using UnityEngine;
using UnityEngine.UI;

namespace OctopusSurvivors
{
    public sealed class LanLobby : MonoBehaviour
    {
        public static LanLobby Instance { get; private set; }
        public static bool IsVisible => Instance != null && Instance._root.activeSelf;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Instance = null;
            _backgroundSprite = null;
            _panelSprite = null;
        }

        const float PanelW = 760f;
        const float ContentW = 640f;

        static Sprite _backgroundSprite;
        static Sprite _panelSprite;

        GameObject _root;
        RectTransform _panel;
        Image _octopus;
        Text _title;
        Text _sub;
        RectTransform _solo;
        RectTransform _create;
        RectTransform _joinFieldRt;
        RectTransform _joinButtonRt;
        Text _status;
        RectTransform _devToggle;
        Text _lanHint;
        Text _ipInfo;
        RectTransform _ipFieldRt;
        RectTransform _lanJoinRt;
        Text _controls;
        InputField _ip;
        InputField _joinCode;
        bool _devOpen;

        public static void Show(string status = null)
        {
            Ensure().SetVisible(true, status);
        }

        public static void Hide()
        {
            if (Instance != null)
                Instance.SetVisible(false, null);
        }

        static LanLobby Ensure()
        {
            if (Instance != null)
                return Instance;
            var canvas = UiUtil.CreateOverlay("LobbyCanvas");
            // 화면 높이에 맞춰 크기를 정해 16:9, 4:3, 와이드에서 모두 패널이 화면 밖으로 나가지 않게 한다.
            canvas.GetComponent<CanvasScaler>().matchWidthOrHeight = 1f;
            var lobby = canvas.gameObject.AddComponent<LanLobby>();
            lobby.Build(canvas.transform);
            Instance = lobby;
            return lobby;
        }

        // 위쪽 가운데를 기준으로 위에서부터 잰 y에 배치한다. Panel/Label/Button/Field 모두 같은 방식으로 맞춘다.
        internal static void Place(RectTransform rt, float centerX, float top, float width, float height)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(centerX, -top);
            rt.sizeDelta = new Vector2(width, height);
        }

        internal static Text MakeLabel(Transform parent, float width, float height, int size, Color color, TextAnchor align)
        {
            return UiUtil.Label(parent, Vector2.zero, new Vector2(width, height), size, color, align);
        }

        // 마우스를 올리면 밝아지도록, 기본은 살짝 어둡게 두고 올렸을 때 흰색(원래 색)이 된다.
        internal static void Style(Button button, Color color, int fontSize)
        {
            button.GetComponent<Image>().color = color;
            var colors = button.colors;
            colors.normalColor = new Color(0.86f, 0.86f, 0.86f, 1f);
            colors.highlightedColor = Color.white;
            colors.selectedColor = new Color(0.86f, 0.86f, 0.86f, 1f);
            colors.pressedColor = new Color(0.68f, 0.68f, 0.68f, 1f);
            colors.disabledColor = Color.white;
            button.colors = colors;
            button.GetComponentInChildren<Text>().fontSize = fontSize;
        }

        void Build(Transform canvas)
        {
            _root = new GameObject("LobbyRoot", typeof(RectTransform));
            _root.transform.SetParent(canvas, false);
            Stretch(_root.GetComponent<RectTransform>());

            // 배경: 위는 깊은 바다색, 아래로 갈수록 청록색, 맨 아래는 모래색 띠.
            var bg = new GameObject("Background", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            bg.transform.SetParent(_root.transform, false);
            Stretch(bg.GetComponent<RectTransform>());
            var bgImage = bg.GetComponent<Image>();
            bgImage.sprite = BackgroundSprite();
            bgImage.color = Color.white;

            // 내용에 맞춰 높이가 정해지는 반투명 둥근 패널.
            var panelImage = UiUtil.Panel(_root.transform, Vector2.zero, new Vector2(PanelW, 700), new Color(0.03f, 0.08f, 0.2f, 0.85f));
            panelImage.sprite = PanelSprite();
            panelImage.type = Image.Type.Sliced;
            panelImage.raycastTarget = false;
            _panel = panelImage.rectTransform;
            _panel.anchorMin = _panel.anchorMax = new Vector2(0.5f, 0.5f);
            _panel.pivot = new Vector2(0.5f, 0.5f);

            var octopusSprite = TryOctopusSprite();
            if (octopusSprite != null)
            {
                var go = new GameObject("Octopus", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                go.transform.SetParent(_panel, false);
                _octopus = go.GetComponent<Image>();
                _octopus.sprite = octopusSprite;
                _octopus.preserveAspect = true;
                _octopus.raycastTarget = false;
            }

            _title = MakeLabel(_panel, ContentW + 40f, 100, 72, Color.white, TextAnchor.MiddleCenter);
            _title.text = "문어 뱀서라이크";
            _title.fontStyle = FontStyle.Bold;
            var outline = _title.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.02f, 0.14f, 0.3f, 1f);
            outline.effectDistance = new Vector2(3f, -3f);
            var shadow = _title.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.45f);
            shadow.effectDistance = new Vector2(0f, -6f);

            _sub = MakeLabel(_panel, ContentW + 40f, 40, 28, new Color(0.85f, 0.95f, 1f), TextAnchor.MiddleCenter);
            _sub.text = "혼자 또는 2인 협동으로 즐기는 문어 서바이벌";
            _sub.resizeTextForBestFit = true;
            _sub.resizeTextMinSize = 18;
            _sub.resizeTextMaxSize = 28;
            _sub.verticalOverflow = VerticalWrapMode.Truncate;

            // 눈에 띄는 버튼 3개: 혼자 하기 / 방 만들기 / 코드 입력 + 참가.
            var solo = UiUtil.Button(_panel, Vector2.zero, new Vector2(ContentW, 96), "혼자 하기");
            Style(solo, new Color(0.96f, 0.56f, 0.14f), 38);
            solo.onClick.AddListener(GameBootstrap.StartHost);
            _solo = solo.GetComponent<RectTransform>();

            var netHost = UiUtil.Button(_panel, Vector2.zero, new Vector2(ContentW, 96), "방 만들기");
            Style(netHost, new Color(0.16f, 0.46f, 0.8f), 38);
            netHost.onClick.AddListener(GameBootstrap.StartRelayHost);
            _create = netHost.GetComponent<RectTransform>();

            _joinCode = UiUtil.Field(_panel, Vector2.zero, new Vector2(420, 96), "참가 코드 6글자");
            _joinCode.characterLimit = 8;
            _joinCode.contentType = InputField.ContentType.Alphanumeric;
            _joinCode.characterValidation = InputField.CharacterValidation.None;
            _joinCode.textComponent.fontSize = 34;
            ((Text)_joinCode.placeholder).fontSize = 34;
            _joinFieldRt = _joinCode.GetComponent<RectTransform>();

            var netJoin = UiUtil.Button(_panel, Vector2.zero, new Vector2(200, 96), "참가");
            Style(netJoin, new Color(0.12f, 0.58f, 0.55f), 38);
            netJoin.onClick.AddListener(() => GameBootstrap.StartRelayClient(_joinCode.text));
            _joinButtonRt = netJoin.GetComponent<RectTransform>();

            // 상태/에러 문구: 버튼 바로 아래. 비어 있으면 자리를 차지하지 않고, 있으면 그 높이만큼 아래가 밀린다.
            _status = MakeLabel(_panel, ContentW, 40, 20, new Color(1f, 0.9f, 0.5f), TextAnchor.UpperLeft);
            _status.verticalOverflow = VerticalWrapMode.Truncate;
            _status.text = string.Empty;

            // 개발용 (LAN): 처음에는 접혀 있다.
            var devToggle = UiUtil.Button(_panel, Vector2.zero, new Vector2(240, 44), "개발용 (LAN)");
            Style(devToggle, new Color(0.2f, 0.28f, 0.36f, 0.95f), 22);
            devToggle.onClick.AddListener(ToggleDev);
            _devToggle = devToggle.GetComponent<RectTransform>();

            var soft = new Color(0.72f, 0.82f, 0.9f);
            _lanHint = MakeLabel(_panel, ContentW + 40f, 30, 20, soft, TextAnchor.MiddleCenter);
            _lanHint.text = "LAN 테스트 (같은 네트워크 / 127.0.0.1)";
            _ipInfo = MakeLabel(_panel, ContentW + 40f, 30, 20, soft, TextAnchor.MiddleCenter);
            _ipInfo.text = $"이 PC IP: {LocalIPv4()}   포트 {GameConfig.Port}";

            _ip = UiUtil.Field(_panel, Vector2.zero, new Vector2(400, 64), "상대 Host IP (예: 192.168.0.10)");
            _ip.text = "127.0.0.1";
            _ip.textComponent.fontSize = 24;
            ((Text)_ip.placeholder).fontSize = 22;
            _ipFieldRt = _ip.GetComponent<RectTransform>();

            var lanJoin = UiUtil.Button(_panel, Vector2.zero, new Vector2(220, 64), "Join (LAN 테스트)");
            Style(lanJoin, new Color(0.2f, 0.28f, 0.36f, 0.95f), 18);
            lanJoin.onClick.AddListener(() => GameBootstrap.StartClient(_ip.text));
            _lanJoinRt = lanJoin.GetComponent<RectTransform>();

            // 조작 설명: 맨 아래 작은 글씨 한 줄 (게임 화면 HUD와 같은 문구).
            _controls = MakeLabel(_panel, ContentW + 40f, 30, 18, new Color(0.8f, 0.9f, 0.95f), TextAnchor.MiddleCenter);
            _controls.text = "이동 방향키   획득 E   합성창 F   변신 Space   능력 선택 1 2 3   닫기/나가기 Esc";
            _controls.resizeTextForBestFit = true;
            _controls.resizeTextMinSize = 12;
            _controls.resizeTextMaxSize = 18;
            _controls.verticalOverflow = VerticalWrapMode.Truncate;

            SetDevOpen(false);
        }

        internal static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        void ToggleDev()
        {
            SetDevOpen(!_devOpen);
        }

        void SetDevOpen(bool open)
        {
            _devOpen = open;
            _lanHint.gameObject.SetActive(open);
            _ipInfo.gameObject.SetActive(open);
            _ipFieldRt.gameObject.SetActive(open);
            _lanJoinRt.gameObject.SetActive(open);
            Layout();
        }

        // 위에서 아래로 차례로 쌓고, 마지막 위치로 패널 높이를 정한다. 빈 공간이 남지 않는다.
        void Layout()
        {
            if (_panel == null)
                return;

            float y = 26f;
            if (_octopus != null)
            {
                Place(_octopus.rectTransform, 0f, y, 120f, 120f);
                y += 124f;
            }

            Place(_title.rectTransform, 0f, y, ContentW + 40f, 100f);
            y += 100f;
            Place(_sub.rectTransform, 0f, y, ContentW + 40f, 40f);
            y += 40f + 20f;

            Place(_solo, 0f, y, ContentW, 96f);
            y += 96f + 16f;
            Place(_create, 0f, y, ContentW, 96f);
            y += 96f + 16f;
            Place(_joinFieldRt, -110f, y, 420f, 96f);
            Place(_joinButtonRt, 210f, y, 200f, 96f);
            y += 96f;

            bool hasStatus = !string.IsNullOrEmpty(_status.text);
            _status.gameObject.SetActive(hasStatus);
            if (hasStatus)
            {
                y += 14f;
                Place(_status.rectTransform, 0f, y, ContentW, 40f);
                float h = Mathf.Clamp(_status.preferredHeight, 28f, 150f);
                Place(_status.rectTransform, 0f, y, ContentW, h);
                y += h;
            }

            y += 16f;
            Place(_devToggle, 0f, y, 240f, 44f);
            y += 44f;
            if (_devOpen)
            {
                y += 14f;
                Place(_lanHint.rectTransform, 0f, y, ContentW + 40f, 30f);
                y += 30f;
                Place(_ipInfo.rectTransform, 0f, y, ContentW + 40f, 30f);
                y += 34f;
                Place(_ipFieldRt, -120f, y, 400f, 64f);
                Place(_lanJoinRt, 210f, y, 220f, 64f);
                y += 64f;
            }

            y += 18f;
            Place(_controls.rectTransform, 0f, y, ContentW + 40f, 30f);
            y += 30f + 22f;

            _panel.anchoredPosition = Vector2.zero;
            _panel.sizeDelta = new Vector2(PanelW, y);
        }

        static Sprite TryOctopusSprite()
        {
            try
            {
                return OctopusSheet.Frame(OctopusAnim.Idle, 0);
            }
            catch
            {
                return null; // 그림을 못 불러오면 문어 그림만 건너뛴다.
            }
        }

        // GLSL의 smoothstep(edge0, edge1, x). Mathf.SmoothStep은 (from, to, t)라 경계 판정에 쓰면 안 된다.
        static float Smooth(float edge0, float edge1, float x)
        {
            float t = Mathf.Clamp01((x - edge0) / (edge1 - edge0));
            return t * t * (3f - 2f * t);
        }

        // 코드로 만든 세로 그라데이션. 텍스처 아래쪽이 화면 아래다.
        internal static Sprite BackgroundSprite()
        {
            if (_backgroundSprite != null)
                return _backgroundSprite;

            const int w = 256;
            const int h = 256;
            var deep = new Color(0.01f, 0.07f, 0.22f);
            var shallow = new Color(0.1f, 0.68f, 0.72f);
            var sand = new Color(0.94f, 0.85f, 0.62f);
            var sandDark = new Color(0.8f, 0.68f, 0.45f);
            var foam = new Color(0.88f, 0.98f, 0.96f);

            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };

            var pixels = new Color[w * h];
            for (int y = 0; y < h; y++)
            {
                float t = y / (h - 1f);
                for (int x = 0; x < w; x++)
                {
                    float shore = 0.12f + 0.012f * Mathf.Sin(x * 0.09f) + 0.006f * Mathf.Sin(x * 0.23f + 1f);
                    float u = Mathf.Clamp01((t - shore) / (1f - shore));
                    Color sea = Color.Lerp(shallow, deep, Mathf.SmoothStep(0f, 1f, u));
                    Color beach = Color.Lerp(sand, sandDark, Mathf.Clamp01((shore - t) / shore));
                    Color c = Color.Lerp(beach, sea, Smooth(shore - 0.006f, shore + 0.006f, t));
                    float f = Mathf.Clamp01(1f - Mathf.Abs(t - (shore + 0.012f)) / 0.014f);
                    c = Color.Lerp(c, foam, f * 0.55f);
                    c.a = 1f;
                    pixels[y * w + x] = c;
                }
            }

            tex.SetPixels(pixels);
            tex.Apply(false, true);
            _backgroundSprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
            _backgroundSprite.hideFlags = HideFlags.HideAndDontSave;
            return _backgroundSprite;
        }

        // 모서리가 둥근 9-슬라이스 패널용 Sprite.
        internal static Sprite PanelSprite()
        {
            if (_panelSprite != null)
                return _panelSprite;

            const int size = 64;
            const float radius = 22f;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };

            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float px = x + 0.5f;
                    float py = y + 0.5f;
                    float dx = Mathf.Max(radius - px, px - (size - radius), 0f);
                    float dy = Mathf.Max(radius - py, py - (size - radius), 0f);
                    float d = Mathf.Sqrt(dx * dx + dy * dy) - radius;
                    pixels[y * size + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(0.5f - d));
                }
            }

            tex.SetPixels(pixels);
            tex.Apply(false, true);
            _panelSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f,
                0, SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
            _panelSprite.hideFlags = HideFlags.HideAndDontSave;
            return _panelSprite;
        }

        void SetVisible(bool visible, string status)
        {
            _root.SetActive(visible);
            if (status != null && _status != null)
            {
                _status.text = status;
                Layout();
            }
        }

        public void SetStatus(string status)
        {
            if (_status != null)
            {
                _status.text = status;
                Layout();
            }
        }

        public static string LocalIPv4()
        {
            try
            {
                foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (nic.OperationalStatus != OperationalStatus.Up)
                        continue;
                    if (nic.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                        continue;
                    foreach (var addr in nic.GetIPProperties().UnicastAddresses)
                    {
                        if (addr.Address.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(addr.Address))
                            return addr.Address.ToString();
                    }
                }
            }
            catch
            {
                // ignored
            }

            return "127.0.0.1";
        }
    }
}
