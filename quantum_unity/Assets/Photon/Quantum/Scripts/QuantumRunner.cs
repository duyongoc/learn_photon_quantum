using Photon.Deterministic;
using Quantum;
using Quantum.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using Photon.Realtime;
using UnityEngine;

public sealed class QuantumRunner : MonoBehaviour, IDisposable {
  public static QuantumRunner Default => _activeRunners.Count == 0 ? null : _activeRunners[0];
  public static IEnumerable<QuantumRunner> ActiveRunners => _activeRunners;
  private static List<QuantumRunner> _activeRunners = new List<QuantumRunner>();

  /// <summary>
  ///  Use this prevent the session from being update automatically in order to call Session.Update(DeltaTime) in your own code.
  ///  For example to inject custom delta time values.
  /// </summary>
  [HideInInspector]
  public bool OverrideUpdateSession = false;

  /// <summary>
  /// Quantum start game parameters.
  /// </summary>
  public struct StartParameters {
    /// <summary>
    /// The runtime config the Quantum game should use. Every client needs to set it, the server selects the first one send to it.
    /// </summary>
    public RuntimeConfig RuntimeConfig;
    /// <summary>
    /// The deterministic config the Quantum game should use. Every client needs to set it, the server selects the first one send to it.
    /// </summary>
    public DeterministicSessionConfig DeterministicConfig;
    /// <summary>
    /// The replay provider injects recorded inputs and rpcs into the game which is required to run the game as a replay. <see cref="InputProvider"/> is an implementation of the replay provider. See useages of <see cref="QuantumGame.RecordedInputs"/> and <see cref="QuantumRunnerLocalReplay.InputProvider"/>.
    /// </summary>
    public IDeterministicReplayProvider ReplayProvider;
    /// <summary>
    /// The game mode (default is Multiplayer). 
    /// Local mode is for testing only, the simulation is not connected online. It does not go into prediction nor does it perform rollbacks.
    /// Replay mode will also run offline and requires the ReplayProvider to be set to process the input.
    /// Spectating mode will run the simulation without a player and without the ability to input.
    /// </summary>
    public DeterministicGameMode GameMode;
    /// <summary>
    /// The initial tick to start the simulation from as set in FrameData (only set this when FrameData is set as well). The initial frame is also encoded in the data, but required deserilization first.
    /// </summary>
    public Int32 InitialFrame;
    /// <summary>
    /// Serialized frame to start the simulation from. Requires InitialFrame to be set as well. This can be a reconnect or an instant replay where we already have a frame snapshot locally (<see cref="QuantumInstantReplay"/>).
    /// </summary>
    public Byte[] FrameData;
    /// <summary>
    /// Optionally name the runner to access it from <see cref="QuantumRunner.FindRunner(string)"/>. This is useful when multiple runners are active on the client (for example an instant replay).
    /// </summary>
    public string RunnerId;
    /// <summary>
    /// Optionally set the quit behaviour in <see cref="QuantumNetworkCommunicator"/> to choose what is done automatically when the game is destroyed.
    /// </summary>
    public QuantumNetworkCommunicator.QuitBehaviour QuitBehaviour;
    /// <summary>
    /// The player count for the game. Requires to be set and the value will be written into the determinstic config that is send to the server.
    /// </summary>
    public Int32 PlayerCount;
    /// <summary>
    /// The local player count. Normally you set this to 1. Requires to be 0 when game mode Spectating is used.
    /// </summary>
    public Int32 LocalPlayerCount;
    /// <summary>
    /// The recording flags will enable the recording of input and checksums (requires memory and allocations). When enabled <see cref="QuantumGame.GetRecordedReplay"/> can be used access the replay data.
    /// </summary>
    public RecordingFlags RecordingFlags;
    /// <summary>
    /// The LoadBalancingClient object needs to be connected to game sever (joined a room) when handed to Quantum. Is not required for Replay or Local game modes.
    /// </summary>
    public LoadBalancingClient NetworkClient;
    /// <summary>
    /// Optionally override the resource manager for example from deserialized Quantum assets (as showcased in <see cref="QuantumRunnerLocalReplay"/>).
    /// </summary>
    public IResourceManager ResourceManagerOverride;
    /// <summary>
    /// The instant replay feature requires this setup data for snapshot recording.
    /// </summary>
    public InstantReplaySettings InstantReplayConfig;
    /// <summary>
    ///  Extra heaps to allocate for a session in case you need to create 'auxiliary' frames than actually required for the simulation itself.
    /// </summary>
    public Int32 HeapExtraCount;
    /// <summary>
    /// Optionally provide assest to be added to the dynamic asset db. This can be used to introduce procedurally generated assets into the simulation from the start.
    /// </summary>
    public DynamicAssetDB InitialDynamicAssets;
    /// <summary>
    /// Set this to true when rejoining a game to make sure that a snapshot is requested.
    /// </summary>
    [Obsolete("IsRejoin is not used anymore")]
    public bool IsRejoin;
    /// <summary>
    /// Optionally set a timeout that will enable <see cref="QuantumRunner.HasGameStartTimedOut"/> to check for a timeout during reconnecting into a running game and waiting for a snapshot that never arrives. The checking and handling needs to be done by you.
    /// </summary>
    public float StartGameTimeoutInSeconds;
  }

  public QuantumGame          Game            { get; private set; }
  public DeterministicSession Session         { get; private set; }
  public string               Id              { get; private set; }
  public SimulationUpdateTime DeltaTimeType   { get; set; }
  public LoadBalancingClient  NetworkClient   { get; private set; }
  public RecordingFlags       RecordingFlags  { get; private set; }

  public bool HideGizmos = false;
  public QuantumEditorSettings GizmoSettings;

  public bool IsRunning => Game?.Frames.Predicted != null;

  public bool HasGameStartTimedOut => _startGameTimeout > 0.0f && Session != null && Session.IsPaused && Time.time > _startGameTimeout;

  public float? DeltaTime {
    get {
      switch (DeltaTimeType) {
        case SimulationUpdateTime.EngineDeltaTime:         return Time.deltaTime;
        case SimulationUpdateTime.EngineUnscaledDeltaTime: return Time.unscaledDeltaTime;
      }

      return null;
    }
  }

  private bool _shutdownRequested;
  private float _startGameTimeout;

  void Update() {
    if (Session != null && OverrideUpdateSession == false) {
      Session.Update(DeltaTime);
      UnityDB.Update();
    }

    if (_shutdownRequested) {
      _shutdownRequested = false;
      Shutdown();
    }
  }

  void OnDisable() {
    if (Session != null) {
      Session.Destroy();
      Session = null;
      Game    = null;
    }
  }

  void OnDrawGizmos() {
#if UNITY_EDITOR
    if (Session != null && HideGizmos == false) {
      var game = Session.Game as QuantumGame;
      if (game != null) {
        QuantumGameGizmos.OnDrawGizmos(game, GizmoSettings);
      }
    }
#endif
  }

  public void Shutdown() {
    // Runner is shut down, destroys its gameobject, will trigger OnDisable() in next frame, will destroy the session, session will call dispose on the runner.
    Destroy(gameObject);
  }

  public void Dispose() {
    // Called by the Session.Destroy().
    _activeRunners.Remove(this);
  }

  public static void Init(Boolean force = false) {
    // verify using Unity unsafe utils
    MemoryLayoutVerifier.Platform = new QuantumUnityMemoryLayoutVerifierPlatform();

    // set native platform
    Photon.Deterministic.Native.Utils = new QuantumUnityNativeUtility();

    // load lookup table
    FPMathUtils.LoadLookupTables(force);

    // init profiler
    Quantum.Profiling.HostProfiler.Init(x => UnityEngine.Profiling.Profiler.BeginSample(x),
                  () => UnityEngine.Profiling.Profiler.EndSample());

    // init thread profiling (2019.x and up)
    Quantum.Profiling.HostProfiler.InitThread((a, b) => UnityEngine.Profiling.Profiler.BeginThreadProfiling(a, b),
                                          () => UnityEngine.Profiling.Profiler.EndThreadProfiling());

    // init debug draw functions
    Draw.Init(DebugDraw.Ray, DebugDraw.Line, DebugDraw.Circle, DebugDraw.Sphere, DebugDraw.Rectangle, DebugDraw.Box, DebugDraw.Clear);

    // init quantum logger
    Log.Init(Debug.Log, Debug.LogWarning, Debug.LogError, Debug.LogException);

    // init photon logger
    DeterministicLog.Init(Debug.Log, Debug.LogWarning, Debug.LogError, Debug.LogException);
  }

  public static QuantumRunner StartGame(String clientId, StartParameters param) {
    Log.Info("Starting Game");

    // set a default runner id if none is given
    if (param.RunnerId == null) {
      param.RunnerId = "DEFAULT";
    }

    if (param.FrameData?.Length > 0 && (param.InitialDynamicAssets?.IsEmpty == false)) {
      Log.Warn(
        $"Both {nameof(StartParameters.FrameData)} and {nameof(StartParameters.InitialDynamicAssets)} are set " +
        $"and not empty. Serialized frames already contain a copy of DynamicAssetDB and that copy will be used " +
        $"instead of {nameof(StartParameters.InitialDynamicAssets)}");
    }

    // init debug
    Init();

    // Make sure the runtime config has a simulation config set.
    if (param.RuntimeConfig.SimulationConfig.Id == 0) {
      param.RuntimeConfig.SimulationConfig.Id = SimulationConfig.DEFAULT_ID;
    }

    IResourceManager resourceManager = param.ResourceManagerOverride ?? UnityDB.DefaultResourceManager;

    var simulationConfig = (SimulationConfig)resourceManager.GetAsset(param.RuntimeConfig.SimulationConfig.Id);

#if UNITY_WEBGL
    if (simulationConfig.ThreadCount > 1) {
      var msg = String.Format("Multithreading is not supported if WebGL is a targeted platform. Please set your SimulationConfig.ThreadCount to 1 (currently set to {0}).", simulationConfig.ThreadCount);
#if UNITY_EDITOR
      Log.Warn(msg);
#else
      Log.Error(msg);
      throw new Exception(msg);
#endif
    }
#endif
    
    if (param.GameMode == DeterministicGameMode.Multiplayer) {
      if (param.NetworkClient == null) {
        throw new Exception("Requires a NetworkClient to start multiplayer mode");
      }

      if (param.NetworkClient.IsConnected == false) {
        throw new Exception("Not connected to photon");
      }

      if (param.NetworkClient.InRoom == false) {
        throw new Exception("Can't start networked game when not in a room");
      }
    }

    // Make copy of deterministic config here, because we write to it
    var deterministicConfig = DeterministicSessionConfig.FromByteArray(DeterministicSessionConfig.ToByteArray(param.DeterministicConfig));
    deterministicConfig.PlayerCount = param.PlayerCount;

    // Create the runner
    var runner = CreateInstance(param.RunnerId);
    runner.Id             = param.RunnerId;
    runner.DeltaTimeType  = simulationConfig.DeltaTimeType;
    runner.NetworkClient  = param.NetworkClient;
    runner.RecordingFlags = param.RecordingFlags;
    // Create the game
    runner.Game = new QuantumGame(new QuantumGame.StartParameters() {
      ResourceManager       = resourceManager, 
      AssetSerializer       = new QuantumUnityJsonSerializer(), 
      CallbackDispatcher    = QuantumCallback.Dispatcher,
      EventDispatcher       = QuantumEvent.Dispatcher,
      InstantReplaySettings = param.InstantReplayConfig,
      HeapExtraCount        = param.HeapExtraCount,
      InitialDynamicAssets  = param.InitialDynamicAssets,
    });

    // new "local mode" runs as "replay" (with Game providing input polling), to avoid rollbacks of the local network debugger.
    // old Local mode can still be used for debug purposes (but RunnerLocalDebug now uses replay mode).
    // if (param.LocalInputProvider == null && param.GameMode == DeterministicGameMode.Local)
    //   param.LocalInputProvider = runner.Game;

    DeterministicPlatformInfo info = CreatePlatformInfo();

    DeterministicSessionArgs args;
    args.Mode          = param.GameMode;
    args.RuntimeConfig = RuntimeConfig.ToByteArray(param.RuntimeConfig);
    args.SessionConfig = deterministicConfig;
    args.Game          = runner.Game;
    args.Communicator  = CreateCommunicator(param.GameMode, param.NetworkClient, param.QuitBehaviour);
    args.Replay        = param.ReplayProvider;
    args.InitialTick   = param.InitialFrame;
    args.FrameData     = param.FrameData;
    args.PlatformInfo  = info;

    // Create the session
    try {
      runner.Session = new DeterministicSession(args);
    } catch (Exception e) {
      Debug.LogException(e);
      runner.Dispose();
      return null;
    }

    // For convenience, to be able to access the runner by the session.
    runner.Session.Runner = runner;

    // Join local players
    runner.Session.Join(clientId, Math.Max(0, param.LocalPlayerCount));

#if QUANTUM_REMOTE_PROFILER
    if (!Application.isEditor) {
      var client = new QuantumProfilingClient(clientId, deterministicConfig, info);
      runner.Game.ProfilerSampleGenerated += (sample) => {
        client.SendProfilingData(sample);
        client.Update();
      };
    }
#endif

    runner._startGameTimeout = 0.0f;
    if (param.StartGameTimeoutInSeconds > 0) {
      runner._startGameTimeout = Time.time + param.StartGameTimeoutInSeconds;
    }

    return runner;
  }

  public static DeterministicPlatformInfo CreatePlatformInfo() {
    DeterministicPlatformInfo info;
    info            = new DeterministicPlatformInfo();
    info.Allocator  = new QuantumUnityNativeAllocator();
    info.TaskRunner = QuantumTaskRunnerJobs.GetInstance();

#if UNITY_EDITOR
    info.Runtime      = DeterministicPlatformInfo.Runtimes.Mono;
    info.RuntimeHost  = DeterministicPlatformInfo.RuntimeHosts.UnityEditor;
    info.Architecture = DeterministicPlatformInfo.Architectures.x86;
#if UNITY_EDITOR_WIN
    info.Platform = DeterministicPlatformInfo.Platforms.Windows;
#elif UNITY_EDITOR_OSX
    info.Platform = DeterministicPlatformInfo.Platforms.OSX;
#endif
#else
    info.RuntimeHost = DeterministicPlatformInfo.RuntimeHosts.Unity;
#if ENABLE_IL2CPP
    info.Runtime = DeterministicPlatformInfo.Runtimes.IL2CPP;
#else
    info.Runtime = DeterministicPlatformInfo.Runtimes.Mono;
#endif
#if UNITY_STANDALONE_WIN
    info.Platform = DeterministicPlatformInfo.Platforms.Windows;
#elif UNITY_STANDALONE_OSX
    info.Platform = DeterministicPlatformInfo.Platforms.OSX;
#elif UNITY_STANDALONE_LINUX
    info.Platform = DeterministicPlatformInfo.Platforms.Linux;
#elif UNITY_IOS
    info.Platform = DeterministicPlatformInfo.Platforms.IOS;
#elif UNITY_ANDROID
    info.Platform = DeterministicPlatformInfo.Platforms.Android;
#elif UNITY_TVOS
    info.Platform = DeterministicPlatformInfo.Platforms.TVOS;
#elif UNITY_XBOXONE
    info.Platform = DeterministicPlatformInfo.Platforms.XboxOne;
#elif UNITY_PS4
    info.Platform = DeterministicPlatformInfo.Platforms.PlayStation4;
#elif UNITY_SWITCH
    info.Platform = DeterministicPlatformInfo.Platforms.Switch;
#endif
#endif
    return info;
  }

  /// <summary>
  /// This cannot be called during the execution of Runner.Update() and Session.Update() methods.
  /// For this immediate needs to be false, which waits until the main thread is outside of Session.Update() to continue the shutdown of all runners.
  /// </summary>
  /// <param name="immediate">Destroy the sessions immediately or wait to Session.Update to complete.</param>
  /// <returns>At least on runner is active and will shut down.</returns>
  public static bool ShutdownAll(bool immediate = false) {
    var result = _activeRunners.Count > 0;
    if (immediate) {
      while (_activeRunners.Count > 0) {
        _activeRunners.Last().Shutdown();
      }
    }
    else {
      foreach (var runner in _activeRunners)
        runner._shutdownRequested = true;
    }

    return result;
  }

  public static QuantumRunner FindRunner(string id) {
    foreach (var runner in _activeRunners) {
      if (runner.Id == id)
        return runner;
    }
    return null;
  }

  public static QuantumRunner FindRunner(IDeterministicGame game) {
    foreach (var runner in _activeRunners) {
      if (runner.Game == game)
        return runner;
    }
    return null;
  }

  [Obsolete("Use FindRunner")]
  internal static QuantumRunner FindRunnerForGame(IDeterministicGame game) => FindRunner(game);

  private static QuantumNetworkCommunicator CreateCommunicator(DeterministicGameMode mode, LoadBalancingClient networkClient, QuantumNetworkCommunicator.QuitBehaviour quitBehaviour) {
    if (mode != DeterministicGameMode.Multiplayer && mode != DeterministicGameMode.Spectating) {
      return null;
    }

    return new QuantumNetworkCommunicator(networkClient, quitBehaviour);
  }

  static QuantumRunner CreateInstance(string name) {
    GameObject go = new GameObject($"QuantumRunner ({name})");
    var runner = go.AddComponent<QuantumRunner>();

    runner._shutdownRequested = false;

    _activeRunners.Add(runner);

    DontDestroyOnLoad(go);

    return runner;
  }
}
