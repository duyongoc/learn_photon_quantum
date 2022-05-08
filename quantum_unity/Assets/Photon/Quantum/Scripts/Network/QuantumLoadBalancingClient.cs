using System.Collections.Generic;
using ExitGames.Client.Photon;
using Photon.Realtime;
using UnityEngine;

// Migration from PUN to Realtime:
// - AutoJoinLobby must be done by hand: call LoadBalancingClient.OpJoinLobby(null); when OnConnectedToMaster()
// - Room list is not cached and is send in chunks, see UILobby.UpdateRoomList()
public class QuantumLoadBalancingClient : LoadBalancingClient, IConnectionCallbacks {
  public static string BestRegionSummaryKey = "Quantum_BestRegionSummary";
  public QuantumLoadBalancingClient(ConnectionProtocol protocol = ConnectionProtocol.Udp) : base(protocol) {
    ConnectionCallbackTargets.Add(this);

    LoadBalancingPeer.SentCountAllowance = 9;
  }
 
  public virtual bool ConnectUsingSettings(AppSettings appSettings, string nickname) {

    LocalPlayer.NickName = nickname;

    if (string.IsNullOrEmpty(appSettings.FixedRegion)) {
      // Hand in the last ping summary to chose best region more quickly.
      appSettings.BestRegionSummaryFromStorage = PlayerPrefs.GetString(BestRegionSummaryKey);
    }

    return ConnectUsingSettings(appSettings);

  }

  public void OnConnected() {
  }

  public void OnConnectedToMaster() {
    // Save the latest ping summary to the disk.
    if (!string.IsNullOrEmpty(SummaryToCache)) {
      PlayerPrefs.SetString(BestRegionSummaryKey, SummaryToCache);
      SummaryToCache = null;
    }
  }

  public void OnDisconnected(DisconnectCause cause) {
  }

  public void OnRegionListReceived(RegionHandler regionHandler) {
  }

  public void OnCustomAuthenticationResponse(Dictionary<string, object> data) {
  }

  public void OnCustomAuthenticationFailed(string debugMessage) {
  }
}
