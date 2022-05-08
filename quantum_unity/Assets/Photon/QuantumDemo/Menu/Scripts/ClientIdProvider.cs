using System;
using Photon.Realtime;

namespace Quantum.Demo {
  public static class ClientIdProvider {
    public enum Type {
      NewGuid                = 0,
      PhotonNickname         = 1,
      PhotonUserId           = 2,
      PhotonNicknamePlusGuid = 3
    }

    public static string CreateClientId(Type type, LoadBalancingClient _loadBalancingClient) {
      switch (type) {
        case Type.NewGuid:
          return Guid.NewGuid().ToString();
        case Type.PhotonNickname:
          return _loadBalancingClient.LocalPlayer.NickName;
        case Type.PhotonNicknamePlusGuid:
          return $"{_loadBalancingClient.LocalPlayer.NickName}_{Guid.NewGuid().ToString()}";
        case Type.PhotonUserId:
          return _loadBalancingClient.LocalPlayer.UserId;
      }
      return string.Empty;
    }
  }
}
