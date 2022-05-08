using Photon.Realtime;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace PhotonRealtimeAsync {
  public class OperationHandler : IConnectionCallbacks, IMatchmakingCallbacks {
    public PhotonConnectionCallbacks ConnectionCallbacks = new PhotonConnectionCallbacks();
    public PhotonMatchmakingCallbacks MatchmakingCallbacks = new PhotonMatchmakingCallbacks();

    private bool _throwOnErrors;
    private TaskCompletionSource<short> _result;
    private CancellationTokenSource _cancellation;

    public Task<short> Task => _result.Task;
    public TaskCompletionSource<short> CompletionSource => _result;
    public CancellationToken Token => _cancellation.Token;
    public bool IsCancellationRequested => _cancellation.IsCancellationRequested;

    public OperationHandler(bool throwOnErrors = true) {
      _result = new TaskCompletionSource<short>();
      _cancellation  = new CancellationTokenSource(TimeSpan.FromSeconds(Globals.OperationTimeoutSec));
      _cancellation.Token.Register(() => SetException(new OperationTimeoutException("Operation timed out")));
      _throwOnErrors = throwOnErrors;
    }

    public void SetResult(short result) {
      if (_result.TrySetResult(result)) {
        if (_cancellation.IsCancellationRequested == false) {
          _cancellation.Cancel();
        }
        
        _cancellation.Dispose();
      }
    }

    public void SetException(Exception e) {
      if (_result.TrySetException(e)) {
        if (_cancellation.IsCancellationRequested == false) {
          _cancellation.Cancel();
        }

        _cancellation.Dispose();
      }
    }

    #region ConnectionCallbacks

    public void OnConnected() {
      ConnectionCallbacks.ConnectedToNameServer?.Invoke();
    }

    public void OnConnectedToMaster() {
      if (ConnectionCallbacks.ConnectedToMaster != null) {
        ConnectionCallbacks.ConnectedToMaster.Invoke();
      } else {
        SetResult(ErrorCode.Ok);
      }
    }

    public void OnCustomAuthenticationFailed(string debugMessage) {
      if (ConnectionCallbacks.CustomAuthenticationFailed != null) {
        ConnectionCallbacks.CustomAuthenticationFailed.Invoke(debugMessage);
      } else {
        SetException(new AuthenticationFailedException(debugMessage));
      }
    }

    public void OnCustomAuthenticationResponse(Dictionary<string, object> data) {
      ConnectionCallbacks.CustomAuthenticationResponse?.Invoke(data);
    }

    public void OnDisconnected(DisconnectCause cause) {
      if (ConnectionCallbacks.Disconnected != null) {
        ConnectionCallbacks.Disconnected.Invoke(cause);
      } else {
        SetException(new DisconnectException(cause));
      }
    }

    public void OnRegionListReceived(RegionHandler regionHandler) {
      ConnectionCallbacks.RegionListReceived?.Invoke(regionHandler);
    }

    #endregion

    #region MatchmakingCallbacks

    public void OnCreatedRoom() {
      MatchmakingCallbacks.CreatedRoom?.Invoke();
    }

    public void OnCreateRoomFailed(short returnCode, string message) {
      if (MatchmakingCallbacks.CreateRoomFailed != null) {
        MatchmakingCallbacks.CreateRoomFailed.Invoke(returnCode, message);
      } else {
        if (_throwOnErrors) {
          SetException(new OperationException(returnCode, message));
        } else {
          Log.Error(message);
          SetResult(returnCode);
        };
      }
    }

    public void OnFriendListUpdate(List<FriendInfo> friendList) {
      MatchmakingCallbacks.FriendListUpdate?.Invoke(friendList);
    }

    public void OnJoinedRoom() {
      if (MatchmakingCallbacks.JoinedRoom != null) {
        MatchmakingCallbacks.JoinedRoom.Invoke();
      } else {
        SetResult(ErrorCode.Ok);
      }
    }

    public void OnJoinRandomFailed(short returnCode, string message) {
      if (MatchmakingCallbacks.JoinRoomRandomFailed != null) {
        MatchmakingCallbacks.JoinRoomRandomFailed.Invoke(returnCode, message);
      } else {
        if (_throwOnErrors) {
          SetException(new OperationException(returnCode, message));
        } else {
          Log.Error(message);
          SetResult(returnCode);
        };
      }
    }

    public void OnJoinRoomFailed(short returnCode, string message) {
      if (MatchmakingCallbacks.JoinRoomFailed != null) {
        MatchmakingCallbacks.JoinRoomFailed.Invoke(returnCode, message);
      }
      else {
        if (_throwOnErrors) {
          SetException(new OperationException(returnCode, message));
        } else {
          Log.Error(message);
          SetResult(returnCode);
        };
      }
    }

    public void OnLeftRoom() {
    }

    #endregion
  }
}
