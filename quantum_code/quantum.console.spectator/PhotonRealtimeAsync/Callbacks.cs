using Photon.Realtime;
using System;
using System.Collections.Generic;

namespace PhotonRealtimeAsync {
  public class PhotonConnectionCallbacks {
    public Action ConnectedToMaster;
    public Action ConnectedToNameServer;
    public Action<RegionHandler> RegionListReceived;
    public Action<DisconnectCause> Disconnected;
    public Action<string> CustomAuthenticationFailed;
    public Action<Dictionary<string, object>> CustomAuthenticationResponse;
  }

  public class PhotonMatchmakingCallbacks {
    public Action<List<FriendInfo>> FriendListUpdate;
    public Action JoinedRoom;
    public Action CreatedRoom;
    public Action<short, string> JoinRoomFailed;
    public Action<short, string> JoinRoomRandomFailed;
    public Action<short, string> CreateRoomFailed;
    public Action LeftRoom;
  }
}
