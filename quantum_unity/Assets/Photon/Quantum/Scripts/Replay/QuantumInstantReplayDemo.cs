using UnityEngine;
using Quantum;
using System;

public class QuantumInstantReplayDemo : MonoBehaviour {

  public float PlaybackSpeed = 0.5f;
  [QuantumInspector, Quantum.Inspector.ReadOnly]
  public bool IsReplayRunning;
  [InspectorButton("Editor_StartInstantReplay", "Start", true)]
  public bool Button_StartInstantReplay;
  [InspectorButton("Editor_StopInstantReplay", "Stop", true)]
  public bool Button_StopInstantReplay;
  public float ReplayLengthSec = 2.0f;
  public bool ShowReplayLabel = true;
  public bool ShowFadingEffect = true;

  [Space]
  public QuantumInstantReplaySeekMode RewindMode = QuantumInstantReplaySeekMode.Disabled;

  [Header("These only work if RewindMode is set")]
  public bool EnableLoop = false;
  [Range(0, 1)]
  public float NormalizedTime;
  private float previousNormalizedTime;

  QuantumInstantReplay _instantReplay;
  bool _isFading;
  float _fadingAlpha = 1.0f;
  Texture2D _fadingTexture;
  float _fadingTime;

  #region Unity Callbacks

  private void Awake() {

    QuantumCallback.Subscribe(this, (CallbackGameDestroyed c) => {
      if (_instantReplay == null)
        return;

      if (c.Game == _instantReplay.LiveGame) {
        // main game was shut down, shut down replay
        CleanUpReplay();
      } else if (c.Game == _instantReplay.ReplayGame) {
        // this will be called if the replay runner is shut down outside this class.
        // we can call shutdown() on the runner multiple times during the same frame.
        CleanUpReplay();
      }
    });
  }

  public void Update() {

    if ( QuantumRunner.Default != null ) {
      // Tell the game to start capturing snapshots. This can be called at any point in the game.
      QuantumRunner.Default.Game.StartRecordingInstantReplaySnapshots();
    }

    if (_instantReplay != null) {
      if (_instantReplay.CanSeek) {
        if (previousNormalizedTime != NormalizedTime) {
          _instantReplay.SeekNormalizedTime(NormalizedTime);
        }
      }

      if (_instantReplay.Update(Time.unscaledDeltaTime * PlaybackSpeed)) {
        previousNormalizedTime = NormalizedTime = _instantReplay.NormalizedTime;
      } else {
        CleanUpReplay();
      }
    }

    Button_StartInstantReplay = _instantReplay == null && QuantumRunner.Default != null;
    Button_StopInstantReplay = _instantReplay != null;
    IsReplayRunning = _instantReplay != null;
  }

  private void CleanUpReplay() {
    _instantReplay.Dispose();
    _instantReplay = null;
    OnReplayStopped();
  }

  public void OnDisable() {
    if (_instantReplay != null && QuantumRunner.Default != null) {
      _instantReplay.Dispose();
      _instantReplay = null;
    }
  }

  void OnDestroy() {
    if (_fadingTexture != null)
      Destroy(_fadingTexture);
    _fadingTexture = null;
  }

  void OnGUI() {
    if (ShowReplayLabel && _instantReplay != null) {
      GUI.contentColor = Color.red;
      GUI.Label(new Rect(10, 10, 200, 100), "INSTANT REPLAY");

      bool guiEnabled = GUI.enabled;
      try {
        GUI.enabled = _instantReplay.CanSeek;
        var frameNumber = _instantReplay.ReplayGame.Frames.Verified.Number;
        var seekFrameNumber = (int)GUI.HorizontalSlider(new Rect(10, 40, 150, 100), frameNumber, _instantReplay.StartFrame, _instantReplay.EndFrame);
        if (_instantReplay.CanSeek && frameNumber != seekFrameNumber) {
          _instantReplay.SeekFrame(seekFrameNumber);
        }
      } finally {
        GUI.enabled = guiEnabled;
      }
    }

    if (_isFading) {
      _fadingTime += Time.deltaTime;
      _fadingAlpha = Mathf.Lerp(1.0f, 0.0f, _fadingTime);

      if (_fadingTexture == null)
        _fadingTexture = new Texture2D(1, 1);

      _fadingTexture.SetPixel(0, 0, new Color(0, 0, 0, _fadingAlpha));
      _fadingTexture.Apply();

      GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), _fadingTexture);

      _isFading = _fadingAlpha > 0;
    }
  }

  #endregion

  #region Instant Replay Callbacks

  void OnReplayStarted(QuantumGame game) {
    Debug.LogFormat("### Starting quantum instant replay at frame {0} ###", game.Frames.Predicted.Number);

    // FindObjectOfType is super slow, but it serves the demo purpose here.
    var entityViewUpdater = GameObject.FindObjectOfType<EntityViewUpdater>();
    if (entityViewUpdater != null) {
      entityViewUpdater.SetCurrentGame(game);
      entityViewUpdater.TeleportAllEntities();
    }

    StartFading();
  }

  void OnReplayStopped() {
    Debug.LogFormat("### Stopping quantum instant replay and resuming the live game ###");

    var entityViewUpdater = GameObject.FindObjectOfType<EntityViewUpdater>();
    if (entityViewUpdater != null) {
      entityViewUpdater.SetCurrentGame(QuantumRunner.Default.Game);
      entityViewUpdater.TeleportAllEntities();
    }

    StartFading();
  }

  void StartFading() {
    if (ShowFadingEffect) {
      _isFading = true;
      _fadingAlpha = 1.0f;
      _fadingTime = 0.0f;
    }
  }

  #endregion

  #region Editor Button

  public void Editor_StartInstantReplay() {

    if (_instantReplay == null && QuantumRunner.Default) {
      _instantReplay = new QuantumInstantReplay(QuantumRunner.Default.Game, ReplayLengthSec, RewindMode, EnableLoop);
      OnReplayStarted(_instantReplay.ReplayGame);
    }
  }

  public void Editor_StopInstantReplay() {
    if (_instantReplay != null) {
      CleanUpReplay();
    }
  }

  #endregion
}
