using System;
using Photon.Realtime;
using UnityEngine;

[Serializable]
[CreateAssetMenu(menuName = "Quantum/Configurations/PhotonServerSettings", order = Quantum.EditorDefines.AssetMenuPriorityConfigurations)]
public class PhotonServerSettings : ScriptableObject {
  public static PhotonServerSettings Instance {
    get {
      if (_instance == null) {
        _instance = Resources.Load<PhotonServerSettings>("PhotonServerSettings");
      }

      return _instance;
    }
  }

  private static PhotonServerSettings _instance;

  // Connect to specific region cloud:        UseNameSever = true,  FixedRegion = "us", Server = ""
  // Connect to best region:                  UseNameSever = true,  FixedRegion = "",   Server = ""
  // Connect to (local) master server:        UseNameSever = false, FixedRegion = "",   Server = "10.0.0.0.", Port = 5055

  public AppSettings AppSettings;
  public int PlayerTtlInSeconds = 0;
  public int EmptyRoomTtlInSeconds = 0;

  public bool CanRejoin => PlayerTtlInSeconds > 0;

  public static AppSettings CloneAppSettings(AppSettings appSettings) {
    return new AppSettings {
      FixedRegion           = appSettings.FixedRegion,
      AppIdChat             = appSettings.AppIdChat,
      AppIdRealtime         = appSettings.AppIdRealtime,
      AppIdVoice            = appSettings.AppIdVoice,
      AppVersion            = appSettings.AppVersion,
      Server                = appSettings.Server,
      AuthMode              = appSettings.AuthMode,
      EnableLobbyStatistics = appSettings.EnableLobbyStatistics,
      NetworkLogging        = appSettings.NetworkLogging,
      Port                  = appSettings.Port,
      Protocol              = appSettings.Protocol,
      UseNameServer         = appSettings.UseNameServer,
      BestRegionSummaryFromStorage = appSettings.BestRegionSummaryFromStorage,
      EnableProtocolFallback = appSettings.EnableProtocolFallback,
      ProxyServer           = appSettings.ProxyServer
    };
  }
}
