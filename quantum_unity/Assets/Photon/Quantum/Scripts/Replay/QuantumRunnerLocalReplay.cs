using System;
using Quantum;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class QuantumRunnerLocalReplay : MonoBehaviour {
  public TextAsset ReplayFile;
  public TextAsset DatabaseFile;
  public string DatabasePath;
  public TextAsset ChecksumFile;
  public float SimulationSpeedMultiplier = 1.0f;
  public bool ShowReplayLabel;

  public InstantReplaySettings InstantReplayConfig = InstantReplaySettings.Default;

  QuantumRunner _runner;
  IResourceManager _resourceManager;

  private static InputProvider InputProvider;
  

  void Start() {
    if (QuantumRunner.Default != null)
      return;

    if (ReplayFile == null) {
      Debug.LogError("QuantumRunnerLocalReplay - not replay file selected.");
      return;
    }

    Debug.Log("### Starting quantum in local replay mode ###");

    var serializer = new QuantumUnityJsonSerializer();
    var replayFile = serializer.DeserializeReplay(ReplayFile.bytes);

    // Create a new input provider from the replay file
    InputProvider = new InputProvider(replayFile.InputHistory);

    var param = new QuantumRunner.StartParameters {
      RuntimeConfig = replayFile.RuntimeConfig,
      DeterministicConfig = replayFile.DeterministicConfig,
      ReplayProvider = InputProvider,
      GameMode = Photon.Deterministic.DeterministicGameMode.Replay,
      RunnerId = "LOCALREPLAY",
      PlayerCount = replayFile.DeterministicConfig.PlayerCount,
      LocalPlayerCount = replayFile.DeterministicConfig.PlayerCount,
      InstantReplayConfig = InstantReplayConfig,
      InitialFrame = replayFile.InitialFrame,
      FrameData = replayFile.InitialFrameData,
    };

    if (DatabaseFile != null) {
      // This is potentially breaking, as it introduces UnityDB-ResourceManager duality
      var assets = serializer.DeserializeAssets(DatabaseFile.bytes);
      _resourceManager = new ResourceManagerStatic(assets, new QuantumUnityNativeAllocator());
      param.ResourceManagerOverride = _resourceManager;
    }

    _runner = QuantumRunner.StartGame("LOCALREPLAY", param);

    if (ChecksumFile != null) {
      var checksumFile = serializer.DeserializeChecksum(ChecksumFile.bytes);
      _runner.Game.StartVerifyingChecksums(checksumFile);
    }
  }

  public void Update() {
    if (QuantumRunner.Default != null && QuantumRunner.Default.Session != null) {
      // Set the session ticking to manual to inject custom delta time.
      QuantumRunner.Default.OverrideUpdateSession = SimulationSpeedMultiplier != 1.0f;
      if (QuantumRunner.Default.OverrideUpdateSession) {
        var deltaTime = QuantumRunner.Default.DeltaTime;
        if (deltaTime == null) {
          // DeltaTime can be null if we selected Quantum internal stopwatch. Use unscaled Unity time instead.
          deltaTime = Time.unscaledDeltaTime;
        }
        QuantumRunner.Default.Session.Update(deltaTime * SimulationSpeedMultiplier);
        UnityDB.Update();
      }
    }

#if UNITY_EDITOR
    if (InputProvider != null && _runner.Session.IsReplayFinished == true) {
      EditorApplication.isPaused = true;
    }
#endif
  }

  private void OnDestroy() {
    _resourceManager?.Dispose();
    _resourceManager = null;
  }

#if UNITY_EDITOR
  private float guiTimer;

  void OnGUI() {
    if (ShowReplayLabel && InputProvider != null) {
      if ( _runner.Session.IsReplayFinished) {
        GUI.contentColor = Color.red;
        GUI.Label(new Rect(10, 10, 200, 100), "REPLAY COMPLETED");
      }
      else {
        guiTimer += Time.deltaTime;
        if (guiTimer % 2.0f > 1.0f) {
          GUI.contentColor = Color.red;
          GUI.Label(new Rect(10, 10, 200, 100), "REPLAY PLAYING");
        }
      }
    }
  }
#endif
}
