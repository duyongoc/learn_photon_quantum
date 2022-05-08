using System;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using System.Reflection;
using ExitGames.Client.Photon;
using Photon.Realtime;
using UI = UnityEngine.UI;

namespace Quantum.Demo {
  public class UIRoom : UIScreen<UIRoom>, IInRoomCallbacks, IOnEventCallback, IConnectionCallbacks, IMatchmakingCallbacks {
    public UI.Button              StartButton;
    public UI.Text                RoomName;
    public UI.Text                Region;
    public GameObject             WaitingMessage;
    public UI.Dropdown            MapSelectDropdown;
    public UI.Text                ClientCountText;
    public UI.Dropdown            ClientCountDropdown;
    public UI.Toggle              HideRoomOnStartToggle;
    public RectTransform          PlayerGrid;
    public UIRoomPlayer           PlayerTemplate;
    public ClientIdProvider.Type  IdProvider = ClientIdProvider.Type.NewGuid;
    public RuntimeConfigContainer RuntimeConfigContainer;

    public Boolean Spectate = false;

    public Boolean IsRejoining { get; set; }

    private List<MapInfo>      _mapInfo;
    private List<UIRoomPlayer> _players = new List<UIRoomPlayer>();

    public static string LastMapSelected {
      get => PlayerPrefs.GetString("Quantum.Demo.UIRoom.LastMapSelected", "0");
      set => PlayerPrefs.SetString("Quantum.Demo.UIRoom.LastMapSelected", value);
    }

    private class MapInfo {
      public string Scene;
      public AssetGuid Guid;

      public static List<MapInfo> CreateTable() {
        var maps = UnityEngine.Resources.LoadAll<MapAsset>(QuantumEditorSettings.Instance.DatabasePathInResources);
        var list = maps.Select(x => new MapInfo {Guid = x.Settings.Guid, Scene = x.Settings.Scene}).ToList();
        list.Sort((a, b) => String.Compare(a.Scene, b.Scene, StringComparison.Ordinal));
        return list;
      }
    }

    #region Unity MonoBehaviour

    private void Start() {
      PlayerTemplate.Hide();
    }

    #endregion

    #region Unity UI Callbacks

    public void OnDisconnectClicked() {
      UIMain.Client.Disconnect();
    }

    public void OnStartClicked() {
      if (UIMain.Client != null && UIMain.Client.InRoom && UIMain.Client.LocalPlayer.IsMasterClient && UIMain.Client.CurrentRoom.IsOpen) {
        if (!UIMain.Client.OpRaiseEvent((byte)UIMain.PhotonEventCode.StartGame, null, new RaiseEventOptions {Receivers = ReceiverGroup.All}, SendOptions.SendReliable)) {
          Debug.LogError($"Failed to send start game event");
        }
      }
    }

    public void OnMapSelectionChanged(int value) {
      if (UIMain.Client != null && UIMain.Client.InRoom && UIMain.Client.LocalPlayer.IsMasterClient) {
        var selectedScene = MapSelectDropdown.options[value].text;
        var selectedGuid  = _mapInfo.FirstOrDefault(m => m.Scene == selectedScene)?.Guid;
        var ht            = new ExitGames.Client.Photon.Hashtable {{"MAP-GUID", selectedGuid.Value.Value }};
        UIMain.Client.CurrentRoom.SetCustomProperties(ht);
        LastMapSelected = selectedGuid.Value.Value.ToString();
      }
    }

    public void OnPlayerCountChanged(int value) {
      if (UIMain.Client != null && UIMain.Client.InRoom && UIMain.Client.LocalPlayer.IsMasterClient) {
        // Set the dropdown value back, only change this on server validation.
        ClientCountDropdown.value            = UIMain.Client.CurrentRoom.MaxPlayers - 1;
        UIMain.Client.CurrentRoom.MaxPlayers = (byte)(value + 1);
      }
    }

    public void OnHideRoomOnStartChanged(bool value) {
      if (UIMain.Client != null && UIMain.Client.InRoom && UIMain.Client.LocalPlayer.IsMasterClient) {
        var ht = new ExitGames.Client.Photon.Hashtable {{"HIDE-ROOM", value}};
        UIMain.Client.CurrentRoom.SetCustomProperties(ht);
      }
    }

    #endregion

    #region UIScreen

    public override void OnShowScreen(bool first) {
      UIMain.Client?.AddCallbackTarget(this);

      object mapGuidValue = null;

      if (UIMain.Client != null &&
          UIMain.Client.CurrentRoom.CustomProperties.TryGetValue("MAP-GUID", out mapGuidValue) &&
          UIMain.Client.CurrentRoom.CustomProperties.TryGetValue("STARTED",  out var started)) {
        // The game is already running as indicated by the room property. Run the start game procedure.
        Debug.Log("Game already running");
        var mapGuid = (AssetGuid)(long)mapGuidValue;
        StartQuantumGame(mapGuid);
        HideScreen();
        UIGame.ShowScreen();
      }
      else {
        var mapGuid = (AssetGuid)(long)mapGuidValue;
        _mapInfo = MapInfo.CreateTable();
        Assert.Always(_mapInfo.Count > 0);

        MapSelectDropdown.ClearOptions();
        MapSelectDropdown.AddOptions(_mapInfo.Select(m => m.Scene).ToList());
        MapSelectDropdown.value = 0;

        ClientCountDropdown.ClearOptions();
        ClientCountDropdown.AddOptions(Enumerable.Range(1, Quantum.Input.MAX_COUNT).ToList().Select(i => i.ToString()).ToList());
        ClientCountDropdown.value = ClientCountDropdown.options.Count - 1;

        var index = _mapInfo.FindIndex(m => m.Guid == mapGuid);
        MapSelectDropdown.value = index >= 0 ? index : 0;

        UpdateUI();
      }
    }

    public override void OnHideScreen(bool first) {
      UIMain.Client?.RemoveCallbackTarget(this);
      IsRejoining = false;
    }

    #endregion

    #region UIRoom

    private void UpdateUI() {
      if (UIMain.Client == null || UIMain.Client.InRoom == false) {
        UIMain.Client?.Disconnect();
        return;
      }

      // Update UI controls based on if we are the master client.
      var isMasterClient = UIMain.Client.LocalPlayer.IsMasterClient;
      WaitingMessage.Toggle(isMasterClient == false);
      StartButton.Toggle(isMasterClient);
      MapSelectDropdown.interactable        = isMasterClient;
      ClientCountDropdown.interactable      = isMasterClient;
      HideRoomOnStartToggle.interactable    = isMasterClient;

      RoomName.text = UIMain.Client.CurrentRoom.Name;
      Region.text = UIMain.Client.CloudRegion.ToUpper();

      if (UIMain.Client.CurrentRoom.CustomProperties.TryGetValue("HIDE-ROOM", out var hideRoomOnStart)) {
        HideRoomOnStartToggle.isOn = (bool)hideRoomOnStart;
      }

      // Update selected map.
      if (UIMain.Client.CurrentRoom.CustomProperties.TryGetValue("MAP-GUID", out var mapGuid)) {
        var selectedScene  = _mapInfo.FirstOrDefault(m => m.Guid == (long)mapGuid)?.Scene;
        var mapSelectIndex = MapSelectDropdown.options.FindIndex(0, MapSelectDropdown.options.Count, optionData => optionData.text == selectedScene);
        if (MapSelectDropdown.value != mapSelectIndex) {
          // Calling Value directly will trigger the OnChanged callback
          var dropdownValueField = (typeof(UI.Dropdown)).GetField("m_Value", BindingFlags.NonPublic | BindingFlags.Instance);
          dropdownValueField.SetValue(MapSelectDropdown, mapSelectIndex);
          MapSelectDropdown.RefreshShownValue();
        }
      }

      // Update player count
      ClientCountText.text      = UIMain.Client.CurrentRoom.PlayerCount.ToString();
      ClientCountDropdown.value = UIMain.Client.CurrentRoom.MaxPlayers - 1;

      var toggle = ClientCountDropdown.GetComponent<UIDropdownToggle>();
      toggle.DisabledIndices = Enumerable.Range(0, UIMain.Client.CurrentRoom.PlayerCount - 1).ToList();

      // Update player UI
      while (_players.Count < UIMain.Client.CurrentRoom.MaxPlayers) {
        var instance = Instantiate(PlayerTemplate);
        instance.transform.SetParent(PlayerGrid, false);
        instance.transform.SetAsLastSibling();

        _players.Add(instance);
      }

      var i = 0;
      foreach (var player in UIMain.Client.CurrentRoom.Players) {
        _players[i].Name.text = FormatPlayerName(player.Value);
        _players[i].Show();
        i++;
      }

      for (; i < _players.Count; ++i) {
        _players[i].Hide();
      }
    }

    private static String FormatPlayerName(Player player) {
      String playerName  = player.IsLocal ? $"<color=white>{player.NickName}</color>" : player.NickName;
      if (player.IsMasterClient) {
        playerName += " (Master Client)";
      }

      return playerName;
    }

    #endregion

    #region IInRoomCallbacks

    public void OnPlayerEnteredRoom(Player newPlayer) {
      UpdateUI();
    }

    public void OnPlayerLeftRoom(Player otherPlayer) {
      UpdateUI();
    }

    public void OnRoomPropertiesUpdate(Hashtable propertiesThatChanged) {
      UpdateUI();
    }

    public void OnPlayerPropertiesUpdate(Player targetPlayer, Hashtable changedProps) {
    }

    public void OnMasterClientSwitched(Player newMasterClient) {
      UpdateUI();
    }

    #endregion

    #region IOnEventCallback

    public void OnEvent(EventData photonEvent) {
      switch (photonEvent.Code) {
        case (byte)UIMain.PhotonEventCode.StartGame:

          UIMain.Client.CurrentRoom.CustomProperties.TryGetValue("MAP-GUID", out object mapGuidValue);
          if (mapGuidValue == null) {
            UIDialog.Show("Error", "Failed to read the map guid during start", () => UIMain.Client?.Disconnect());
            return;
          }

          if (UIMain.Client.LocalPlayer.IsMasterClient) {
            // Save the started state in room properties for late joiners (TODO: set this from the plugin)
            var ht = new ExitGames.Client.Photon.Hashtable {{"STARTED", true}};
            UIMain.Client.CurrentRoom.SetCustomProperties(ht);

            if (UIMain.Client.CurrentRoom.CustomProperties.TryGetValue("HIDE-ROOM", out var hideRoom) && (bool)hideRoom) {
              UIMain.Client.CurrentRoom.IsVisible = false;
            }
          }

          StartQuantumGame((AssetGuid)(long)mapGuidValue);

          HideScreen();
          UIGame.ShowScreen();

          break;
      }
    }

    private void StartQuantumGame(AssetGuid mapGuid) {
      var config = RuntimeConfigContainer != null ? RuntimeConfig.FromByteArray(RuntimeConfig.ToByteArray(RuntimeConfigContainer.Config)) : new RuntimeConfig();

      config.Map.Id = mapGuid;

      var param = new QuantumRunner.StartParameters {
        RuntimeConfig             = config,
        DeterministicConfig       = DeterministicSessionConfigAsset.Instance.Config,
        ReplayProvider            = null,
        GameMode                  = Spectate ? Photon.Deterministic.DeterministicGameMode.Spectating : Photon.Deterministic.DeterministicGameMode.Multiplayer,
        FrameData                 = IsRejoining ? UIGame.Instance?.FrameSnapshot : null,
        InitialFrame              = IsRejoining ? (UIGame.Instance?.FrameSnapshotNumber).Value : 0,
        PlayerCount               = UIMain.Client.CurrentRoom.MaxPlayers,
        LocalPlayerCount          = Spectate ? 0 : 1,
        RecordingFlags            = RecordingFlags.None,
        NetworkClient             = UIMain.Client,
        StartGameTimeoutInSeconds = 10.0f
      };

      Debug.Log($"Starting QuantumRunner with map guid '{mapGuid}' and requesting {param.LocalPlayerCount} player(s).");

      // Joining with the same client id will result in the same quantum player slot which is important for reconnecting.
      var clientId = ClientIdProvider.CreateClientId(IdProvider, UIMain.Client);
      QuantumRunner.StartGame(clientId, param);

      ReconnectInformation.Refresh(UIMain.Client, TimeSpan.FromMinutes(1));
    }

    #endregion

    #region IConnectionCallbacks

    public void OnConnected() {
    }

    public void OnConnectedToMaster() {
    }

    public void OnDisconnected(DisconnectCause cause) {
      Debug.Log($"Disconnected: {cause}");

      if (cause != DisconnectCause.DisconnectByClientLogic) {
        UIDialog.Show("Disconnected", cause.ToString(), () => {
          HideScreen();
          UIConnect.ShowScreen();
        });
      }
      else {
        HideScreen();
        UIConnect.ShowScreen();
      }
    }

    public void OnRegionListReceived(RegionHandler regionHandler) {
    }

    public void OnCustomAuthenticationResponse(Dictionary<string, object> data) {
    }

    public void OnCustomAuthenticationFailed(string debugMessage) {
    }

    #endregion

    #region IMatchmakingCallbacks

    public void OnFriendListUpdate(List<FriendInfo> friendList) {
    }

    public void OnCreatedRoom() {
    }

    public void OnCreateRoomFailed(short returnCode, string message) {
    }

    public void OnJoinedRoom() {
    }

    public void OnJoinRoomFailed(short returnCode, string message) {
    }

    public void OnJoinRandomFailed(short returnCode, string message) {
    }

    public void OnLeftRoom() {
    }

    #endregion
  }
}