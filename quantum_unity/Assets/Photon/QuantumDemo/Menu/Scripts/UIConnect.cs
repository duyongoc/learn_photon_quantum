using System;
using UnityEngine;
using UI = UnityEngine.UI;

namespace Quantum.Demo {
  public class UIConnect : UIScreen<UIConnect> {
    public PhotonRegions     SelectableRegions;
    public PhotonAppVersions SelectableAppVersion;

    public UI.Dropdown   RegionDropdown;
    public UI.Dropdown   AppVersionDropdown;
    public UI.InputField Username;
    public UI.Button     ReconnectButton;

    private static string LastSelectedRegion {
      get => PlayerPrefs.GetString("Quantum.Demo.UIConnect.LastSelectedRegion", PhotonServerSettings.Instance.AppSettings.FixedRegion);
      set => PlayerPrefs.SetString("Quantum.Demo.UIConnect.LastSelectedRegion", value);
    }

    private static string LastUsername {
      get => PlayerPrefs.GetString("Quantum.Demo.UIConnect.LastUsername", Guid.NewGuid().ToString());
      set => PlayerPrefs.SetString("Quantum.Demo.UIConnect.LastUsername", value);
    }

    public static int LastSelectedAppVersion {
      get => PlayerPrefs.GetInt("Quantum.Demo.UIConnect.LastSelectedAppVersion");
      set => PlayerPrefs.SetInt("Quantum.Demo.UIConnect.LastSelectedAppVersion", value);
    }

    protected new void Awake() {
      base.Awake();

      Username.text = LastUsername;

      var appSettings = PhotonServerSettings.Instance.AppSettings;

      // Create region options
      RegionDropdown.AddOptions(PhotonRegions.CreateDefaultDropdownOptions(out int selectedOption, LastSelectedRegion, appSettings, SelectableRegions));
      RegionDropdown.value = selectedOption;
      RegionDropdown.transform.parent.gameObject.SetActive(string.IsNullOrEmpty(appSettings.Server));

      // Create version options
      AppVersionDropdown.AddOptions(PhotonAppVersions.CreateDefaultDropdownOptions(appSettings, SelectableAppVersion));
      AppVersionDropdown.value = LastSelectedAppVersion;
    }

    public override void OnShowScreen(bool first) {
      base.OnShowScreen(first);

      ReconnectButton.interactable = ReconnectInformation.Instance.IsValid;
    }

    public void OnAppVersionHelpButtonClicked() {
      UIDialog.Show("AppVersion", "The AppVersion (string) separates clients connected to the cloud into different groups. This is important to maintain simultaneous different live version and the matchmaking.\n\nChoosing 'Private' in the demo menu f.e. will only allow players to find each other when they are using the exact same build.");
    }

    public void OnConnectClicked() {
      if (String.IsNullOrEmpty(Username.text.Trim())) {
        UIDialog.Show("Error", "User name not set.");
        return;
      }

      var appSettings = PhotonServerSettings.CloneAppSettings(PhotonServerSettings.Instance.AppSettings);

      LastUsername = Username.text;
      Debug.Log($"Using user name '{Username.text}'");

      UIMain.Client = new QuantumLoadBalancingClient(PhotonServerSettings.Instance.AppSettings.Protocol);

      // Overwrite region
      if (string.IsNullOrEmpty(appSettings.Server) == false) {
        // Direct connect will not set a region
        appSettings.FixedRegion = string.Empty;
      }
      else {
        // Connections to nameserver require an app id
        if (string.IsNullOrEmpty(appSettings.AppIdRealtime.Trim())) {
          UIDialog.Show("Error", "AppId not set.\n\nSearch or create PhotonServerSettings and configure an AppId.");
          return;
        }

        if (RegionDropdown.value == 0) {
          appSettings.FixedRegion = string.Empty;
          LastSelectedRegion = "best";
        }
        else if (SelectableRegions != null && RegionDropdown.value <= SelectableRegions.Regions.Count) {
          appSettings.FixedRegion = SelectableRegions.Regions[RegionDropdown.value - 1].Token;
          LastSelectedRegion = appSettings.FixedRegion;
        }

        Debug.Log($"Using region '{LastSelectedRegion}'");
      }

      // Append selected app version
      appSettings.AppVersion += PhotonAppVersions.AppendAppVersion((PhotonAppVersions.Type)AppVersionDropdown.value, SelectableAppVersion);
      LastSelectedAppVersion = AppVersionDropdown.value;
      Debug.Log($"Using app version '{appSettings.AppVersion}'");

      if (UIMain.Client.ConnectUsingSettings(appSettings, Username.text)) {
        HideScreen();
        UIConnecting.ShowScreen();
      }
      else {
        Debug.LogError($"Failed to connect with app settings: '{appSettings.ToStringFull()}'");
      }
    }

    public void OnReconnectClicked() {
      // Client object is still valid, try a reconnecting
      if (UIMain.Client != null) {
        if (PhotonServerSettings.Instance.CanRejoin) {
          // Reconnect to game server and automatically rejoin the room
          // https://doc.photonengine.com/en-us/realtime/current/troubleshooting/analyzing-disconnects#quick_rejoin__reconnectandrejoin_
          if (UIMain.Client.ReconnectAndRejoin()) {
            Debug.Log($"Reconnecting and rejoining");
            HideScreen();
            UIReconnecting.ShowScreen();
            return;
          }
        }
        else {
          // Reconnect to master server and join back into the room
          if (UIMain.Client.ReconnectToMaster()) {
            Debug.Log($"Reconnecting to master server");
            HideScreen();
            UIReconnecting.ShowScreen();
            return;
          }
        }
      }

      // Client object is null (after app start for example), connect to cloud and join/rejoin room later
      if (UIMain.Client == null && ReconnectInformation.Instance.IsValid) {
        UIMain.Client = new QuantumLoadBalancingClient(PhotonServerSettings.Instance.AppSettings.Protocol);
        UIMain.Client.UserId = ReconnectInformation.Instance.UserId;

        var appSettings = PhotonServerSettings.CloneAppSettings(PhotonServerSettings.Instance.AppSettings);
        appSettings.FixedRegion = ReconnectInformation.Instance.Region;
        appSettings.AppVersion = ReconnectInformation.Instance.AppVersion;

        if (UIMain.Client.ConnectUsingSettings(appSettings, LastUsername)) {
          Debug.Log($"Reconnecting to nameserver using reconnect info {ReconnectInformation.Instance}");
          HideScreen();
          UIReconnecting.ShowScreen();
          return;
        }
      }

      Debug.LogError($"Cannot reconnect");
      ReconnectInformation.Reset();
      ReconnectButton.interactable = false;
    }
  }
}