using Photon.Deterministic;
using Photon.Realtime;
using PhotonRealtimeAsync;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// This is an example of how to connect and start a spectator app and can be used as a blueprint for your own application.
/// Or be used as is and customized by overriding OnSimulationStart, OnSimulationUpdate and OnSimulationEnd.
/// The concepts are very similar to the console replay runner and the custom Quantum server plugin except that here we also maintain a Photon connection.
/// </summary>
namespace Quantum.Spectator {
  using Task = System.Threading.Tasks.Task;

  public class Spectator {
    private LocalSnapshot _localSnapshot;

    /// <summary>
    /// The simulation can stop incomplete with errors. The Run() loop will try to reconnect in those cases.
    /// </summary>
    public enum GameResult {
      Completed,
      NotStarted,
      StartGameTimedOut,
      ConnectionError
    }

    public struct StartParameter {
      /// <summary>
      /// Pass a load balancing client instance to use.
      /// </summary>
      public LoadBalancingClient Client;
      /// <summary>
      /// Use local snapshots during reconnecting (0 = disabled)
      /// </summary>
      public int LocalSnapshotTimeoutSec;
      /// <summary>
      /// Wait between connection atempts.
      /// </summary>
      public int ConnectionAttemptIntervalMs;
      /// <summary>
      /// Nummber of connection attempts
      /// </summary>
      public int ConnectionAttempts;
      /// <summary>
      /// Cancelation token to stop the spectator
      /// </summary>
      public CancellationToken CancelToken;

      /// <summary>
      /// Default settings
      /// </summary>
      public static StartParameter Default = new StartParameter {
        Client = null,
        LocalSnapshotTimeoutSec = 0,
        ConnectionAttemptIntervalMs = 5000,
        ConnectionAttempts = 5,
        CancelToken = CancellationToken.None
      };
    }

    /// <summary>
    /// Run the spectator app. Connects to Photon server and room, and start and runs the simulation.
    /// </summary>
    /// <param name="runtimeConfig">Runtime config</param>
    /// <param name="sessionConfig">Session config</param>
    /// <param name="appSettings">AppSettings, must have AppId, Fixed Region</param>
    /// <param name="enterRoomParams">Uses this to create the room, or joins a room if RoomName is set</param>
    /// <param name="lutProvider">Provides the loading of the Lut files</param>
    /// <param name="assetDBData">Asset database in binary form (as loaded from file for example)</param>
    /// <param name="startParameter">Optionally pass start parameters</param>
    /// <returns></returns>
    public async Task Run(RuntimeConfig runtimeConfig, DeterministicSessionConfig sessionConfig, AppSettings appSettings, EnterRoomParams enterRoomParams, LutProvider lutProvider, byte[] assetDBData, StartParameter startParameter) {
      int connectionAttempt = 0;

      var client = startParameter.Client;
      if (client == null) {
        client = new LoadBalancingClient(appSettings.Protocol);
      }

      var gameResult = GameResult.NotStarted;
      while (gameResult != GameResult.Completed && ++connectionAttempt <= startParameter.ConnectionAttempts && startParameter.CancelToken.IsCancellationRequested == false) {
        if (connectionAttempt > 1) {
          Log.Debug($"Waiting for {startParameter.ConnectionAttemptIntervalMs / 1000.0f} sec");
          try {
            await Task.Delay(startParameter.ConnectionAttemptIntervalMs, startParameter.CancelToken);
          } catch (TaskCanceledException) {
            break;
          }
        }

        Log.Debug($"Connecting to Photon ({connectionAttempt}/{startParameter.ConnectionAttempts})");

        if (gameResult == GameResult.NotStarted) {
          // connect to Photon
          try {
            await client.ConnectUsingSettingsAsync(appSettings);
            Log.Debug($"Connected to Photon: {client.CloudRegion}");
            if (string.IsNullOrEmpty(enterRoomParams.RoomName)) {
              await client.CreateAndJoinRoomAsync(enterRoomParams);
            } else {
              await client.JoinRoomAsync(enterRoomParams);
            }
            Log.Debug($"Joined room: {client.CurrentRoom.Name}");
          } catch (Exception e) {
            Log.Exception(e);
            try {
              await client.DisconnectAsync();
            } catch (Exception e2) {
              Log.Exception(e2);
            }
            continue;
          }
        }
        else {
          // reconnect to Photon
          try {
            await client.ReconnectAndRejoinAsync();
            Log.Debug($"Reconnected to Photon: {client.CloudRegion}, room {client.CurrentRoom.Name}");
          } catch (Exception e1) {
            Log.Exception(e1);
            try {
              await client.DisconnectAsync();
            } catch (Exception e2) {
              Log.Exception(e2);
            }
            continue;
          }
        }

        gameResult = await RunSimulation(client, runtimeConfig, sessionConfig, lutProvider, assetDBData, startParameter.LocalSnapshotTimeoutSec, startParameter.CancelToken);

        try {
          await client.DisconnectAsync();
        }
        catch (DisconnectException) {
          // we start disconnecting during Quantum shutdown and need to catch it here as it can trigger in between
        }

        Log.Info($"Game finished with result: {gameResult}");
      }

      if (gameResult == GameResult.Completed) {
        Log.Info("Completed the simulation");
      }
      else {
        Log.Error("Failed to run or complete the simulation");
      }
    }

    /// <summary>
    /// Called before the simualtion is started. Setup callback and events handlers.
    /// </summary>
    /// <param name="callbacks">Callback dispatcher</param>
    /// <param name="events">Event dispatcher</param>
    /// <returns></returns>
    protected virtual List<IDisposable> OnSimulationStart(CallbackDispatcher callbacks, EventDispatcher events) {
      var disposeList = new List<IDisposable>();

      // example how to evaluate each tick
      disposeList.Add(callbacks.SubscribeManual<CallbackSimulateFinished>(c => {
        if (c.Game.Frames.Verified.Number % 100 == 0) {
          Log.Debug($"Tick {c.Game.Frames.Verified.Number}");
        }
      }));

      return disposeList;
    }

    /// <summary>
    /// Called after the simulation has been updated. 
    /// </summary>
    /// <returns>Return false to stop the simulation gracefully</returns>
    protected virtual bool OnSimulationUpdate() {
      // stop the simulation when escape has been pressed
      if (Console.KeyAvailable && Console.ReadKey().Key == ConsoleKey.Escape) {
        return false;
      }

      return true;
    }

    /// <summary>
    /// Called when the simulation ended.
    /// </summary>
    /// <param name="gameResult">Game result</param>
    protected virtual void OnSimulationEnd(GameResult gameResult) {
    }

    /// <summary>
    /// Initializes and starts the simulation, updates it regularily and waits until it ended to return a result.
    /// The client will be ticked internally.
    /// </summary>
    /// <param name="networkClient">Realtime Load Balancing Client</param>
    /// <param name="runtimeConfig">Runtime config</param>
    /// <param name="sessionConfig">Session config</param>
    /// <param name="lutProvider">Provides the loading of the Lut files</param>
    /// <param name="assetDBData">Asset database in binary form (as loaded from file for example)</param>
    /// <returns></returns>
    protected async Task<GameResult> RunSimulation(LoadBalancingClient networkClient, RuntimeConfig runtimeConfig, DeterministicSessionConfig sessionConfig, LutProvider lutProvider, byte[] assetDBData, int localSnapshotTimeoutSec, CancellationToken cancellationToken) {
      if (FPLut.IsLoaded == false) {
        FPLut.Init(lutProvider);
        Assert.Always(FPLut.IsLoaded);
      }

      var container = new SessionContainer(sessionConfig, runtimeConfig);
      var serializer = new QuantumJsonSerializer();
      var resourceManager = new ResourceManagerStatic(serializer.DeserializeAssets(assetDBData), SessionContainer.CreateNativeAllocator());
      var callbacks = new CallbackDispatcher();
      var events = new EventDispatcher();
      var networkCommunicator = new QuantumNetworkCommunicator(networkClient, QuantumNetworkCommunicator.QuitBehaviour.Disconnect);

      container.StartSpectator(new QuantumGame.StartParameters {
        AssetSerializer = serializer,
        CallbackDispatcher = callbacks,
        EventDispatcher = events,
        ResourceManager = resourceManager
      }, networkCommunicator,
         _localSnapshot != null && _localSnapshot.IsValid ? _localSnapshot.Snapshot : null,
         _localSnapshot != null && _localSnapshot.IsValid ? _localSnapshot.Tick : 0);

      var disposeList = OnSimulationStart(callbacks, events);

      // using 100 ms fixed update interval gives a good compromise between updating the connection and progressing the simulation
      var deltaTime = 100;
      var result = GameResult.Completed;
      var sw = Stopwatch.StartNew();

      while (cancellationToken.IsCancellationRequested == false) {
        // wait 100 ms minus the time we needed to tick the simulation
        var d = (int)(deltaTime - sw.ElapsedMilliseconds);
        if (d > 0) {
          try {
            await Task.Delay(d, cancellationToken);
          }
          catch (OperationCanceledException) {
            break;
          }
        }
        sw.Restart();

        container.Service();

        if (OnSimulationUpdate() == false) {
          break;
        }

        if (container.HasGameStartTimedOut) {
          Log.Error("Game start has timed out, no client send a snapshot in time");
          result = GameResult.StartGameTimedOut;
          break;
        }

        if (networkClient.State != ClientState.Joined) {
          Log.Error($"Connection to game server lost, State: {networkClient.State} Cause: {networkClient.DisconnectedCause}");
          result = GameResult.ConnectionError;

          if (localSnapshotTimeoutSec > 0) {
            _localSnapshot = LocalSnapshot.Create(container.QuantumGame, localSnapshotTimeoutSec);
          }

          break;
        }
      }

      // clear callbacks
      if (disposeList != null) {
        foreach (var c in disposeList) {
          c.Dispose();
        }
        disposeList.Clear();
      }

      OnSimulationEnd(result);

      // this will also disconnect the photon connection (if QuitBehaviour.Disconnect)
      container.Destroy();

      // this will free allocated Quantum resources
      resourceManager.Dispose();

      return result;
    }

    private class LocalSnapshot {
      public readonly byte[] Snapshot;
      public readonly int Tick;
      private DateTime _timeout;

      public bool IsValid => DateTime.Now <= _timeout;

      public LocalSnapshot(byte[] snapshot, int tick, int timeoutInSec) {
        Snapshot = snapshot;
        Tick = tick;
        _timeout = DateTime.Now + TimeSpan.FromSeconds(timeoutInSec);
        Log.Info($"Creating local snapshot for tick {tick}");
      }

      public static LocalSnapshot Create(QuantumGame game, int timeoutInSec) {
        if (game?.Frames.Verified != null) {
          return new LocalSnapshot(
            game.Frames.Verified.Serialize(DeterministicFrameSerializeMode.Blit),
            game.Frames.Verified.Number, 
            timeoutInSec);
        }
        return null;
      }
    }
  }
}
