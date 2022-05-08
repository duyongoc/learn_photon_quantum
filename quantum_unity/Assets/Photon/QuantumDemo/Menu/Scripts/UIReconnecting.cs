using System.Collections.Generic;
using Photon.Realtime;
using UnityEngine;

namespace Quantum.Demo {
  public class UIReconnecting : UIScreen<UIReconnecting>, IConnectionCallbacks, IMatchmakingCallbacks {
    private int _rejoinIterations;

    #region UIScreen

    public override void OnShowScreen(bool first) {
      _rejoinIterations = 0;
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
      // Reconnected to the master server, try to rejoin first, when it fails it will try to join normally in OnJoinRoomFailed()
      JoinOrRejoin(ReconnectInformation.Instance.Room, PhotonServerSettings.Instance.CanRejoin);
    }

    private void JoinOrRejoin(string roomName, bool rejoin = false) {
      if (rejoin) {
        Debug.Log($"Trying to rejoin room '{roomName}");
        if (!UIMain.Client.OpRejoinRoom(roomName)) {
          Debug.LogError("Failed to send rejoin room operation");
          UIMain.Client.Disconnect();
        }
      } else {
        Debug.Log($"Trying to join room '{roomName}'");
        if (!UIMain.Client.OpJoinRoom(new EnterRoomParams { RoomName = roomName })) {
          Debug.LogError("Failed to send join room operation");
          UIMain.Client.Disconnect();
        }
      }
    }

    public void OnDisconnected(DisconnectCause cause) {
      Debug.Log($"Disconnected: {cause}");

      // Reconnecting failed, reset everything
      UIMain.Client = null;
      ReconnectInformation.Reset();

      switch (cause) {
        case DisconnectCause.DisconnectByClientLogic:
          HideScreen();
          UIConnect.ShowScreen();
          break;

        case DisconnectCause.AuthenticationTicketExpired:
        case DisconnectCause.InvalidAuthentication:
        // This can happen during reconnection when the authentication ticket has expired (timeout is 1 hour)
        // A cloud connect could be initiated here followed by a room rejoin

        default:
          UIDialog.Show("Reconnecting Failed", cause.ToString(), () => {
            HideScreen();
            UIConnect.ShowScreen();
          });
          break;
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
      Debug.Log($"Joined or rejoined room '{UIMain.Client.CurrentRoom.Name}' successfully as actor '{UIMain.Client.LocalPlayer.ActorNumber}'");
      HideScreen();
      UIRoom.Instance.IsRejoining = true;
      UIRoom.ShowScreen();
    }

    public async void OnJoinRoomFailed(short returnCode, string message) {
      switch (returnCode) {
        case ErrorCode.JoinFailedFoundActiveJoiner:
          // This will happen when the client created a new connection and the corresponding actor is still marked active in the room (10 second timeout).
          // In this case we have to try rejoining a couple times.
          if (_rejoinIterations++ < 10) {
            Debug.Log($"Rejoining failed, player is still marked active in the room. Trying again ({_rejoinIterations}/10)");
            await System.Threading.Tasks.Task.Delay(1000);
            JoinOrRejoin(ReconnectInformation.Instance.Room, PhotonServerSettings.Instance.CanRejoin);
            return;
          }
          break;

        case ErrorCode.JoinFailedWithRejoinerNotFound:
          // We tried to rejoin but there is not inactive actor in the room, try joining instead.
          JoinOrRejoin(ReconnectInformation.Instance.Room);
          return;

      }

      Debug.LogError($"Joining or rejoining room failed with error '{returnCode}': {message}");
      UIDialog.Show("Joining Room Failed", message, () => UIMain.Client.Disconnect());
    }

    public void OnJoinRandomFailed(short returnCode, string message) {
    }

    public void OnLeftRoom() {
    }

    #endregion
  }
}