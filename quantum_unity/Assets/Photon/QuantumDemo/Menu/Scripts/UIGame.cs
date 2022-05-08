using System;
using System.Collections.Generic;
using Photon.Deterministic;
using Photon.Realtime;
using UnityEngine;

namespace Quantum.Demo {
  public class UIGame : UIScreen<UIGame>, IConnectionCallbacks {
    public GameObject UICamera;
    public List<GameObject> MenuObjects;

    public byte[] FrameSnapshot {
      get {
        if (Mathf.RoundToInt(Time.time) < _frameSnapshotTimeout) {
          return _frameSnapshot;
        }
        return null;
      }
    }

    public int FrameSnapshotNumber {
      get {
        if (Mathf.RoundToInt(Time.time) < _frameSnapshotTimeout) {
          return _frameSnapshotNumber;
        }
        return 0;
      }
    }

    private byte[] _frameSnapshot;
    private int _frameSnapshotNumber;
    private float _frameSnapshotTimeout;

    public void Update() {
      if (QuantumRunner.Default != null && QuantumRunner.Default.HasGameStartTimedOut) {
        UIDialog.Show("Error", "Game start timed out", () => {
          UIMain.Client.Disconnect();
        });
      }
    }

    public override void OnShowScreen(bool first) {
      _frameSnapshot = null;
      _frameSnapshotNumber = 0;
      _frameSnapshotTimeout = 0.0f;

      UICamera.Hide();

      foreach (var menuObject in MenuObjects) {
        menuObject.Hide();
      }

      UIMain.Client?.AddCallbackTarget(this);
      QuantumCallback.Subscribe(this, (CallbackPluginDisconnect c) => OnCallbackPluginDisconnect(c.Reason));
    }

    public override void OnHideScreen(bool first) {
      QuantumCallback.UnsubscribeListener(this);
      UIMain.Client?.RemoveCallbackTarget(this);

      UICamera.Show();

      foreach (var menuObject in MenuObjects) {
        menuObject.Show();
      }
    }

    private void OnCallbackPluginDisconnect(string reason) {
      UIDialog.Show("Plugin Disconnect", reason, () => {
        UIMain.Client.Disconnect();
      });
    }

    public void OnLeaveClicked() {
      UIMain.Client.Disconnect();
      // Debugging: use these instead of UIMain.Client.Disconnect()
      //UIMain.Client.SimulateConnectionLoss(true);
      //UIMain.Client.LoadBalancingPeer.StopThread();
    }

    public void OnConnected() {
    }

    public void OnConnectedToMaster() {
    }

    public void OnDisconnected(DisconnectCause cause) {
      Debug.Log($"Disconnected: {cause}");

      switch (cause) {
        case DisconnectCause.DisconnectByClientLogic:
          break;

        default:
          // Create a frame snapshot to use for reconnecting to the game
          if (QuantumRunner.Default?.Game?.Frames.Verified != null) {
            _frameSnapshot = QuantumRunner.Default.Game.Frames.Verified.Serialize(DeterministicFrameSerializeMode.Blit);
            _frameSnapshotNumber = QuantumRunner.Default.Game.Frames.Verified.Number;
            _frameSnapshotTimeout = Time.time + 20.0f;
            Debug.Log($"Created frame snapshot at tick {_frameSnapshotNumber}");
          }
          break;

      }

      QuantumRunner.ShutdownAll(true);

      HideScreen();
      UIConnect.ShowScreen();
    }

    public void OnRegionListReceived(RegionHandler regionHandler) {
    }

    public void OnCustomAuthenticationResponse(Dictionary<string, object> data) {
    }

    public void OnCustomAuthenticationFailed(string debugMessage) {
    }
  }
}