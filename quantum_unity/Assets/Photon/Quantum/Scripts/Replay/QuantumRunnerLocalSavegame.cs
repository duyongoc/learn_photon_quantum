using System;
using Quantum;
using UnityEngine;

public class QuantumRunnerLocalSavegame : MonoBehaviour {
  public TextAsset SavegameFile;
  public TextAsset DatabaseFile;
  public string DatabasePath;
  public InstantReplaySettings InstantReplayConfig = InstantReplaySettings.Default;
  private IResourceManager _resourceManager;

  public void Start() {
    if (QuantumRunner.Default != null)
      return;

    if (SavegameFile == null) {
      Debug.LogError("QuantumRunnerLocalSavegame - not savegame file selected.");
      return;
    }

    Debug.Log("### Starting quantum in local savegame mode ###");

    // Load replay file in json or bson
    var serializer = new QuantumUnityJsonSerializer();
    var replayFile = serializer.DeserializeReplay(SavegameFile.bytes);

    var param = new QuantumRunner.StartParameters {
      RuntimeConfig = replayFile.RuntimeConfig,
      DeterministicConfig = replayFile.DeterministicConfig,
      GameMode = Photon.Deterministic.DeterministicGameMode.Local,
      FrameData = replayFile.Frame,
      InitialFrame = replayFile.Length,
      RunnerId = "LOCALSAVEGAME",
      PlayerCount = replayFile.DeterministicConfig.PlayerCount,
      LocalPlayerCount = replayFile.DeterministicConfig.PlayerCount,
      InstantReplayConfig = InstantReplayConfig,
    };

    if (DatabaseFile != null) {
      // This is potentially breaking, as it introduces UnityDB-ResourceManager duality
      var assets = serializer.DeserializeAssets(DatabaseFile.bytes);
      _resourceManager = new ResourceManagerStatic(assets, new QuantumUnityNativeAllocator());
      param.ResourceManagerOverride = _resourceManager;
    }

    QuantumRunner.StartGame("LOCALSAVEGAME", param);
  }

  private void OnDestroy() {
    _resourceManager?.Dispose();
    _resourceManager = null;
  }
}