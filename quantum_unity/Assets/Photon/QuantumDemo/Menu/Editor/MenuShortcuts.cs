using Photon.Deterministic;
using Photon.Realtime;
using Quantum.Editor;
using System;
using System.IO;
using System.Linq;
using System.Xml;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Quantum.Demo {
  public static class MenuShortcuts {
    [MenuItem("Quantum/Demo/Open Menu Scene", false, 1)]
    public static void OpenMenuScene() {
      EditorSceneManager.OpenScene("Assets/Photon/QuantumDemo/Menu/Menu.unity");
    }

    [MenuItem("Quantum/Demo/Open Auto Menu Scene", false, 2)]
    public static void OpenAutoMenuScene() {
      EditorSceneManager.OpenScene("Assets/Photon/QuantumDemo/Menu/MenuAuto.unity");
    }

    private static string SpectatorProjectPath => Path.GetFullPath($"{Application.dataPath}/../{Path.GetDirectoryName(QuantumEditorSettings.Instance.QuantumSolutionPath)}/quantum.console.spectator");

    [MenuItem("Quantum/Demo/Start Spectator (Create Room)", false, 21)]
    public static void StartSpectatorAndCreateRoom() {
      var maps = UnityEngine.Resources.LoadAll<MapAsset>(QuantumEditorSettings.Instance.DatabasePathInResources);

      switch (maps.Length) {
        case 0: Log.Error("No maps found"); break;
        case 1: StartSpectatorAndCreateRoom(maps[0].Settings.Guid); break;
        default:  MapSelectionWindows.Init(StartSpectatorAndCreateRoom, maps); break;
      }
    }

    public static void StartSpectatorAndCreateRoom(AssetGuid mapId) {
      var sessionConfig = DeterministicSessionConfig.FromByteArray(DeterministicSessionConfig.ToByteArray(DeterministicSessionConfigAsset.Instance.Config));
      sessionConfig.PlayerCount = Input.MAX_COUNT;

      var runtimeConfig = new RuntimeConfig();
      runtimeConfig.Map.Id = mapId;
      runtimeConfig.SimulationConfig.Id = SimulationConfig.DEFAULT_ID;

      var appSettings = PhotonServerSettings.CloneAppSettings(PhotonServerSettings.Instance.AppSettings);
      // Todo: version selection
      appSettings.AppVersion += $" {PhotonAppVersions.Private}";

      var enterRoomParams = new SerializableEnterRoomParams();
      enterRoomParams.RoomOptions = new RoomOptions();
      enterRoomParams.RoomOptions.IsVisible = true;
      enterRoomParams.RoomOptions.MaxPlayers = Input.MAX_COUNT;
      enterRoomParams.RoomOptions.Plugins = new string[] { "QuantumPlugin" };
      enterRoomParams.RoomOptions.PlayerTtl = PhotonServerSettings.Instance.PlayerTtlInSeconds * 1000;
      enterRoomParams.RoomOptions.EmptyRoomTtl = PhotonServerSettings.Instance.EmptyRoomTtlInSeconds * 1000;
      enterRoomParams.RoomOptions.CustomRoomProperties = new ExitGames.Client.Photon.Hashtable {
        { "HIDE-ROOM", false },
        { "MAP-GUID", mapId.Value },
        { "STARTED", true} };

      ExportSpectatorAssets(runtimeConfig, sessionConfig, appSettings, enterRoomParams);
      StartSpectator();
    }

    [MenuItem("Quantum/Demo/Start Spectator (Join Current Room)", true, 22)]
    public static bool StartSpectatorAndJoinRoomTest() {
      return Application.isPlaying && UIMain.Client != null && UIMain.Client.InRoom;
    }

    [MenuItem("Quantum/Demo/Start Spectator (Join Current Room)", false, 22)]
    public static void StartSpectatorAndJoinRoom() {
      if (UIMain.Client == null || UIMain.Client.InRoom == false) {
        Debug.LogError("Not connected or not in room");
        return;
      }

      if (UIMain.Client.CurrentRoom.CustomProperties.TryGetValue("MAP-GUID", out var mapGuid) == false) {
        Debug.LogError("Map guid not found in room");
        return;
      }

      if (UIMain.Client.CurrentRoom.CustomProperties.TryGetValue("STARTED", out var started) == false || (bool)started == false) {
        Debug.LogError("Game not started");
        return;
      }

      var sessionConfig = DeterministicSessionConfig.FromByteArray(DeterministicSessionConfig.ToByteArray(DeterministicSessionConfigAsset.Instance.Config));
      sessionConfig.PlayerCount = Input.MAX_COUNT;

      var runtimeConfig = new RuntimeConfig();
      runtimeConfig.Map.Id = (long)mapGuid;
      runtimeConfig.SimulationConfig.Id = SimulationConfig.DEFAULT_ID;

      var appSettings = PhotonServerSettings.CloneAppSettings(PhotonServerSettings.Instance.AppSettings);
      appSettings.FixedRegion = UIMain.Client.CloudRegion;
      appSettings.AppVersion = UIMain.Client.AppVersion;

      var enterRoomParams = new SerializableEnterRoomParams();
      enterRoomParams.RoomName = UIMain.Client.CurrentRoom.Name;

      ExportSpectatorAssets(runtimeConfig, sessionConfig, appSettings, enterRoomParams);
      StartSpectator();
    }
    
    public static void StartSpectator() {
#if !UNITY_EDITOR_WIN
      // get dotnet and mono working on Mac
      Debug.LogWarning("Spectator menu commands are not supported.");
      return;
#else
      var projectPath = $"{SpectatorProjectPath}/quantum.console.spectator.csproj";
      var exePath = $"{SpectatorProjectPath}/bin/quantum.console.spectator.exe";

      // compile project
      if (RunBuild(projectPath)) {
        // start exe
        RunExe(exePath, true);
      }
#endif
    }

    public static void ExportSpectatorAssets(RuntimeConfig runtimeConfig, DeterministicSessionConfig sessionConfig, AppSettings appSettings, SerializableEnterRoomParams enterRoomParams) {
      var assetPath = $"{SpectatorProjectPath}/bin/assets";
      if (Directory.Exists(assetPath) == false) {
        try {
          Directory.CreateDirectory(assetPath);
        } catch (Exception e) {
          Log.Error($"Failed to create the directory {assetPath}");
          Log.Exception(e);
          return;
        }
      }

      // export asset db
      AssetDBGeneration.Export(PathUtils.Combine(assetPath, "db.json"));

      // export app settings
      File.WriteAllText(PathUtils.Combine(assetPath, "AppSettings.json"), JsonUtility.ToJson(appSettings, true));

      // export session config
      File.WriteAllText(PathUtils.Combine(assetPath, "SessionConfig.json"), JsonUtility.ToJson(sessionConfig, true));

      // export runtime config
      File.WriteAllText(PathUtils.Combine(assetPath, "RuntimeConfig.json"), JsonUtility.ToJson(runtimeConfig, true));

      // export room settings (using custom xml to support hashtables)
      using (var writer = XmlWriter.Create(PathUtils.Combine(assetPath, "EnterRoomParams.xml"), new XmlWriterSettings { Indent = true }))
        SerializableEnterRoomParams.Serialize(writer, enterRoomParams);

      Debug.Log($"Exported spectator assets to {assetPath}");
    }

    private static void RunExe(string path, bool keepWindowOpen = false) {
      Debug.Log($"Start running '{path}'");

      var startInfo = new System.Diagnostics.ProcessStartInfo() { WorkingDirectory = Path.GetDirectoryName(path) };
      if (keepWindowOpen) {
        startInfo.FileName = "cmd.exe";
        startInfo.Arguments = $"/K {path}";
      } else {
        startInfo.FileName = path;
      }
      
      var process = new System.Diagnostics.Process { StartInfo = startInfo };
      process.Start();
    }

    private static bool RunBuild(string projectPath) {
      Debug.Log($"Start building '{projectPath}'");

      // If compilation fails try to replace $(SolutionDir) in quantum.code.csproj PostBuildEvent and replace it by $(ProjectDir)..\
      var startInfo = new System.Diagnostics.ProcessStartInfo() {
        WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden,
        CreateNoWindow = true,
        FileName = "cmd.exe",
        Arguments = $"/c dotnet build {projectPath} --no-dependencies",
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false
      };

      var process = new System.Diagnostics.Process {
        StartInfo = startInfo
      };

      process.Start();

      string output = process.StandardOutput.ReadToEnd();

      process.WaitForExit();
      process.Close();

      if (output.Contains(" error ")) {
        Debug.LogError(output);
        return false;
      } else {
        Debug.Log(output);
        return true;
      }
    }

    public class MapSelectionWindows : EditorWindow {
      private static MapAsset[] _maps;
      private static string[] _options;
      private static int _selectedMap;
      private static Action<AssetGuid> _callback;

      public static void Init(Action<AssetGuid> callback, MapAsset[] maps) {
        _maps = maps; 
        _options = _maps.Select(m => m.name).ToArray();
        _callback = callback;

        if (long.TryParse(UIRoom.LastMapSelected, out var defaultMapGuid)) {
          _selectedMap = Array.FindIndex(_maps, m => m.Settings.Guid == defaultMapGuid);
          _selectedMap = Math.Max(0, _selectedMap);
        }

        var window = CreateInstance<MapSelectionWindows>();
        window.position = new Rect(Screen.width / 2, Screen.height / 2, 250, 90);
        window.titleContent = new GUIContent("Select Map");
        window.ShowPopup();
      }

      void OnLostFocus() {
        Close();
      }

      void OnGUI() {
        var style = new GUIStyle(EditorStyles.label);
        style.alignment = TextAnchor.MiddleCenter;
        style.fontStyle = FontStyle.Bold;
        EditorGUILayout.LabelField("Select Map", style);

        GUILayout.Space(10);

        _selectedMap = EditorGUILayout.Popup(_selectedMap, _options);

        GUILayout.Space(10);

        using (new EditorGUILayout.HorizontalScope()) {
          if (GUILayout.Button("Cancel")) {
            Close();
          }
          GUILayout.Space(10);
          if (GUILayout.Button("Ok")) {
            Close();
            _callback?.Invoke(_maps[_selectedMap].Settings.Guid);
          }
        }
      }
    }
  }
}