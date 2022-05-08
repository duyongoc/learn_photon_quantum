using System;
using System.Diagnostics;
using System.Security.Cryptography;
using UnityEngine;
using UnityEngine.EventSystems;
using Debug = UnityEngine.Debug;
using UI = UnityEngine.UI;

public class QuantumStats : MonoBehaviour {
  public UI.Text FrameVerified;
  public UI.Text FramePredicted;
  public UI.Text Predicted;
  public UI.Text Resimulated;
  public UI.Text SimulateTime;
  public UI.Text SimulationState;
  public UI.Text NetworkPing;
  public UI.Text NetworkIn;
  public UI.Text NetworkOut;
  public UI.Text InputOffset;

  public UI.Text ToggleButtonText;
  public GameObject[] Toggles;
  public Boolean StartEnabled = true;

  Stopwatch _networkTimer;

  void Start() {
    // create event system if none exists in the scene
    var eventSystems = FindObjectsOfType<EventSystem>();
    if (eventSystems == null || eventSystems.Length == 0) {
      gameObject.AddComponent<EventSystem>();
      gameObject.AddComponent<StandaloneInputModule>();
    }
    
    SetState(StartEnabled);
  }

  void Update() {
    if (QuantumRunner.Default && Toggles[0].activeSelf) {
      var gameInstance = QuantumRunner.Default.Game;

      if (gameInstance.Session.FramePredicted != null) {
        FrameVerified.text = gameInstance.Session.FrameVerified.Number.ToString();
        FramePredicted.text = gameInstance.Session.FramePredicted.Number.ToString();
      }

      Predicted.text = gameInstance.Session.PredictedFrames.ToString();
      NetworkPing.text = gameInstance.Session.Stats.Ping.ToString();
      SimulateTime.text = Math.Round(gameInstance.Session.Stats.UpdateTime * 1000, 2) + " ms";
      InputOffset.text = gameInstance.Session.Stats.Offset.ToString();
      Resimulated.text = gameInstance.Session.Stats.ResimulatedFrames.ToString();

      if (gameInstance.Session.IsStalling) {
        SimulationState.text = "Stalling";
        SimulationState.color = Color.red;
      }
      else {
        SimulationState.text = "Running";
        SimulationState.color = Color.green;
      }

      if (QuantumRunner.Default.NetworkClient != null && QuantumRunner.Default.NetworkClient.IsConnected) {
        QuantumRunner.Default.NetworkClient.LoadBalancingPeer.TrafficStatsEnabled = true;

        if (_networkTimer == null) {
          _networkTimer = Stopwatch.StartNew();
        }

        NetworkIn.text = (int)(QuantumRunner.Default.NetworkClient.LoadBalancingPeer.TrafficStatsIncoming.TotalPacketBytes / _networkTimer.Elapsed.TotalSeconds) + " bytes/s";
        NetworkOut.text = (int)(QuantumRunner.Default.NetworkClient.LoadBalancingPeer.TrafficStatsOutgoing.TotalPacketBytes / _networkTimer.Elapsed.TotalSeconds) + " bytes/s";
      }
    }
    else {
      _networkTimer = null;
    }
  }

  public void ResetNetworkStats() {
    _networkTimer = null;

    if (QuantumRunner.Default != null && QuantumRunner.Default.NetworkClient != null && QuantumRunner.Default.NetworkClient.IsConnected) {
      QuantumRunner.Default.NetworkClient.LoadBalancingPeer.TrafficStatsReset();
    }
  }

  void SetState(bool state) {
    for (int i = 0; i < Toggles.Length; ++i) {
      Toggles[i].SetActive(state);
    }

    ToggleButtonText.text = state ? "Hide Stats" : "Show Stats";
  }
  
  public void Toggle() {
    SetState(!Toggles[0].activeSelf);
  }

  public static void Show() {
    GetObject().SetState(true);
  }

  public static void Hide() {
    GetObject().SetState(false);
  }

  public static QuantumStats GetObject() {
    QuantumStats stats;

    // find existing or create new
    if (!(stats = FindObjectOfType<QuantumStats>())) {
      stats = Instantiate(Resources.Load<QuantumStats>(nameof(QuantumStats)));
    }

    return stats;
  }
  
}
