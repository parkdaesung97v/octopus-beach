using UnityEngine;
using UnityEngine.UI;

namespace OctopusSurvivors
{
    public sealed partial class GameHud : MonoBehaviour
    {
        public static GameHud Instance { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Instance = null;
        }

        const int CardCount = GameConfig.UpgradeChoices;

        GameObject _root;
        Text _hpText;
        Text _dashText;
        Image _dashFill;
        Text _loadout;
        Text _keys;
        Text _over;
        Text _stage;
        Image _bossFill;
        GameObject _bossRoot;
        Text _bossLabel;
        Text _octopusCounts;
        Image _hpFill;
        Image _slot0Icon;
        Image _slot1Icon;
        Image _slot2Icon;
        Image _slot3Icon;
        Image _specialIcon;
        Image _relicIcon;
        RectTransform _radarSweep;
        RectTransform _radarSelf;
        RectTransform _radarAlly;
        RectTransform _radarBoss;

        GameObject _cardRoot;
        GameObject _exitRoot;
        Text _exitMessage;
        int _backFrame = -1;
        public bool ExitConfirmationOpen => _exitRoot != null && _exitRoot.activeInHierarchy;
        Text _cardHeader;
        readonly Image[] _cards = new Image[CardCount];
        readonly Text[] _cardTitles = new Text[CardCount];
        readonly Text[] _cardDeltas = new Text[CardCount];

        public static void Show()
        {
            Ensure()._root.SetActive(true);
        }

        public static void Hide()
        {
            if (Instance != null)
            {
                Instance._exitRoot?.SetActive(false);
                Instance.combineRoot?.SetActive(false);
                Instance._root.SetActive(false);
            }
        }

        static GameHud Ensure()
        {
            if (Instance != null)
                return Instance;
            var canvas = UiUtil.CreateOverlay("HudCanvas");
            canvas.sortingOrder = 80;
            var hud = canvas.gameObject.AddComponent<GameHud>();
            hud.Build(canvas.transform);
            Instance = hud;
            return hud;
        }

        void Build(Transform canvas)
        {
            _root = new GameObject("HudRoot", typeof(RectTransform));
            _root.transform.SetParent(canvas, false);
            var rt = _root.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;

            UiUtil.Bar(_root.transform, new Vector2(40, -30), new Vector2(420, 28), new Color(0.15f, 0.1f, 0.1f, 0.8f), new Color(0.85f, 0.2f, 0.25f), out _hpFill);
            _hpText = UiUtil.Label(_root.transform, new Vector2(40, -62), new Vector2(420, 32), 22, Color.white);
            _loadout = UiUtil.Label(_root.transform, new Vector2(112, -105), new Vector2(688, 365), 22, Color.white);
            _loadout.alignment = TextAnchor.UpperLeft;
            UiUtil.Bar(_root.transform, new Vector2(40, -490), new Vector2(220, 12), new Color(.08f,.13f,.2f,.8f), new Color(.3f,.85f,1f), out _dashFill);
            _dashText = UiUtil.Label(_root.transform, new Vector2(40, -508), new Vector2(340, 32), 19, Color.white);
            _slot3Icon = HudIcon(_root.transform, new Vector2(42f, -261f), 46f);
            _slot2Icon = HudIcon(_root.transform, new Vector2(42f, -209f), 46f);
            _slot0Icon = HudIcon(_root.transform, new Vector2(42f, -105f), 46f);
            _slot1Icon = HudIcon(_root.transform, new Vector2(42f, -157f), 46f);
            _specialIcon = HudIcon(_root.transform, new Vector2(42f, -334f), 46f);

            _stage = UiUtil.Label(_root.transform, Vector2.zero, new Vector2(420, 40), 26, new Color(1f, 0.95f, 0.7f), TextAnchor.UpperRight);
            var srt = _stage.rectTransform;
            srt.anchorMin = srt.anchorMax = new Vector2(1f, 1f);
            srt.pivot = new Vector2(1f, 1f);
            srt.anchoredPosition = new Vector2(-40f, -30f);
            _relicIcon = HudIcon(_root.transform, new Vector2(-472f, -24f), 42f, new Vector2(1f, 1f));
            _relicIcon.sprite = RuntimeArt.Icon("RelicPearl");

            _bossRoot = new GameObject("BossBar", typeof(RectTransform));
            _bossRoot.transform.SetParent(_root.transform, false);
            var brt = _bossRoot.GetComponent<RectTransform>();
            brt.anchorMin = brt.anchorMax = new Vector2(0.5f, 1f);
            brt.pivot = new Vector2(0.5f, 1f);
            brt.anchoredPosition = new Vector2(0f, -20f);
            brt.sizeDelta = new Vector2(640f, 60f);
            UiUtil.Bar(_bossRoot.transform, new Vector2(0f, -26f), new Vector2(640, 22), new Color(0.2f, 0.05f, 0.12f, 0.85f), new Color(0.85f, 0.15f, 0.55f), out _bossFill);
            var bossLabel = UiUtil.Label(_bossRoot.transform, new Vector2(0f, 0f), new Vector2(640, 26), 20, new Color(1f, 0.7f, 0.85f), TextAnchor.MiddleCenter);
            _bossLabel = bossLabel;
            _octopusCounts = UiUtil.Label(_bossRoot.transform, new Vector2(0f, -51f), new Vector2(640, 24), 16,
                new Color(1f, .83f, .87f), TextAnchor.MiddleCenter);
            bossLabel.text = "보스";
            _bossRoot.SetActive(false);

            _keys = UiUtil.Label(_root.transform, Vector2.zero, new Vector2(1400, 48), 20, new Color(1f, 1f, 1f, 0.85f));
            var krt = _keys.rectTransform;
            krt.anchorMin = krt.anchorMax = new Vector2(0f, 0f);
            krt.pivot = new Vector2(0f, 0f);
            krt.anchoredPosition = new Vector2(40f, 24f);
            _keys.text = "이동 방향키   획득 E   합성창 F   변신 Space   능력 선택 1 2 3   닫기/나가기 Esc";

            _over = UiUtil.Label(_root.transform, Vector2.zero, new Vector2(520, 120), 40, new Color(1f, 0.45f, 0.4f), TextAnchor.MiddleCenter);
            var ort = _over.rectTransform;
            ort.anchorMin = ort.anchorMax = new Vector2(0.5f, 0.5f);
            ort.pivot = new Vector2(0.5f, 0.5f);
            ort.anchoredPosition = Vector2.zero;
            _over.text = "";

            BuildCards();
            BuildCombine();
            BuildRadar();
            BuildExitConfirmation();
            _root.SetActive(false);
        }

        void BuildExitConfirmation()
        {
            _exitRoot = UiUtil.Panel(_root.transform, Vector2.zero, Vector2.zero, new Color(0, 0, 0, .6f)).gameObject;
            _exitRoot.name = "ExitConfirmation";
            var overlay = _exitRoot.GetComponent<RectTransform>();
            overlay.anchorMin = Vector2.zero; overlay.anchorMax = Vector2.one;
            overlay.offsetMin = overlay.offsetMax = Vector2.zero;
            var panel = UiUtil.Panel(_exitRoot.transform, Vector2.zero, new Vector2(640, 260), new Color(.06f, .1f, .16f, 1));
            _exitMessage = UiUtil.Label(panel.transform, new Vector2(30, -25), new Vector2(580, 125), 25, Color.white, TextAnchor.MiddleCenter);
            var cancel = UiUtil.Button(panel.transform, new Vector2(-145, -75), new Vector2(240, 55), "취소 (Esc)");
            var leave = UiUtil.Button(panel.transform, new Vector2(145, -75), new Vector2(240, 55), "로비로 나가기");
            // Explicit click confirmation; no inherited keyboard selection can submit Leave.
            var navigation = new Navigation { mode = Navigation.Mode.None };
            cancel.navigation = leave.navigation = navigation;
            cancel.onClick.AddListener(() => _exitRoot.SetActive(false));
            leave.onClick.AddListener(() => { _exitRoot.SetActive(false); GameBootstrap.LeaveToLobby(); });
            _exitRoot.SetActive(false);
        }

        public void HandleBack()
        {
            if (_backFrame == Time.frameCount) return;
            _backFrame = Time.frameCount;
            if (DevMenu.CloseIfOpen()) return;
            if (CloseCombineIfOpen()) return;
            if (ExitConfirmationOpen) { _exitRoot.SetActive(false); return; }
            var session = GameSession.Instance;
            if (session == null || !session.IsPlaying) return;
            if (!session.GameOver.Value && session.StageFlow.Value != (byte)StageState.Combat) return;
            _exitMessage.text = session.IsServer
                ? "로비로 나갈까요?\n방장이 나가면 함께 플레이 중인 친구의 연결도 종료됩니다."
                : "로비로 나갈까요?\n나는 게임에서 나가고 방장은 계속 플레이합니다.";
            UnityEngine.EventSystems.EventSystem.current?.SetSelectedGameObject(null);
            _exitRoot.SetActive(true);
            _exitRoot.transform.SetAsLastSibling();
        }

        static Image HudIcon(Transform parent, Vector2 position, float size, Vector2? anchor = null)
        {
            var go = new GameObject("PixelIcon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            Vector2 a = anchor ?? new Vector2(0f, 1f);
            rt.anchorMin = rt.anchorMax = a;
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = position;
            rt.sizeDelta = new Vector2(size, size);
            var image = go.GetComponent<Image>();
            image.preserveAspect = true;
            image.raycastTarget = false;
            image.color = Color.white;
            return image;
        }

        void BuildRadar()
        {
            const float size = 176f;
            var root = new GameObject("Radar", typeof(RectTransform));
            root.transform.SetParent(_root.transform, false);
            var rt = root.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(1f, 0f);
            rt.anchoredPosition = new Vector2(-28f, 84f);
            rt.sizeDelta = new Vector2(size, size);

            var disc = RadarImage(root.transform, size, SpriteFactory.Circle(Color.white));
            disc.color = new Color(0.03f, 0.07f, 0.09f, 0.78f);

            var outer = RadarImage(root.transform, size * 0.92f, SpriteFactory.Ring(Color.white));
            outer.color = new Color(0.45f, 0.85f, 0.75f, 0.45f);
            var mid = RadarImage(root.transform, size * 0.55f, SpriteFactory.Ring(Color.white));
            mid.color = new Color(0.45f, 0.85f, 0.75f, 0.28f);

            RadarBar(root.transform, new Vector2(size, 2f), new Color(0.5f, 0.8f, 0.72f, 0.22f));
            RadarBar(root.transform, new Vector2(2f, size), new Color(0.5f, 0.8f, 0.72f, 0.22f));

            var sweep = RadarBar(root.transform, new Vector2(3f, size * 0.46f), new Color(0.55f, 1f, 0.85f, 0.45f));
            var sweepRt = sweep.rectTransform;
            sweepRt.pivot = new Vector2(0.5f, 0f);
            sweepRt.anchoredPosition = Vector2.zero;
            _radarSweep = sweepRt;

            _radarSelf = RadarImage(root.transform, 12f, SpriteFactory.Circle(Color.white)).rectTransform;
            _radarSelf.GetComponent<Image>().color = new Color(0.85f, 0.95f, 1f);
            _radarAlly = RadarImage(root.transform, 14f, SpriteFactory.Circle(Color.white)).rectTransform;
            _radarAlly.GetComponent<Image>().color = new Color(0.35f, 0.85f, 1f);
            _radarAlly.gameObject.SetActive(false);
            _radarBoss = RadarImage(root.transform, 18f, SpriteFactory.Circle(Color.white)).rectTransform;
            _radarBoss.GetComponent<Image>().color = new Color(1f, 0.25f, 0.55f);
            _radarBoss.gameObject.SetActive(false);
        }

        static Image RadarImage(Transform parent, float size, Sprite sprite)
        {
            var go = new GameObject("RadarPart", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(size, size);
            var image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.raycastTarget = false;
            return image;
        }

        static Image RadarBar(Transform parent, Vector2 size, Color color)
        {
            var go = new GameObject("RadarBar", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = size;
            var image = go.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        void UpdateRadar(OctopusPlayer self, GameSession session)
        {
            if (_radarSweep == null)
                return;

            _radarSweep.localEulerAngles = new Vector3(0f, 0f, -Time.unscaledTime * 120f);
            _radarSelf.anchoredPosition = Vector2.zero;

            const float worldRange = 18f;
            const float pixelRange = 74f;
            Vector2 origin = self.Pos;
            var ally = Teammate(self);
            bool allyOn = ally != null && ally.IsSpawned;
            _radarAlly.gameObject.SetActive(allyOn);
            if (allyOn)
            {
                _radarAlly.anchoredPosition = RadarPoint(ally.Pos - origin, worldRange, pixelRange);
                _radarAlly.GetComponent<Image>().color = ally.Downed.Value
                    ? new Color(0.55f, 0.62f, 0.7f)
                    : new Color(0.35f, 0.85f, 1f);
            }

            Vector2 bossPos = Vector2.zero;
            bool bossOn = session != null && session.BossAlive.Value
                && WorldView.Instance != null && WorldView.Instance.TryBossPosition(out bossPos);
            _radarBoss.gameObject.SetActive(bossOn);
            if (bossOn)
                _radarBoss.anchoredPosition = RadarPoint(bossPos - origin, worldRange, pixelRange);
        }

        static Vector2 RadarPoint(Vector2 delta, float worldRange, float pixelRange)
        {
            float dist = delta.magnitude;
            if (dist > worldRange && dist > 0.001f)
                delta *= worldRange / dist;
            return delta / worldRange * pixelRange;
        }

        void BuildCards()
        {
            _cardRoot = new GameObject("LevelUpCards", typeof(RectTransform));
            _cardRoot.transform.SetParent(_root.transform, false);
            var crt = _cardRoot.GetComponent<RectTransform>();
            crt.anchorMin = crt.anchorMax = new Vector2(0.5f, 0.5f);
            crt.pivot = new Vector2(0.5f, 0.5f);
            crt.anchoredPosition = new Vector2(0f, -140f);
            crt.sizeDelta = new Vector2(1100f, 320f);

            _cardHeader = UiUtil.Label(_cardRoot.transform, Vector2.zero, new Vector2(1100, 50), 32, new Color(1f, 0.92f, 0.5f), TextAnchor.MiddleCenter);
            var hrt = _cardHeader.rectTransform;
            hrt.anchorMin = hrt.anchorMax = new Vector2(0.5f, 1f);
            hrt.pivot = new Vector2(0.5f, 1f);
            hrt.anchoredPosition = new Vector2(0f, 60f);

            float spacing = 360f;
            for (int i = 0; i < CardCount; i++)
            {
                float x = (i - (CardCount - 1) * 0.5f) * spacing;
                var card = UiUtil.Panel(_cardRoot.transform, new Vector2(x, -60f), new Vector2(330f, 290f), new Color(0.11f, 0.13f, 0.18f, 0.96f));
                card.gameObject.name = "Card" + i;
                _cards[i] = card;

                int index = i;
                var button = card.gameObject.AddComponent<Button>();
                button.targetGraphic = card;
                button.onClick.AddListener(() =>
                {
                    var local = OctopusPlayer.Local;
                    if (local != null)
                        local.ChooseUpgrade(index);
                });

                var key = UiUtil.Label(card.transform, new Vector2(18f, -14f), new Vector2(60f, 40f), 30, new Color(1f, 1f, 1f, 0.6f));
                key.text = (i + 1).ToString();

                _cardTitles[i] = UiUtil.Label(card.transform, new Vector2(18f, -58f), new Vector2(294f, 60f), 24, Color.white);
                _cardDeltas[i] = UiUtil.Label(card.transform, new Vector2(18f, -122f), new Vector2(294f, 152f), 21, new Color(0.85f, 0.9f, 1f), TextAnchor.UpperLeft);
                _cardDeltas[i].resizeTextForBestFit = true;
                _cardDeltas[i].resizeTextMinSize = 16;
                _cardDeltas[i].resizeTextMaxSize = 21;
                _cardDeltas[i].verticalOverflow = VerticalWrapMode.Truncate;
            }

            _cardRoot.SetActive(false);
        }

        void Update()
        {
            if (_root == null || !_root.activeSelf)
                return;

            var p = OctopusPlayer.Local;
            if (p == null || !p.IsSpawned)
                return;

            float hp = p.Hp.Value;
            float max = Mathf.Max(1f, p.MaxHp.Value);
            _hpFill.fillAmount = hp / max;
            _dashFill.fillAmount = p.DashCharges.Value == p.MaxDashCharges ? 1f : 1f - p.DashRechargeLeft.Value / GameConfig.DashRecharge;
            _dashText.text = $"Shift 대시 {p.DashCharges.Value}/{p.MaxDashCharges}" +
                (p.DashCharges.Value < p.MaxDashCharges ? $" · {p.DashRechargeLeft.Value:0.0}초" : " · 충전 완료");
            _hpText.text = p.Downed.Value ? "다운" : $"HP  {Mathf.CeilToInt(hp)} / {Mathf.CeilToInt(max)}";

            var session = GameSession.Instance;
            string slots = $"속성1 {SlotText(p,0)}\n\n속성2 {SlotText(p,1)}\n\n속성3 {SlotText(p,2)}\n\n속성4 {SlotText(p,3)}";
            string combos = "";
            for(byte i=0;i<3;i++) combos += (i>0 ? " · " : "") + (p.ComboAt(i)!=ComboId.None ? ElementRules.Name(p.ComboAt(i)) : "빈 칸");
            _loadout.text = $"{slots}\n\n요소: {combos}\n{SpecialText(p)}";
            _slot3Icon.sprite = RuntimeArt.ElementIcon(p.SlotElement(3));
            _slot3Icon.enabled = _slot3Icon.sprite != null;
            _slot2Icon.sprite = RuntimeArt.ElementIcon(p.SlotElement(2));
            _slot2Icon.enabled = _slot2Icon.sprite != null;
            _slot0Icon.sprite = RuntimeArt.ElementIcon(p.SlotElement((byte)0));
            _slot0Icon.enabled = _slot0Icon.sprite != null;
            _slot1Icon.sprite = RuntimeArt.ElementIcon(p.SlotElement((byte)1));
            _slot1Icon.enabled = _slot1Icon.sprite != null;
            _specialIcon.sprite = RuntimeArt.SpecialIcon((SpecialId)p.Special.Value);
            _specialIcon.enabled = _specialIcon.sprite != null;

            if (session != null)
            {
                int stage = session.Stage.Value;
                int got = session.ArtifactDelivered.Value;
                int needPieces = GameConfig.RequiredPieces(stage);
                _relicIcon.enabled = true;
                string held = p.HasArtifact.Value ? "   조각 소지" : "";
                string boss = session.BossAlive.Value ? "   보스전" : "";
                _stage.text = stage % 5 == 0 ? $"스테이지 {stage}   보스 처치 목표" : $"스테이지 {stage}   조각 {got} / {needPieces}{held}";
                bool bossOn = session.BossAlive.Value;
                if (_bossRoot.activeSelf != bossOn)
                    _bossRoot.SetActive(bossOn);
                if (bossOn)
                {
                    _bossFill.fillAmount = session.BossHpRatio.Value;
                    if (session.OctopusBossActive)
                    {
                        var octopus = session.OctopusBoss.Value;
                        _bossLabel.text = octopus.Vulnerable > 0 ? $"문어 보스 무력화 · 본체 공격 가능 {octopus.Vulnerable:F1}초" :
                            octopus.Pattern == 2 ? $"문어 보스 · 본체 보호 ({octopus.LivingArms}/6) · 난타 {octopus.Contacts}/30" :
                            $"문어 보스 · 본체 보호 ({octopus.LivingArms}/6) · {OctopusBossPattern.HalfName(octopus.Half)}쪽 난타";
                        _octopusCounts.text = octopus.Vulnerable > 0 ? "10초 후 다리가 복구됩니다" :
                            $"다리 HP  1:{octopus.ArmHealth(0):P0}   2:{octopus.ArmHealth(1):P0}   3:{octopus.ArmHealth(2):P0}   4:{octopus.ArmHealth(3):P0}   5:{octopus.ArmHealth(4):P0}   6:{octopus.ArmHealth(5):P0}";
                    }
                    else { _bossLabel.text = "보스"; _octopusCounts.text = ""; }
                }
            }

            UpdateRadar(p, session);

            bool over = session != null && session.GameOver.Value;
            if (over)
                _over.text = "게임 오버\nEsc 나가기 확인";
            else if (p.Downed.Value)
            {
                int pct = Mathf.RoundToInt(p.ReviveProgress.Value * 100f);
                _over.text = pct > 0 ? $"다운됨\n부활 중 {pct}%" : "다운됨\n팀원이 파란 원 안에 3초 있으면 부활";
            }
            else
            {
                var ally = Teammate(p);
                if (ally != null && ally.Downed.Value)
                {
                    int pct = Mathf.RoundToInt(ally.ReviveProgress.Value * 100f);
                    _over.text = pct > 0 ? $"팀원 부활 중 {pct}%" : "팀원 다운\n파란 원 안에 3초 서 있으면 부활";
                }
                else
                    _over.text = "";
            }

            UpdateCards(p, over);
            UpdateCombine(p, session);
        }

        void UpdateCards(OctopusPlayer p, bool gameOver)
        {
            var session = GameSession.Instance;
            bool choosing = session != null && session.StageFlow.Value == (byte)StageState.Choosing;
            bool countdown = session != null && session.StageFlow.Value == (byte)StageState.Countdown;
            bool picking = !gameOver && choosing && !p.StageChoiceComplete.Value;
            bool show = !gameOver && (choosing || countdown);
            _cardRoot.SetActive(show);
            if (!show) return;
            var ally = Teammate(p);
            string allyStatus = ally == null ? "" : ally.StageChoiceComplete.Value ? "팀원 선택 완료" : "팀원 선택 중";
            _cardHeader.text = countdown
                ? $"스테이지 클리어! 다음 스테이지까지 {Mathf.CeilToInt(session.NextStageCountdown.Value)}초"
                : picking ? $"스테이지 클리어! 능력 선택 {3-p.PendingPicks.Value}/2" + (ally == null ? "" : " · " + allyStatus)
                : "선택 완료" + (ally == null ? "" : " · " + allyStatus);
            if (!picking)
            {
                for(int i=0;i<CardCount;i++) _cards[i].gameObject.SetActive(false);
                return;
            }

            for (int i = 0; i < CardCount; i++)
            {
                var id = p.Offer(i);
                bool valid = id != UpgradeId.None;
                _cards[i].gameObject.SetActive(valid);
                if (!valid)
                    continue;

                int owned = p.UpgradeCount(id);
                _cardTitles[i].text = $"[{Upgrades.Grade(id)}] {Upgrades.Name(id)}";
                _cardTitles[i].color = Upgrades.Tint(id);
                _cardDeltas[i].text = (Upgrades.IsHidden(id) || id == UpgradeId.ExtraDash ? "1회 습득\n" : $"Lv.{owned} → {owned + 1} / {Upgrades.MaxPicks(id)}\n") + Upgrades.Delta(id, owned);
            }
        }

        static string SpecialText(OctopusPlayer p)
        {
            var session = GameSession.Instance;
            bool powered = session != null && session.SpecialPassive.Value;
            if (p.Special.Value == 0)
            {
                return powered
                    ? "특수요소 없음. 10스테이지 패시브는 켜짐. 특수요소를 먹으면 금색으로 바뀜"
                    : "특수요소 없음 (5스테이지 보스 처치 시 드랍)";
            }

            string name = ElementRules.Name((SpecialId)p.Special.Value);
            string form = p.Transformed.Value ? "변신중" : "기본";
            string cd = p.SpecialCooldownLeft > 0f ? $"스킬 쿨 {p.SpecialCooldownLeft:0.0}초" : "Space 변신 시 즉시 발동";
            string passive = powered
                ? $"패시브 ON  피해 +{(GameConfig.SpecialPassiveMul - 1f) * 100f:0}%  몸 금색"
                : "패시브는 10스테이지 보스 처치 후";
            return $"특수요소 {name}  {form}  {cd}\n{passive}";
        }

        static string TeamUltText(GameSession session)
        {
            if (session == null)
                return "팀 필살기 대기";
            float cd = session.TeamUltCooldown.Value;
            if (cd > 0.05f)
                return $"팀 필살기 쿨 {cd:0.0}초  (변신 상태로 충돌)";
            return "팀 필살기 준비  (둘 다 변신 후 충돌, 30초)";
        }

        static OctopusPlayer Teammate(OctopusPlayer self)
        {
            for (int i = 0; i < OctopusPlayer.All.Count; i++)
            {
                var p = OctopusPlayer.All[i];
                if (p != null && p != self && p.IsSpawned)
                    return p;
            }

            return null;
        }

        static string SlotText(OctopusPlayer p, byte slot)
        {
            var id = p.SlotElement(slot);
            if (id == ElementId.None)
                return "-";
            return ElementRules.Name(id);
        }
    }
}
