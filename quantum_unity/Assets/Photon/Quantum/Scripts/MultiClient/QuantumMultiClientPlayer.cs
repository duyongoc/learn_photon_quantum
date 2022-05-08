using System.Collections.Generic;
using Photon.Realtime;
using ExitGames.Client.Photon;
using UnityEngine;
using Quantum;
using System;

public class QuantumMultiClientPlayer : MonoBehaviour, IConnectionCallbacks, IMatchmakingCallbacks, IInRoomCallbacks, IOnEventCallback {
  public RuntimeConfig RuntimeConfig;
  public RuntimePlayer RuntimePlayer;
  public QuantumEditorSettings GizmoSettings;
  public int PlayerCount;
  public bool IsFirstPlayer;
  public AssetGuid MapGuid;
  public GameObject PlayerInputTemplate;
  public EntityViewUpdater EntityViewUpdaterTemplate;
  public Action<QuantumMultiClientPlayer> OnPlayerQuitCallback;
  public Action<QuantumMultiClientPlayer> OnPlayerCreatedCallback;

  public string RunnerId => _runner?.Id;
  public GameObject Input => _input;

  QuantumLoadBalancingClient _client;
  EnterRoomParams _enterRoomParams;
  QuantumMultiClientPlayerView _ui;
  QuantumRunner _runner;
  GameObject _input;
  EntityViewUpdater _evu;
  DispatcherSubscription _quantumGameStartedSubscription;

  #region Unity Callbacks

  public void Update() {
    _client?.Service();
  }

  #endregion

  #region Public Methods

  public void BindView(QuantumMultiClientPlayerView view) {
    _ui = view;
    _ui.Label.text = gameObject.name;
    _ui.Input.gameObject.SetActive(false);
    _ui.View.gameObject.SetActive(false);
    _ui.Gizmos.gameObject.SetActive(false);
    _ui.Quit.gameObject.SetActive(false);
  }

  public void Run(AppSettings appSettings) {
    _client = new QuantumLoadBalancingClient(appSettings.Protocol);
    _client.AddCallbackTarget(this);

    if (_client.ConnectUsingSettings(appSettings, name) == false) {
      Debug.LogError("Failed to start connection");
    }
  }

  public void Stop() {
    QuantumCallback.Unsubscribe(_quantumGameStartedSubscription);

    OnPlayerQuitCallback?.Invoke(this);
    OnPlayerQuitCallback = null;
    OnPlayerCreatedCallback = null;

    _runner?.Shutdown();
    _runner = null;

    _client?.Disconnect();
    _client?.RemoveCallbackTarget(this);
    _client = null;

    if (_ui != null) {
      Destroy(_ui.gameObject);
      _ui = null;
    }

    if (_input != null) {
      Destroy(_input);
      _input = null;
    }

    if (_evu != null) {
      Destroy(_evu);
      _evu = null;
    }

    Destroy(gameObject);
  }

  #endregion

  #region Quantum Callbacks

  private void OnQuantumGameStarted(QuantumGame game) {
    foreach (var localPlayer in game.GetLocalPlayers()) {
      game.SendPlayerData(localPlayer, RuntimePlayer ?? new RuntimePlayer());
    }
  }

  #endregion

  #region UI Callbacks

  private void OnInputToggle(bool isEnabled) {
    if (_input != null) {
      _input.SetActive(isEnabled);
    }
  }

  private void OnViewToggle(bool isEnabled) {
    _evu.gameObject.SetActive(isEnabled);
  }

  private void OnGizmoToggle(bool isEnabled) {
    _runner.HideGizmos = !isEnabled;
  }

  private void OnQuitClicked() {
    Stop();
  }

  #endregion

  #region Photon Realtime Callbacks

  public void OnConnected() {
  }

  public void OnConnectedToMaster() {
    if (IsFirstPlayer) {
      _enterRoomParams = new EnterRoomParams();
      _enterRoomParams.RoomOptions = new RoomOptions();
      _enterRoomParams.RoomOptions.IsVisible = true;
      _enterRoomParams.RoomOptions.MaxPlayers = (byte)PlayerCount;
      _enterRoomParams.RoomOptions.Plugins = new string[] { "QuantumPlugin" };
      _enterRoomParams.RoomOptions.CustomRoomProperties = new Hashtable { { "HIDE-ROOM", false } };
      _enterRoomParams.RoomOptions.PlayerTtl = PhotonServerSettings.Instance.PlayerTtlInSeconds * 1000;
      _enterRoomParams.RoomOptions.EmptyRoomTtl = PhotonServerSettings.Instance.EmptyRoomTtlInSeconds * 1000;

      if (!_client.OpCreateRoom(_enterRoomParams)) {
        Debug.Log("Failed to send join or create room operation");
        _client.Disconnect();
      }
    } else {
      if (!_client.OpJoinRandomRoom(new OpJoinRandomRoomParams())) {
        Debug.Log("Failed to OpJoinRandomRoom");
        _client.Disconnect();
      }
    }
  }

  public void OnCreatedRoom() {
  }

  public void OnCreateRoomFailed(short returnCode, string message) {
    Debug.Log($"OnCreateRoomFailed {returnCode} {message}");
  }

  public void OnCustomAuthenticationFailed(string debugMessage) {
    Debug.Log($"OnCustomAuthenticationFailed {debugMessage}");
  }

  public void OnCustomAuthenticationResponse(Dictionary<string, object> data) {
  }

  public void OnDisconnected(DisconnectCause cause) {
    Debug.Log($"{name} disconnected by {cause}");
    Stop();
  }

  public void OnFriendListUpdate(List<FriendInfo> friendList) {
  }

  public void OnJoinedRoom() {
    Debug.Log($"Joined room '{_client.CurrentRoom.Name}'");

    if (_client != null) {
      StartQuantumGame();
    }
  }

  private void StartQuantumGame() {
    var config = RuntimeConfig ?? new RuntimeConfig();

    if (MapGuid.IsValid) {
      config.Map.Id = MapGuid;
    }

    var startParameters = new QuantumRunner.StartParameters {
      RuntimeConfig = config,
      DeterministicConfig = DeterministicSessionConfigAsset.Instance.Config,
      ReplayProvider = null,
      GameMode = Photon.Deterministic.DeterministicGameMode.Multiplayer,
      InitialFrame = 0,
      PlayerCount = _client.CurrentRoom.MaxPlayers,
      LocalPlayerCount = 1,
      RecordingFlags = RecordingFlags.None,
      NetworkClient = _client,
      RunnerId = name
    };

    _runner = QuantumRunner.StartGame(name, startParameters);
    _runner.GizmoSettings = GizmoSettings;

    if (PlayerInputTemplate != null) {
      _input = Instantiate(PlayerInputTemplate);
      _input.transform.SetParent(gameObject.transform);
    }

    if (EntityViewUpdaterTemplate != null) {
      // Use EVU template from parent
      _evu = Instantiate(EntityViewUpdaterTemplate);
      _evu.gameObject.name = $"EntityViewUpdater {name}";
      _evu.gameObject.SetActive(true);
    } else {
      // Create and add our EVU script
      var go = new GameObject($"EntityViewUpdater {name}");
      _evu = go.AddComponent<EntityViewUpdater>();
    }

    _evu.ViewParentTransform = _evu.transform;
    _evu.SetCurrentGame(_runner.Game);
    _evu.transform.SetParent(gameObject.transform);

    _ui.Input.gameObject.SetActive(true);
    _ui.Input.onValueChanged.AddListener(OnInputToggle);
    _ui.View.gameObject.SetActive(true);
    _ui.View.onValueChanged.AddListener(OnViewToggle);
    _ui.Gizmos.gameObject.SetActive(true);
    _ui.Gizmos.onValueChanged.AddListener(OnGizmoToggle);
    _ui.Quit.gameObject.SetActive(true);
    _ui.Quit.onClick.AddListener(OnQuitClicked);

    _ui.Input.isOn = IsFirstPlayer;
    _ui.View.isOn = IsFirstPlayer;
    _ui.Gizmos.isOn = false;

    _quantumGameStartedSubscription = QuantumCallback.Subscribe(this, (CallbackGameStarted c) => OnQuantumGameStarted(c.Game), game => game == _runner.Game);

    OnPlayerCreatedCallback?.Invoke(this);
  }

  public void OnJoinRandomFailed(short returnCode, string message) {
    Debug.Log($"OnJoinRandomFailed {returnCode} {message}");
    _client.Disconnect();
  }

  public void OnJoinRoomFailed(short returnCode, string message) {
    Debug.Log($"OnJoinRoomFailed {returnCode} {message}");
    _client.Disconnect();
  }

  public void OnLeftRoom() {
  }

  public void OnRegionListReceived(RegionHandler regionHandler) {
  }

  public void OnPlayerEnteredRoom(Photon.Realtime.Player newPlayer) {
  }

  public void OnPlayerLeftRoom(Photon.Realtime.Player otherPlayer) {
  }

  public void OnRoomPropertiesUpdate(Hashtable propertiesThatChanged) {
  }

  public void OnPlayerPropertiesUpdate(Photon.Realtime.Player targetPlayer, Hashtable changedProps) {
  }

  public void OnMasterClientSwitched(Photon.Realtime.Player newMasterClient) {
  }

  public void OnEvent(EventData photonEvent) {
  }

  #endregion
}
