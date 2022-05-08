using Quantum;
using System;
using System.Collections;
using System.Linq;
using Photon.Deterministic;
using UnityEngine;
using UnityEngine.Events;
using System.Collections.Generic;

public class QuantumRunnerLocalDebug : QuantumCallbacks {
  public RecordingFlags  RecordingFlags = RecordingFlags.Default;

  public InstantReplaySettings InstantReplayConfig = InstantReplaySettings.Default;

  public float SimulationSpeedMultiplier = 1.0f;
  public RuntimeConfig   Config;
  public RuntimePlayer[] Players;

  public bool PreloadAddressables = false;
  public DynamicAssetDBSettings DynamicAssetDB;

  bool _isReload;

#if QUANTUM_ADDRESSABLES
  public async void Start()
#else
  public void Start()
#endif
  { 
    if (QuantumRunner.Default != null)
      return;

#if QUANTUM_ADDRESSABLES
    if (PreloadAddressables) {
      // there's also an overload that accepts a target list paramter
      var addressableAssets = UnityDB.CollectAddressableAssets();
      // preload all the addressable assets
      foreach (var (assetRef, address) in addressableAssets) {
        // there are a few ways to load an asset with Addressables (by label, by IResourceLocation, by address etc.)
        // but it seems that they're not fully interchangeable, i.e. loading by label will not make loading by address
        // be reported as done immediately; hence the only way to preload an asset for Quantum is to replicate
        // what it does internally, i.e. load with the very same parameters
        await UnityEngine.AddressableAssets.Addressables.LoadAssetAsync<AssetBase>(address).Task;
      }
    }
#endif

    StartWithFrame(0, null);
  }

  public void StartWithFrame(int frameNumber = 0, byte[] frameData = null) {
    _isReload = frameNumber > 0 && frameData != null;

    Debug.Log("### Starting quantum in local debug mode ###");

    var mapdata = FindObjectOfType<MapData>();
    if (mapdata) {
      // set map to this maps asset
      Config.Map = mapdata.Asset.Settings;

      var playerCount = Math.Max(1, Players.Length);

      var dynamicDB = new DynamicAssetDB();
      DynamicAssetDB.OnInitialDynamicAssetsRequested?.Invoke(dynamicDB);

      // create start game parameter
      var param = new QuantumRunner.StartParameters {
        RuntimeConfig        = Config,
        DeterministicConfig  = DeterministicSessionConfigAsset.Instance.Config,
        ReplayProvider       = null,
        GameMode             = Photon.Deterministic.DeterministicGameMode.Local,
        InitialFrame         = 0,
        RunnerId             = "LOCALDEBUG",
        PlayerCount          = playerCount,
        LocalPlayerCount     = playerCount,
        RecordingFlags       = RecordingFlags,
        InstantReplayConfig  = InstantReplayConfig,
        InitialDynamicAssets = dynamicDB,
      };

      param.InitialFrame = frameNumber;
      param.FrameData    = frameData;

      // start with debug config
      QuantumRunner.StartGame("LOCALDEBUG", param);
    } else {
      throw new Exception("No MapData object found, can't debug start scene");
    }
  }

  public override void OnGameStart(QuantumGame game) {
    if (_isReload == false) {
      for (Int32 i = 0; i < Players.Length; ++i) {
        game.SendPlayerData(i, Players[i]);
      }
    }
  }

  // Update is called once per frame
  public void OnGUI() {
    if (QuantumRunner.Default != null && QuantumRunner.Default.Id == "LOCALDEBUG") {
      if (GUI.Button(new Rect(Screen.width - 150, 10, 140, 25),  "Save And Reload")) {
        StartCoroutine(SaveAndReload());
      }
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
  }

  IEnumerator SaveAndReload() {
    var frameNumber = QuantumRunner.Default.Game.Frames.Verified.Number;
    var frameData = QuantumRunner.Default.Game.Frames.Verified.Serialize(DeterministicFrameSerializeMode.Blit);
    
    Log.Info($"Serialized Frame size: {frameData.Length} bytes");

    QuantumRunner.ShutdownAll();

    while (QuantumRunner.ActiveRunners.Any()) {
      yield return null;
    }

    StartWithFrame(frameNumber, frameData);
  }



  [Serializable]
  public struct DynamicAssetDBSettings {
    [Serializable]
    public class InitialDynamicAssetsRequestedUnityEvent : UnityEvent<DynamicAssetDB> { }

    public InitialDynamicAssetsRequestedUnityEvent OnInitialDynamicAssetsRequested;
  }
}