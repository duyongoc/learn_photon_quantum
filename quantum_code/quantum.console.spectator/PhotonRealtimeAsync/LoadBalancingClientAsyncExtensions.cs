using Photon.Realtime;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace PhotonRealtimeAsync {
  public static class LoadBalancingClientAsyncExtensions {
    /// <summary>
    /// Connect to master server.
    /// </summary>
    /// <param name="client">Client</param>
    /// <param name="appSettings">App settings</param>
    /// <param name="createServiceTask">Runs client.Service() during the operation</param>
    /// <returns>When connected to master server callback was called.</returns>
    /// <exception cref="DisconnectException">Is thrown when the connection terminated</exception>
    /// <exception cref="AuthenticationFailedException">Is thrown when the authentication failed</exception>
    /// <exception cref="OperationStartException">Is thrown when the operation could not be started</exception>
    /// <exception cref="OperationException">Is thrown when the operation completed unsuccesfully</exception>
    /// <exception cref="OperationTimeoutException">Is thrown when the operation timed out</exception>
    public static Task ConnectUsingSettingsAsync(this LoadBalancingClient client, AppSettings appSettings, bool createServiceTask = true) {
      if (client.State != ClientState.Disconnected && client.State != ClientState.PeerCreated) {
        return Task.FromException(new OperationStartException("Client still connected"));
      }

      if (client.ConnectUsingSettings(appSettings) == false) {
        return Task.FromException(new OperationStartException("Failed to start connecting"));
      }

      return client.CreateConnectionHandler(true, createServiceTask).Task;
    }

    /// <summary>
    /// Runs reconnect and rejoin.
    /// </summary>
    /// <param name="client">Client object</param>
    /// <param name="createServiceTask">Runs client.Service() during the operation</param>
    /// <returns>Returns when inside the room</returns>
    /// <exception cref="DisconnectException">Is thrown when the connection terminated</exception>
    /// <exception cref="OperationStartException">Is thrown when the operation could not be started</exception>
    /// <exception cref="OperationException">Is thrown when the operation completed unsuccesfully</exception>
    /// <exception cref="OperationTimeoutException">Is thrown when the operation timed out</exception>
    public static Task ReconnectAndRejoinAsync(this LoadBalancingClient client, bool throwOnError = true, bool createServiceTask = true) {
      if (client.State != ClientState.Disconnected) {
        return Task.FromException(new OperationStartException("Client still connected"));
      }

      if (client.ReconnectAndRejoin() == false) {
        return Task.FromException(new OperationStartException("Failed to start reconnecting"));
      }

      return client.CreateConnectionHandler(throwOnError, createServiceTask).Task;
    }

    /// <summary>
    /// Disconnects the client.
    /// </summary>
    /// <param name="client">Client.</param>
    /// <param name="createServiceTask">Runs client.Service() during the operation</param>
    /// <returns>Returns when the client has successfully disconnected</returns>
    /// <exception cref="DisconnectException">Is thrown when the connection terminated</exception>
    /// <exception cref="OperationStartException">Is thrown when the operation could not be started</exception>
    /// <exception cref="OperationException">Is thrown when the operation completed unsuccesfully</exception>
    /// <exception cref="OperationTimeoutException">Is thrown when the operation timed out</exception>
    public static Task DisconnectAsync(this LoadBalancingClient client, bool createServiceTask = true) {
      if (client == null) {
        return Task.CompletedTask;
      }

      if (client.State == ClientState.Disconnected || client.State == ClientState.PeerCreated) {
        return Task.CompletedTask;
      }

      var handler = client.CreateConnectionHandler(true, createServiceTask);

      handler.ConnectionCallbacks.Disconnected += (cause) => {
        Log.Info($"Disconnected: {cause}");
        handler.SetResult(ErrorCode.Ok);
      };

      if (client.State != ClientState.Disconnecting) {
        client.Disconnect();
      } 

      return handler.Task;
    }

    /// <summary>
    /// Create and join a room.
    /// </summary>
    /// <param name="client">Client object</param>
    /// <param name="enterRoomParams">Enter room params</param>
    /// <param name="throwOnError">Set ErrorCode as result on RoomCreateFailed or RoomJoinFailed</param>
    /// <param name="createServiceTask">Runs client.Service() during the operation</param>
    /// <returns>When the room has been entered</returns>
    /// <exception cref="DisconnectException">Is thrown when the connection terminated</exception>
    /// <exception cref="OperationStartException">Is thrown when the operation could not be started</exception>
    /// <exception cref="OperationException">Is thrown when the operation completed unsuccesfully</exception>
    /// <exception cref="OperationTimeoutException">Is thrown when the operation timed out</exception>
    public static Task<short> CreateAndJoinRoomAsync(this LoadBalancingClient client, EnterRoomParams enterRoomParams, bool throwOnError = true, bool createServiceTask = true) {
      if (client.OpCreateRoom(enterRoomParams) == false) {
        return Task.FromException<short>(new OperationStartException("Failed to send CreateRoom operation"));
      }

      return client.CreateConnectionHandler(throwOnError, createServiceTask).Task;
    }

    /// <summary>
    /// Join room.
    /// </summary>
    /// <param name="client">Client object</param>
    /// <param name="enterRoomParams">Enter room params</param>
    /// <param name="throwOnError">Set ErrorCode as result when JoinRoomFailed</param>
    /// <param name="createServiceTask">Runs client.Service() during the operation</param>
    /// <returns>When room has been entered</returns>
    /// <exception cref="DisconnectException">Is thrown when the connection terminated</exception>
    /// <exception cref="OperationStartException">Is thrown when the operation could not be started</exception>
    /// <exception cref="OperationException">Is thrown when the operation completed unsuccesfully</exception>
    /// <exception cref="OperationTimeoutException">Is thrown when the operation timed out</exception>
    public static Task<short> JoinRoomAsync(this LoadBalancingClient client, EnterRoomParams enterRoomParams, bool throwOnError = true, bool createServiceTask = true) {
      if (client.OpJoinRoom(enterRoomParams) == false) {
        return Task.FromException<short>(new OperationStartException("Failed to send JoinRoom operation"));
      }

      return client.CreateConnectionHandler(throwOnError, createServiceTask).Task;
    }

    /// <summary>
    /// Join random or create room
    /// </summary>
    /// <param name="client">Client object</param>
    /// <param name="joinRandomRoomParams">Join random room params</param>
    /// <param name="enterRoomParams">Enter room params</param>
    /// <param name="throwOnError">Set ErrorCode as result when operation fails with ErrorCode</param>
    /// <param name="createServiceTask">Runs client.Service() during the operation</param>
    /// <returns>When inside a room</returns>
    /// <exception cref="DisconnectException">Is thrown when the connection terminated</exception>
    /// <exception cref="OperationStartException">Is thrown when the operation could not be started</exception>
    /// <exception cref="OperationException">Is thrown when the operation completed unsuccesfully</exception>
    /// <exception cref="OperationTimeoutException">Is thrown when the operation timed out</exception>
    public static Task<short> JoinRandomOrCreateRoomAsync(this LoadBalancingClient client, OpJoinRandomRoomParams joinRandomRoomParams, EnterRoomParams enterRoomParams, bool throwOnError = true, bool createServiceTask = true) {
      if (client.OpJoinRandomOrCreateRoom(joinRandomRoomParams, enterRoomParams) == false) {
        return Task.FromException<short>(new OperationStartException("Failed to send JoinRandomOrCreateRoom operation"));
      }

      return client.CreateConnectionHandler(throwOnError, createServiceTask).Task;
    }

    /// <summary>
    /// Create a <see cref="OperationHandler"/> instance, sets up the Photon callbacks, schedules removing them, create a connection service task.
    /// The handler will monitor the Photon callbacks and complete, fault accordingly. 
    /// Use the callbacks <see cref="OperationHandler.OnCreatedRoom"/> to change the default handling.
    /// <see cref="OperationHandler.Task"/> can complete with ErrorCode.Ok, exception on errors and a timeout <see cref="OperationTimeoutException"/>.
    /// </summary>
    /// <param name="client">Client</param>
    /// <param name="throwOnErrors">The default implementation will throw an exception on every unexpected result, set this to false to return a result <see cref="ErrorCode>"/> instead</param>
    /// <param name="createServiceTask">Runs client.Service() during the operation</param>
    /// <returns>Phtoon Connection Handler object</returns>
    public static OperationHandler CreateConnectionHandler(this LoadBalancingClient client, bool throwOnErrors = true, bool createServiceTask = true) {
      var handler = new OperationHandler(throwOnErrors);

      client.AddCallbackTarget(handler);

      handler.Task.ContinueWith(t => {
        client.RemoveCallbackTarget(handler);
      }, Globals.TaskScheduler);

      if (createServiceTask) {
        CreateServiceTask(client, handler.Token, handler.CompletionSource);
      }

      return handler;
    }

    /// <summary>
    /// Starts a task that calls <see cref="LoadBalancingClient.Service()"/> every updateIntervalMs miliseconds.
    /// The task is stopped by the cancellation token from <see cref="OperationHandler.Token"/>.
    /// It will set an exception on the <see cref="OperationHandler"/> TaskCompletionSource if after the timeout it is still not completed.
    /// </summary>
    /// <param name="client">Client</param>
    /// <param name="token">Cancellation token to stop the update loop</param>
    /// <param name="completionSource">Completion source is notified on an exception in Service()</param>
    public static void CreateServiceTask(this LoadBalancingClient client, CancellationToken token, TaskCompletionSource<short> completionSource) {
      var startTime = DateTime.Now;
      Globals.TaskFactory.StartNew(async () => {
        while (token.IsCancellationRequested == false) {
          try {
            // TODO: replace by sendoutgoing verbose
            client.Service();
            await Task.Delay(Globals.ServiceIntervalMs, token);
          } catch (OperationCanceledException) {
            // cancellation was notified
            break;
          }
          catch (Exception e) {
            // exception in service, try to stop the operation handler
            completionSource.TrySetException(e);
            return;
          }
        };
      }, TaskCreationOptions.LongRunning);
    }
  }
}
