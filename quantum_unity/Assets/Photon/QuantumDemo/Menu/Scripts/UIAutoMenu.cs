using System;
using System.Collections.Generic;
using System.Linq;
using ExitGames.Client.Photon;
using Photon.Realtime;
using UnityEngine;

namespace Quantum.Demo {
  public class UIAutoMenu : MonoBehaviour, IConnectionCallbacks, IMatchmakingCallbacks {
    public byte                      MaxPlayers = 1;
    public bool                      WaitForAll;
    public ClientIdProvider.Type     IdProvider = ClientIdProvider.Type.NewGuid;
    public RuntimeConfigContainer    RuntimeConfigContainer;
    public UnityEngine.UI.ScrollRect Console;
    public UnityEngine.UI.Text       ConsoleText;
    public UnityEngine.UI.Button     SkipWaitingButton;
    public UnityEngine.UI.Dropdown   MapDropdown;

    private AssetGuid           _selectedMapGuid;
    private List<AssetGuid>     _mapGuids; 
    private LoadBalancingClient _localBalancingClient;

    public enum State {
      Connecting,
      Error,
      Joining,
      Creating,
      WaitingForPlayers,
      Starting
    }

    #region Properties

    private State _State {
      get { return _state; }
      set {
        _state = value;
        Debug.Log("Setting UIJoinRandom state to " + _state.ToString());
      }
    }

    private State _state;

    #endregion

    #region UnityCallbacks

    public void Start() {
      var maps = UnityEngine.Resources.LoadAll<MapAsset>(QuantumEditorSettings.Instance.DatabasePathInResources);
      MapDropdown.AddOptions(maps.Select(m => m.name).ToList());

      _mapGuids = maps.Select(m => m.AssetObject.Guid).ToList();

      if (RuntimeConfigContainer.Config.Map.Id.IsValid == false && _mapGuids.Count == 1) {
        _selectedMapGuid = _mapGuids[0];
      } else {
        _selectedMapGuid = RuntimeConfigContainer.Config.Map.Id;
      }

      Application.logMessageReceived += Log;

      var serverSettings = PhotonServerSettings.Instance;

      if (string.IsNullOrEmpty(serverSettings.AppSettings.AppIdRealtime)) {
        Debug.LogError("AppId not set");
      }

      _localBalancingClient = new LoadBalancingClient();
      _localBalancingClient.ConnectionCallbackTargets.Add(this);
      _localBalancingClient.MatchMakingCallbackTargets.Add(this);
      _localBalancingClient.AppId      = serverSettings.AppSettings.AppIdRealtime;
      _localBalancingClient.AppVersion = serverSettings.AppSettings.AppVersion;
      _localBalancingClient.ConnectToRegionMaster(serverSettings.AppSettings.FixedRegion);
    }

    public void OnDestroy() {
      Application.logMessageReceived -= Log;
    }

    public void Update() {
      _localBalancingClient?.Service();

      if (_State == State.Starting)
        return;

      if (_localBalancingClient != null && _localBalancingClient.InRoom) {

        var hasStarted = _localBalancingClient.CurrentRoom.CustomProperties.TryGetValue("START", out var start) && (bool)start;
        var mapGuid = (AssetGuid)(_localBalancingClient.CurrentRoom.CustomProperties.TryGetValue("MAP-GUID", out var guid) ? (long)guid: 0L);

        // Only admin posts properties into the room
        if (_localBalancingClient.LocalPlayer.IsMasterClient) {
          var ht = new Hashtable();
          if (!mapGuid.IsValid) {
            if (_selectedMapGuid.IsValid) {
              ht.Add("MAP-GUID", _selectedMapGuid.Value);
            }
            else {
              MapDropdown.gameObject.SetActive(true);
            }
          }

          // Set START to true when we enough players joined or !WaitForAll
          if (!hasStarted && (!WaitForAll || _localBalancingClient.CurrentRoom.PlayerCount >= MaxPlayers)) {
            ht.Add("START", true);
          }
          else if (!hasStarted && WaitForAll) {
            SkipWaitingButton.gameObject.SetActive(true);
          }

          if (ht.Count > 0) {
            _localBalancingClient.CurrentRoom.SetCustomProperties(ht);
          }
        }

        // Everyone is listening for map and start properties
        if (mapGuid.IsValid && hasStarted) { 
          _State = State.Starting;

          Debug.LogFormat("### Starting game using map '{0}'", mapGuid);

          var config = RuntimeConfigContainer != null ? RuntimeConfig.FromByteArray(RuntimeConfig.ToByteArray(RuntimeConfigContainer.Config)) : new RuntimeConfig();
          config.Map.Id = mapGuid;

          var param = new QuantumRunner.StartParameters {
            RuntimeConfig       = config,
            DeterministicConfig = DeterministicSessionConfigAsset.Instance.Config,
            GameMode            = Photon.Deterministic.DeterministicGameMode.Multiplayer,
            PlayerCount         = _localBalancingClient.CurrentRoom.MaxPlayers,
            LocalPlayerCount    = 1,
            NetworkClient       = _localBalancingClient
          };

          var clientId = ClientIdProvider.CreateClientId(IdProvider, _localBalancingClient);
          QuantumRunner.StartGame(clientId, param);

          GetComponentInParent<Canvas>().gameObject.SetActive(false);
        }
      }
    }

    #endregion

    #region UICallbacks

    public void OnUISkipWaitingButtonClicked() {
      if (_State == State.WaitingForPlayers) {
        var ht = new ExitGames.Client.Photon.Hashtable();
        ht.Add("START", true);
        _localBalancingClient.CurrentRoom.SetCustomProperties(ht);
        SkipWaitingButton.gameObject.SetActive(false);
        Debug.Log("Setting START on the Photon room");
      }
    }

    public void OnUIMapDropdownSelected() {
      _selectedMapGuid = _mapGuids[MapDropdown.value];
      Debug.LogFormat("Selected map '{0}' with guid '{1}'", MapDropdown.options[MapDropdown.value].text, _selectedMapGuid);
    }

    #endregion

    #region ConnectionCallbacks

    public void OnConnected() {
    }

    public void OnConnectedToMaster() {
      _State = State.Joining;
      _localBalancingClient.OpJoinRandomRoom(new OpJoinRandomRoomParams {MatchingType = MatchmakingMode.FillRoom});
    }

    public void OnDisconnected(DisconnectCause cause) {
      _State = State.Error;
    }

    public void OnRegionListReceived(RegionHandler regionHandler) {
    }

    public void OnCustomAuthenticationResponse(Dictionary<string, object> data) {
    }

    public void OnCustomAuthenticationFailed(string debugMessage) {
    }

    #endregion

    #region MatchmakingCallbacks

    public void OnFriendListUpdate(List<FriendInfo> friendList) {
    }

    public void OnCreatedRoom() {
    }

    public void OnCreateRoomFailed(short returnCode, string message) {
    }

    public void OnJoinedRoom() {
      _State = State.WaitingForPlayers;
      Debug.LogFormat("Connected to room '{0}' and waiting for other players (isMasterClient = {1})",
                      _localBalancingClient.CurrentRoom.Name,
                      _localBalancingClient.LocalPlayer.IsMasterClient);
    }

    public void OnJoinRoomFailed(short returnCode, string message) {
    }

    public void OnJoinRandomFailed(short returnCode, string message) {
      _State = State.Creating;

      RoomOptions roomOptions = new RoomOptions {
        IsVisible  = true,
        IsOpen     = true,
        MaxPlayers = MaxPlayers,
        Plugins    = new string[] {"QuantumPlugin"}
      };

      _localBalancingClient.OpCreateRoom(new EnterRoomParams() {RoomOptions = roomOptions});

      Debug.LogFormat("Creating new room for '{0}' max players", MaxPlayers);
    }

    public void OnLeftRoom() {
    }

    #endregion

    private void Log(string condition, string stackTrace, LogType type) {
      var color = type == LogType.Log ? "white" : type == LogType.Warning ? "yellow" : "red";
      ConsoleText.text += $"<color={color}>[{type}]</color> {condition}\n";

      while (ConsoleText.preferredHeight > Console.content.sizeDelta.y) {
        var index = ConsoleText.text.IndexOf("\n");
        if (index < 0) break;
        ConsoleText.text = ConsoleText.text.Remove(0, index + 1);
      }

      if (ConsoleText.preferredHeight < Console.viewport.rect.height) {
        Console.verticalScrollbar.value = 1;
      }
      else {
        var scrollPosition =
          (ConsoleText.preferredHeight - Console.viewport.rect.height) /
          (Console.content.sizeDelta.y - Console.viewport.rect.height);
        Console.verticalScrollbar.value = 1.0f - scrollPosition;
      }
    }
  }
}