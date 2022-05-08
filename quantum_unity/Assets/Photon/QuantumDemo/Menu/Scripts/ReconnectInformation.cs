using System;
using Photon.Realtime;
using UnityEngine;

namespace Quantum.Demo {
  /// <summary>
  /// Demonstrates a mechanism to save current ronnection data to the disk in order to use it for a OpRejoinRoom() after a disconnect and app restart.
  /// </summary>
  [Serializable]
  public class ReconnectInformation {
    public string Room;
    public string Region;
    public string AppVersion;
    public string UserId;
    public long TimeoutInTicks;

    public DateTime Timeout {
      get => new DateTime(TimeoutInTicks);
      set => TimeoutInTicks = value.Ticks;
    }

    public bool IsValid => Timeout >= DateTime.Now;

    public static ReconnectInformation Instance {
      get {
        var result = JsonUtility.FromJson<ReconnectInformation>(PlayerPrefs.GetString("Quantum.Demo.ReconnectInformation"));
        return result ?? new ReconnectInformation();
      }
      set => PlayerPrefs.SetString("Quantum.Demo.ReconnectInformation", JsonUtility.ToJson(value));
    }

    public static void Reset() {
      PlayerPrefs.SetString("Quantum.Demo.ReconnectInformation", string.Empty);
    }

    public static void Refresh(LoadBalancingClient client, TimeSpan timeout) {
      Instance = new ReconnectInformation {
        Room                = client.CurrentRoom.Name,
        Region              = client.CloudRegion,
        Timeout             = DateTime.Now + timeout,
        UserId              = client.UserId,
        AppVersion          = client.AppVersion
      };
    }

    public override string ToString() {
      return $"Room '{Room}' Region '{Region}' Timeout {Timeout}' AppVersion '{AppVersion}' UserId '{UserId}'";
    }
  }
}