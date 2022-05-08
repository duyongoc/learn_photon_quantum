using Photon.Deterministic;
using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;

#if QUANTUM_REMOTE_PROFILER
using System.Diagnostics;
using System.Net;
using Quantum;
using Quantum.Profiling;
using LiteNetLib;
using LiteNetLib.Utils;
#endif

public static class QuantumProfilingClientConstants {
  public const string DISCOVER_TOKEN = "QuantumProfiling/Discover";
  public const string DISCOVER_RESPONSE_TOKEN = "QuantumProfiling/DiscoverResponse";
  public const string CONNECT_TOKEN = "QuantumProfiling/Connect";

  public const byte ClientInfoMessage = 0;
  public const byte FrameMessage = 1;

}

[Serializable]
public class QuantumProfilingClientInfo {
  [Serializable]
  public class CustomProperty {
    public string Name;
    public string Value;
  }

  public string ProfilerId;
  public DeterministicSessionConfig Config;
  public List<CustomProperty> Properties = new List<CustomProperty>();

  public QuantumProfilingClientInfo() {
  }

  public QuantumProfilingClientInfo(string clientId, DeterministicSessionConfig config, DeterministicPlatformInfo platformInfo) {
    ProfilerId = Guid.NewGuid().ToString();
    Config = config;

    Properties.Add(CreateProperty("ClientId", clientId));
    Properties.Add(CreateProperty("MachineName", System.Environment.MachineName));
    Properties.Add(CreateProperty("Architecture", platformInfo.Architecture));
    Properties.Add(CreateProperty("Platform", platformInfo.Platform));
    Properties.Add(CreateProperty("RuntimeHost", platformInfo.RuntimeHost));
    Properties.Add(CreateProperty("Runtime", platformInfo.Runtime));
    Properties.Add(CreateProperty("UnityVersion", Application.unityVersion));
    Properties.Add(CreateProperty("LogicalCoreCount", SystemInfo.processorCount));
    Properties.Add(CreateProperty("CpuFrequency", SystemInfo.processorFrequency));
    Properties.Add(CreateProperty("MemorySizeMB", SystemInfo.systemMemorySize));
    Properties.Add(CreateProperty("OperatingSystem", SystemInfo.operatingSystem));
    Properties.Add(CreateProperty("DeviceModel", SystemInfo.deviceModel));
    Properties.Add(CreateProperty("DeviceName", SystemInfo.deviceName));
    Properties.Add(CreateProperty("ProcessorType", SystemInfo.processorType));

  }


  public string GetProperty(string name, string defaultValue = "Unknown") => Properties.Where(x => x.Name == name).SingleOrDefault()?.Value ?? defaultValue;
  private static CustomProperty CreateProperty<T>(string name, T value) => CreateProperty(name, value.ToString());
  private static CustomProperty CreateProperty(string name, string value) {
    return new CustomProperty() {
      Name = name,
      Value = value,
    };
  }
}

#if QUANTUM_REMOTE_PROFILER
public class QuantumProfilingClient {
  const double BROADCAST_INTERVAL = 1;

  QuantumProfilingClientInfo  _clientInfo;
  NetManager                  _manager;
  EventBasedNetListener       _listener;
  NetPeer                     _serverPeer;

  Stopwatch _broadcastTimer;
  double    _broadcastNext;
  

  public QuantumProfilingClient(string clientId, DeterministicSessionConfig config, DeterministicPlatformInfo platformInfo) {
    _clientInfo = new QuantumProfilingClientInfo(clientId, config, platformInfo);
    _broadcastTimer = Stopwatch.StartNew();

    _listener                           = new EventBasedNetListener();
    _manager                            = new NetManager(_listener);
    _manager.UnconnectedMessagesEnabled = true;
    _manager.Start(0);
    
    //_manager.Connect("192.168.2.199", 30000, NetDataWriter.FromString(QuantumProfilingServer.CONNECT_TOKEN));

    _listener.NetworkReceiveUnconnectedEvent += OnNetworkReceiveUnconnectedEvent;
    _listener.PeerConnectedEvent             += OnPeerConnected;
    _listener.PeerDisconnectedEvent          += OnPeerDisconnected;
  }

  void OnPeerDisconnected(NetPeer peer, DisconnectInfo disconnectinfo) {
    Log.Info($"QuantumProfilingClient: Disconnected from {peer.EndPoint}");

    _serverPeer    = null;
    _broadcastNext = 0;
  }

  void OnPeerConnected(NetPeer peer) {
    Log.Info($"QuantumProfilingClient: Connected to {peer.EndPoint}");
    _serverPeer = peer;

    var writer = new NetDataWriter();
    writer.Put(QuantumProfilingClientConstants.ClientInfoMessage);
    writer.Put(JsonUtility.ToJson(_clientInfo));
    _serverPeer.Send(writer, DeliveryMethod.ReliableUnordered);
  }

  void OnNetworkReceiveUnconnectedEvent(IPEndPoint remoteendpoint, NetPacketReader reader, UnconnectedMessageType messagetype) {
    if (reader.GetString() == QuantumProfilingClientConstants.DISCOVER_RESPONSE_TOKEN) {
      _manager.Connect(remoteendpoint, NetDataWriter.FromString(QuantumProfilingClientConstants.CONNECT_TOKEN));
    }
  }

  public void SendProfilingData(ProfilerContextData data) {
    if (_serverPeer == null) {
      return;
    }

    var writer = new NetDataWriter();
    writer.Put(QuantumProfilingClientConstants.FrameMessage);
    writer.Put(JsonUtility.ToJson(data));
    _serverPeer.Send(writer, DeliveryMethod.ReliableUnordered);
  }

  public void Update() {
    if (_serverPeer == null) {
      var now = _broadcastTimer.Elapsed.TotalSeconds;
      if (now > _broadcastNext) {
        _broadcastNext = now + BROADCAST_INTERVAL;
        _manager.SendBroadcast(NetDataWriter.FromString(QuantumProfilingClientConstants.DISCOVER_TOKEN), 30000);
        Log.Info("QuantumProfilingClient: Looking For Profiling Server");
      }
    }

    _manager.PollEvents();
  }
}
#endif