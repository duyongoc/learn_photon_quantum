using ExitGames.Client.Photon;
using Photon.Deterministic;
using System;
using System.Collections.Generic;
using Photon.Realtime;

namespace Quantum.Spectator {
  // This is 1 to 1 copy from the class in the Unity project
  public class QuantumNetworkCommunicator : ICommunicator {
    public enum QuitBehaviour {
      LeaveRoom,
      LeaveRoomAndBecomeInactive,
      Disconnect,
      None
    }

    public QuitBehaviour ThisQuitBehaviour;

    readonly ByteArraySlice _sendSlice = new ByteArraySlice();
    private readonly RaiseEventOptions _eventOptions;
    private readonly LoadBalancingClient _loadBalancingClient;
    private readonly Dictionary<Byte, Object> _parameters;
    private Action<EventData> _lastEventCallback;

    public Boolean IsConnected {
      get {
        return _loadBalancingClient.IsConnected;
      }
    }

    public Int32 RoundTripTime {
      get {
        return _loadBalancingClient.LoadBalancingPeer.RoundTripTime;
      }
    }

    public Byte LocalPLayerId {
      get {
        return (Byte)_loadBalancingClient.LocalPlayer.ActorNumber;
      }
    }

    internal QuantumNetworkCommunicator(LoadBalancingClient loadBalancingClient, QuitBehaviour quitBehavior) {
      ThisQuitBehaviour = quitBehavior;
      _loadBalancingClient = loadBalancingClient;
      _loadBalancingClient.LoadBalancingPeer.TimePingInterval = 50;
      _loadBalancingClient.LoadBalancingPeer.UseByteArraySlicePoolForEvents = true;

      _parameters = new Dictionary<Byte, Object>();
      _parameters[ParameterCode.ReceiverGroup] = (byte)ReceiverGroup.All;

      _eventOptions = new RaiseEventOptions();
    }

    public void DisposeEventObject(object obj) {
      if (obj is ByteArraySlice bas) {
        bas.Release();
      }
    }

    public void RaiseEvent(Byte eventCode, byte[] message, int messageLength, Boolean reliable, Int32[] toPlayers) {
      _sendSlice.Buffer = message;
      _sendSlice.Count = messageLength;
      _sendSlice.Offset = 0;
      
      _eventOptions.TargetActors = toPlayers;
      _loadBalancingClient.OpRaiseEvent(eventCode, _sendSlice, _eventOptions, reliable ? SendOptions.SendReliable : SendOptions.SendUnreliable);

      // If multiple events are send during a "frame" this only has to be called once after raising them.
      _loadBalancingClient.LoadBalancingPeer.SendOutgoingCommands();
    }

    public void AddEventListener(OnEventReceived onEventReceived) {
      RemoveEventListener();

      // save callback we know how to de-register it
      _lastEventCallback = (eventData) => {
        var bas = eventData.CustomData as ByteArraySlice;
        if (bas != null) {
          onEventReceived(eventData.Code, bas.Buffer, bas.Count, bas);
        }
      };
      
      _loadBalancingClient.EventReceived += _lastEventCallback;
    }

    public void Service() {
      // Can be optimized by splitting into receiving and sending and called from Quantum accordingly
      _loadBalancingClient.Service();
    }

    public void RemoveEventListener() {
      if (_lastEventCallback != null) {
        _loadBalancingClient.EventReceived -= _lastEventCallback;
        _lastEventCallback = null;
      }
    }

    public void OnDestroy() {
      RemoveEventListener();

      switch (ThisQuitBehaviour) {
        case QuitBehaviour.LeaveRoom:
        case QuitBehaviour.LeaveRoomAndBecomeInactive:
          if (_loadBalancingClient.State == ClientState.Joined) {
            _loadBalancingClient.OpLeaveRoom(ThisQuitBehaviour == QuitBehaviour.LeaveRoomAndBecomeInactive);
          }
          break;
        case QuitBehaviour.Disconnect:
          _loadBalancingClient.Disconnect();
          break;
      }
    }
  }
}
