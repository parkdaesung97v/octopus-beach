using System;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace OctopusSurvivors
{
    public sealed class GameBootstrap : MonoBehaviour
    {
        static GameBootstrap _instance;
        NetworkManager _nm;
        bool _sessionSpawned;
        bool _connecting;
        bool _busy;
        float _connectTimer;
        string _connectTarget;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            _instance = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (_instance != null)
                return;
            Physics2D.gravity = Vector2.zero;
            Application.runInBackground = true;
            var go = new GameObject("GameBootstrap");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<GameBootstrap>();
            _instance.Setup();
        }

        void Setup()
        {
            NetworkPrefabFactory.Ensure();
            BuildNetworkManager();
            // IP 안내는 로비의 접힌 "개발용 (LAN)" 영역, 조작 설명은 로비 맨 아래 줄로 옮겼다.
            LanLobby.Show();
        }

        void BuildNetworkManager()
        {
            if (NetworkManager.Singleton != null)
            {
                _nm = NetworkManager.Singleton;
                return;
            }

            var go = new GameObject("NetworkManager");
            go.SetActive(false);
            DontDestroyOnLoad(go);
            var utp = go.AddComponent<UnityTransport>();
            _nm = go.AddComponent<NetworkManager>();
            if (_nm.NetworkConfig == null)
                _nm.NetworkConfig = new NetworkConfig();

            var config = _nm.NetworkConfig;
            config.ProtocolVersion = 4; // Match flame tracking, fruit balance and visuals; actor records remain 25 bytes.
            config.EnableSceneManagement = false;
            config.ForceSamePrefabs = false;
            config.TickRate = 30;
            config.ConnectionApproval = true;
            config.AutoSpawnPlayerPrefabClientSide = false;
            config.PlayerPrefab = NetworkPrefabFactory.PlayerPrefab;
            config.NetworkTransport = utp;
            config.Prefabs.Add(new NetworkPrefab { Prefab = NetworkPrefabFactory.PlayerPrefab });
            config.Prefabs.Add(new NetworkPrefab { Prefab = NetworkPrefabFactory.SessionPrefab });

            _nm.ConnectionApprovalCallback += Approve;
            _nm.OnClientConnectedCallback += OnClientConnected;
            _nm.OnClientDisconnectCallback += OnClientDisconnected;
            go.SetActive(true);
            NetworkPrefabFactory.RegisterHandlers(_nm);
        }

        void Approve(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
        {
            int already = _nm.ConnectedClientsIds.Count;
            response.Approved = already < GameConfig.MaxPlayers && (GameSession.Instance == null || !GameSession.Instance.IsPlaying);
            response.CreatePlayerObject = false;
            response.Reason = response.Approved ? string.Empty : "진행 중인 게임에는 참가할 수 없습니다. 대기실에서 함께 시작해 주세요.";
        }

        void OnClientConnected(ulong clientId)
        {
            if (_nm.IsServer)
            {
                if (!_sessionSpawned)
                    SpawnSession();

                // 대기실을 건너뛰고 진행 중인 판에 늦게 합류한 경우에만 즉시 스폰한다.
                var session = GameSession.Instance;
                if (session != null && session.IsPlaying)
                    SpawnPlayer(clientId);
            }

            if (clientId == _nm.LocalClientId)
            {
                _connecting = false;
                EnterWaitingRoom();
            }
        }

        void OnClientDisconnected(ulong clientId)
        {
            if (clientId == _nm.LocalClientId || !_nm.IsListening)
            {
                GameHud.Hide();
                WaitingRoom.Hide();
                ArenaBuilder.Destroy();
                if (WorldView.Instance != null)
                    WorldView.Instance.Clear();
                LanLobby.Show(_connecting ? ConnectFailMessage() : $"연결 종료. 이 PC IP: {LanLobby.LocalIPv4()}");
                _connecting = false;
                _busy = false;
                _sessionSpawned = false;
                RelaySession.Clear();
            }
        }

        void Update()
        {
            if (!_connecting)
                return;

            _connectTimer += Time.unscaledDeltaTime;
            float giveUp = RelaySession.UsingRelay ? RelaySession.GiveUpSeconds : GameConfig.ConnectGiveUpSeconds + 2f;
            if (_connectTimer < giveUp)
            {
                LanLobby.Instance?.SetStatus(
                    $"{_connectTarget} 로 접속 시도 중... {_connectTimer:0}초 / 약 {giveUp:0}초");
                return;
            }

            _connecting = false;
            if (_nm != null && _nm.IsListening)
                _nm.Shutdown();
            LanLobby.Show(ConnectFailMessage());
        }

        string ConnectFailMessage()
        {
            string reason = _nm != null && !string.IsNullOrEmpty(_nm.DisconnectReason) ? $"\n사유: {_nm.DisconnectReason}" : string.Empty;
            if (RelaySession.UsingRelay)
            {
                return $"{_connectTarget} 인터넷 참가 실패.{reason}\n" +
                       "1) 방장이 먼저 '방 만들기'를 눌렀는지\n" +
                       "2) 참가 코드 6글자가 맞는지\n" +
                       "3) 두 PC 모두 인터넷이 되는지, Unity 프로젝트가 연결됐는지\n" +
                       "확인해 주세요. (docs/Relay설정방법.txt)";
            }

            return $"{_connectTarget} 접속 실패.{reason}\n" +
                   "1) 상대가 먼저 'Host 시작 (LAN 테스트)'을 눌렀는지\n" +
                   "2) IP가 Host PC 것인지 (내 IP 말고)\n" +
                   $"3) Host PC 방화벽에서 포트 {GameConfig.Port} 허용했는지\n" +
                   "확인해 주세요.";
        }

        public static void SpawnAllPlayers()
        {
            if (_instance == null || _instance._nm == null || !_instance._nm.IsServer)
                return;

            var ids = _instance._nm.ConnectedClientsIds;
            for (int i = 0; i < ids.Count; i++)
            {
                ulong id = ids[i];
                if (_instance._nm.ConnectedClients.TryGetValue(id, out var client) && client.PlayerObject != null)
                    continue;
                _instance.SpawnPlayer(id);
            }
        }

        void SpawnSession()
        {
            var go = Instantiate(NetworkPrefabFactory.SessionPrefab);
            go.hideFlags = HideFlags.None;
            go.SetActive(true);
            go.name = "GameSession";
            var no = go.GetComponent<NetworkObject>();
            NetworkPrefabFactory.AssignHash(no, NetHashes.Session);
            no.Spawn(true);
            _sessionSpawned = true;
        }

        void SpawnPlayer(ulong clientId)
        {
            var go = Instantiate(NetworkPrefabFactory.PlayerPrefab);
            go.hideFlags = HideFlags.None;
            go.SetActive(true);
            go.name = "Player_" + clientId;
            var no = go.GetComponent<NetworkObject>();
            NetworkPrefabFactory.AssignHash(no, NetHashes.Player);
            no.SpawnAsPlayerObject(clientId, true);
        }

        public static void StartHost()
        {
            StartHost(GameConfig.Port);
        }

        internal static void StartHost(ushort port)
        {
            if (!CanStartNetwork())
                return;

            RelaySession.Clear();
            _instance.PrepareLanTransport(true, "0.0.0.0");
            if (port != GameConfig.Port)
                _instance._nm.GetComponent<UnityTransport>().SetConnectionData("127.0.0.1", port, "127.0.0.1");
            if (!_instance._nm.StartHost())
            {
                LanLobby.Show($"Host 시작 실패. 포트 {port}이 사용 중인지 확인하세요.");
                return;
            }

            _instance._connecting = false;
            EnterWaitingRoom();
        }

        public static async void StartRelayHost()
        {
            if (!CanStartNetwork())
                return;

            _instance._busy = true;
            try
            {
                LanLobby.Instance?.SetStatus("인터넷 방을 만드는 중... Unity 서비스 연결");
                _instance.PrepareRelayTransport();
                string code = await RelaySession.HostAsync(
                    _instance._nm.GetComponent<UnityTransport>(), GameConfig.MaxPlayers - 1);
                if (!_instance._nm.StartHost())
                {
                    RelaySession.Clear();
                    LanLobby.Show("인터넷 방 시작 실패. 잠시 후 다시 시도하세요.");
                    return;
                }

                GUIUtility.systemCopyBuffer = code;
                _instance._connecting = false;
                EnterWaitingRoom();
            }
            catch (Exception e)
            {
                RelaySession.Clear();
                LanLobby.Show("인터넷 방 만들기 실패.\n" + e.Message);
            }
            finally
            {
                if (_instance != null)
                    _instance._busy = false;
            }
        }

        static void EnterWaitingRoom()
        {
            WaitingRoom.Show();
            LanLobby.Hide();
        }

        public static void StartClient(string ip)
        {
            if (!CanStartNetwork())
                return;

            if (string.IsNullOrWhiteSpace(ip))
                ip = "127.0.0.1";
            ip = ip.Trim();

            if (ip == LanLobby.LocalIPv4())
            {
                LanLobby.Show($"{ip} 는 이 PC의 IP입니다.\n상대(Host) PC의 IP를 입력해 주세요.\n같은 PC 테스트라면 127.0.0.1");
                return;
            }

            RelaySession.Clear();
            _instance.PrepareLanTransport(false, ip);
            if (!_instance._nm.StartClient())
            {
                LanLobby.Show($"Join 실패. IP 형식과 방화벽(포트 {GameConfig.Port})을 확인하세요.");
                return;
            }

            BeginConnecting(ip);
        }

        public static async void StartRelayClient(string code)
        {
            if (!CanStartNetwork())
                return;

            code = RelaySession.Normalize(code);
            if (string.IsNullOrEmpty(code))
            {
                LanLobby.Show("참가 코드를 입력하세요. 방장이 '방 만들기' 후 알려 준 6글자입니다.");
                return;
            }

            _instance._busy = true;
            try
            {
                LanLobby.Instance?.SetStatus($"{code} 인터넷 참가 중... Unity 서비스 연결");
                _instance.PrepareRelayTransport();
                await RelaySession.JoinAsync(_instance._nm.GetComponent<UnityTransport>(), code);
                if (!_instance._nm.StartClient())
                {
                    RelaySession.Clear();
                    LanLobby.Show("인터넷 참가 실패. 코드를 다시 확인하세요.");
                    return;
                }

                BeginConnecting("코드 " + code);
            }
            catch (Exception e)
            {
                RelaySession.Clear();
                LanLobby.Show("인터넷 참가 실패.\n" + e.Message);
            }
            finally
            {
                if (_instance != null)
                    _instance._busy = false;
            }
        }

        static bool CanStartNetwork()
        {
            if (_instance == null || _instance._nm == null)
                return false;
            if (_instance._busy || _instance._connecting || _instance._nm.IsListening)
            {
                LanLobby.Instance?.SetStatus("이미 연결 작업 중입니다. 끝나거나 Esc로 로비로 돌아온 뒤 다시 누르세요.");
                return false;
            }

            return true;
        }

        static void BeginConnecting(string target)
        {
            _instance._connecting = true;
            _instance._connectTimer = 0f;
            _instance._connectTarget = target;
            LanLobby.Instance?.SetStatus($"{target} 로 접속 시도 중...");
        }

        public static void LeaveToLobby()
        {
            if (_instance == null || _instance._nm == null)
                return;
            _instance._connecting = false;
            _instance._busy = false;
            RelaySession.Clear();
            if (_instance._nm.IsListening)
                _instance._nm.Shutdown();
            else
            {
                GameHud.Hide();
                WaitingRoom.Hide();
                LanLobby.Show($"이 PC IP: {LanLobby.LocalIPv4()}");
            }
        }

        void PrepareLanTransport(bool host, string address)
        {
            NetworkPrefabFactory.Ensure();
            var utp = _nm.GetComponent<UnityTransport>();
            utp.UseWebSockets = false;
            utp.ConnectTimeoutMS = GameConfig.ConnectTimeoutMs;
            utp.MaxConnectAttempts = GameConfig.ConnectAttempts;
            if (host)
                utp.SetConnectionData(LanLobby.LocalIPv4(), GameConfig.Port, "0.0.0.0");
            else
                utp.SetConnectionData(address, GameConfig.Port);
        }

        void PrepareRelayTransport()
        {
            NetworkPrefabFactory.Ensure();
            var utp = _nm.GetComponent<UnityTransport>();
            utp.UseWebSockets = false;
            utp.ConnectTimeoutMS = 2000;
            utp.MaxConnectAttempts = 15;
        }
    }
}
