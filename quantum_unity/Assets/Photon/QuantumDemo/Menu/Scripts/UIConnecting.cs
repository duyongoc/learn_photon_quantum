using System.Collections.Generic;
using System.Linq;
using ExitGames.Client.Photon;
using Photon.Realtime;
using UnityEngine;

namespace Quantum.Demo {
  public class UIConnecting : UIScreen<UIConnecting>, IConnectionCallbacks, IMatchmakingCallbacks {
    public RuntimeConfigContainer RuntimeConfigContainer;
    private EnterRoomParams _enterRoomParams;

    #region UIScreen

    public override void OnShowScreen(bool first) {
      UIMain.Client?.AddCallbackTarget(this);
    }

    public override void OnHideScreen(bool first) {
      UIMain.Client?.RemoveCallbackTarget(this);
    }

    #endregion

    #region Unity UI Callbacks

    public void OnDisconnectClicked() {
      UIMain.Client.Disconnect();
    }

    #endregion

    #region IConnectionCallbacks

    public void OnConnected() {
    }

    public void OnConnectedToMaster() {
      if (string.IsNullOrEmpty(UIMain.Client.CloudRegion) == false) {
        Debug.Log($"Connected to master server in region '{UIMain.Client.CloudRegion}'");
      }
      else {
        Debug.Log($"Connected to master server '{UIMain.Client.MasterServerAddress}'");
      }
      Debug.Log($"UserId: {UIMain.Client.UserId}");

      var defaultMapGuid = 0L;
      if (RuntimeConfigContainer != null && RuntimeConfigContainer.Config.Map.Id.IsValid) {
        defaultMapGuid = RuntimeConfigContainer.Config.Map.Id.Value;
      }
      else {
        long.TryParse(UIRoom.LastMapSelected, out defaultMapGuid);

        // The last selected map settings can be from another SDK installation and so have an invalid id.
        var allMapsInResources = UnityEngine.Resources.LoadAll<MapAsset>(QuantumEditorSettings.Instance.DatabasePathInResources);
        if (allMapsInResources.All(m => m.AssetObject.Guid != defaultMapGuid)) {
          defaultMapGuid = 0;
        }
      }

      if (defaultMapGuid == 0) {
        // Fall back to the first map asset we find
        var allMapsInResources = UnityEngine.Resources.LoadAll<MapAsset>(QuantumEditorSettings.Instance.DatabasePathInResources);
        Assert.Always(allMapsInResources.Length > 0);
        defaultMapGuid = allMapsInResources[0].AssetObject.Guid.Value;
      }

      var joinRandomParams = new OpJoinRandomRoomParams();
      _enterRoomParams = new EnterRoomParams();
      _enterRoomParams.RoomOptions = new RoomOptions();
      _enterRoomParams.RoomOptions.IsVisible  = true;
      _enterRoomParams.RoomOptions.MaxPlayers = Input.MAX_COUNT;
      _enterRoomParams.RoomOptions.Plugins    = new string[] { "QuantumPlugin" };
      _enterRoomParams.RoomOptions.CustomRoomProperties = new Hashtable {
        { "HIDE-ROOM", false },
        { "MAP-GUID", defaultMapGuid },
      };
      _enterRoomParams.RoomOptions.PlayerTtl = PhotonServerSettings.Instance.PlayerTtlInSeconds * 1000;
      _enterRoomParams.RoomOptions.EmptyRoomTtl = PhotonServerSettings.Instance.EmptyRoomTtlInSeconds * 1000;

      Debug.Log("Starting random matchmaking");

      if (!UIMain.Client.OpJoinRandomOrCreateRoom(joinRandomParams, _enterRoomParams)) {
        UIMain.Client.Disconnect();
        Debug.LogError($"Failed to send join random operation");
      }
    }

    public void OnDisconnected(DisconnectCause cause) {
      Debug.Log($"Disconnected: {cause}");

      if (cause != DisconnectCause.DisconnectByClientLogic) {
        UIDialog.Show("Connection Failed", cause.ToString(), () => {
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
      UIDialog.Show("Error", $"Create room failed [{returnCode}]: '{message}'", () => UIMain.Client?.Disconnect());
    }

    public void OnJoinedRoom() {
      Debug.Log($"Entered room '{UIMain.Client.CurrentRoom.Name}' as actor '{UIMain.Client.LocalPlayer.ActorNumber}'");
      HideScreen();
      UIRoom.ShowScreen();
    }

    public void OnJoinRoomFailed(short returnCode, string message) {
      UIDialog.Show("Error", $"Joining room failed [{returnCode}]: '{message}'", () => UIMain.Client?.Disconnect());
    }

    public void OnJoinRandomFailed(short returnCode, string message) {
      if (returnCode == ErrorCode.NoRandomMatchFound) {
        if (!UIMain.Client.OpCreateRoom(_enterRoomParams)) {
          UIDialog.Show("Error", "Failed to send join or create room operation", () => UIMain.Client?.Disconnect());
        }
      }
      else {
        UIDialog.Show("Error", $"Join random failed [{returnCode}]: '{message}'", () => UIMain.Client?.Disconnect());
      }
    }

    public void OnLeftRoom() {
      UIDialog.Show("Error", "Left the room unexpectedly", () => UIMain.Client?.Disconnect());
    }

    #endregion
  }
}