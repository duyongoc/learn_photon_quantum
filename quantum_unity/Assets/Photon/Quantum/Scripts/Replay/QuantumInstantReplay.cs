using System;
using System.Collections.Generic;
using Photon.Deterministic;
using UnityEngine;

namespace Quantum {

  public enum QuantumInstantReplaySeekMode {
    Disabled,
    FromStartSnapshot,
    FromIntermediateSnapshots,
  }

  public sealed class QuantumInstantReplay : IDisposable {

    // We need this to fast forward the simulation and wait until is fully initialized.
    public const int InitalFramesToSimulation = 4;

    private bool _loop;
    private QuantumRunner _replayRunner;
    private DeterministicFrameRingBuffer _rewindSnapshots;

    public QuantumInstantReplay(QuantumGame liveGame, float length, QuantumInstantReplaySeekMode seekMode = QuantumInstantReplaySeekMode.Disabled, bool loop = false) {
      if (liveGame == null) {
        throw new ArgumentNullException(nameof(liveGame));
      }

      LiveGame = liveGame;
      EndFrame = liveGame.Frames.Verified.Number;

      var inputProvider = liveGame.Session.IsReplay ? liveGame.Session.ReplayProvider : liveGame.RecordedInputs;
      if (inputProvider == null) {
        throw new ArgumentException(nameof(liveGame), "Can't run instant replays without an input provider. Start the game with StartParams including RecordingFlags.Input.");
      }

      var deterministicConfig = liveGame.Session.SessionConfig;
      var desiredReplayFrame = EndFrame - Mathf.FloorToInt(length * deterministicConfig.UpdateFPS);
      // clamp against actual start frame
      desiredReplayFrame = Mathf.Max(deterministicConfig.UpdateFPS, desiredReplayFrame);

      var snapshot = liveGame.GetInstantReplaySnapshot(desiredReplayFrame);
      if (snapshot == null) {
        throw new ArgumentException(nameof(liveGame), "Unable to find a snapshot for frame " + desiredReplayFrame);
      }

      StartFrame = Mathf.Max(snapshot.Number, desiredReplayFrame);

      List<Frame> snapshotsForRewind = null;
      if (seekMode == QuantumInstantReplaySeekMode.FromIntermediateSnapshots) {
        snapshotsForRewind = new List<Frame>();
        liveGame.GetInstantReplaySnapshots(desiredReplayFrame, EndFrame, snapshotsForRewind);
        Debug.Assert(snapshotsForRewind.Count >= 1);
      } else if (seekMode == QuantumInstantReplaySeekMode.FromStartSnapshot) {
        snapshotsForRewind = new List<Frame>() { snapshot };
      } else if (loop) {
        throw new ArgumentException(nameof(loop), $"Seek mode not compatible with looping: {seekMode}");
      }

      _loop = loop;

      // Create all required start parameters and serialize the snapshot as start data.
      var param = new QuantumRunner.StartParameters {
        RuntimeConfig = liveGame.Configurations.Runtime,
        DeterministicConfig = deterministicConfig,
        ReplayProvider = inputProvider,
        GameMode = DeterministicGameMode.Replay,
        FrameData = snapshot.Serialize(DeterministicFrameSerializeMode.Blit),
        InitialFrame = snapshot.Number,
        RunnerId = "InstantReplay",
        PlayerCount = deterministicConfig.PlayerCount,
        LocalPlayerCount = deterministicConfig.PlayerCount,
        HeapExtraCount = snapshotsForRewind?.Count ?? 0,
      };

      _replayRunner = QuantumRunner.StartGame("INSTANTREPLAY", param);
      _replayRunner.OverrideUpdateSession = true;

      // Run a couple of frames until fully initialized (replayRunner.Session.FrameVerified is set and session state isRunning).
      for (int i = 0; i < InitalFramesToSimulation; i++) {
        _replayRunner.Session.Update(1.0f / deterministicConfig.UpdateFPS);
      }

      // clone the original snapshots
      Debug.Assert(_rewindSnapshots == null);
      if (snapshotsForRewind != null) {
        _rewindSnapshots = new DeterministicFrameRingBuffer(snapshotsForRewind.Count);
        foreach (var frame in snapshotsForRewind) {
          _rewindSnapshots.PushBack(frame, _replayRunner.Game.CreateFrame);
        }
      }

      if (desiredReplayFrame > CurrentFrame) {
        FastForward(desiredReplayFrame);
      }
    }

    public int StartFrame { get; }
    public int CurrentFrame => _replayRunner.Game.Frames.Verified.Number;
    public int EndFrame { get; }

    public bool CanSeek => _rewindSnapshots?.Count > 0;
    public bool IsRunning => CurrentFrame < EndFrame;

    public QuantumGame LiveGame { get; }
    public QuantumGame ReplayGame => _replayRunner?.Game;

    public float NormalizedTime {
      get {
        var currentFrame = _replayRunner.Game.Frames.Verified.Number;
        float result = (currentFrame - StartFrame) / (float)(EndFrame - StartFrame);
        Debug.Assert(result >= 0.0f);
        return Mathf.Clamp01(result);
      }
    }

    public void Dispose() {
      _rewindSnapshots?.Clear();
      _rewindSnapshots = null;
      _replayRunner?.Shutdown();
      _replayRunner = null;
    }

    public void SeekFrame(int frameNumber) {
      if (!CanSeek) {
        throw new InvalidOperationException("Not seekable");
      }

      Debug.Assert(_rewindSnapshots != null);
      var frame = _rewindSnapshots.Find(frameNumber, DeterministicFrameSnapshotBufferFindMode.ClosestLessThanOrEqual);
      if (frame == null) {
        throw new ArgumentOutOfRangeException(nameof(frameNumber), $"Unable to find a frame with number less or equal to {frameNumber}.");
      }

      _replayRunner.Session.ResetReplay(frame);
      FastForward(frameNumber);
    }

    public void SeekNormalizedTime(float normalizedTime) {
      var frame = Mathf.FloorToInt(Mathf.Lerp(StartFrame, EndFrame, normalizedTime));
      SeekFrame(frame);
    }

    public bool Update(float deltaTime) {
      _replayRunner.Session.Update(deltaTime);

      // Stop the running instant replay.
      if (_replayRunner.Game.Frames.Verified != null &&
          _replayRunner.Game.Frames.Verified.Number >= EndFrame) {
        if (_loop) {
          SeekFrame(StartFrame);
        } else {
          return false;
        }
      }

      return true;
    }

    private void FastForward(int frameNumber) {

      if (frameNumber < CurrentFrame) {
        throw new ArgumentException($"Can't seek backwards to {frameNumber} from {CurrentFrame}", nameof(frameNumber));
      } else if (frameNumber == CurrentFrame) {
        // nothing to do here
        return;
      }

      const int MaxAttempts = 3;
      for (int attemptsLeft = MaxAttempts; attemptsLeft > 0; --attemptsLeft) {

        int beforeUpdate = CurrentFrame;

        double deltaTime = GetDeltaTime(frameNumber - beforeUpdate, _replayRunner.Session.SessionConfig.UpdateFPS);
        _replayRunner.Session.Update(deltaTime);

        int afterUpdate = CurrentFrame;

        if (afterUpdate >= frameNumber) {
          if (afterUpdate > frameNumber) {
            Debug.LogWarning($"Seeked after the target frame {frameNumber} (from {beforeUpdate}), got to {afterUpdate}.");
          }
          return;
        } else {
          Debug.LogWarning($"Failed to seek to frame {frameNumber} (from {beforeUpdate}), got to {afterUpdate}. {attemptsLeft} attempts left.");
        }
      }

      throw new InvalidOperationException($"Unable to seek to frame {frameNumber}, ended up on {CurrentFrame}");
    }

    private static double GetDeltaTime(int frames, int simulationRate) {
      // need repeated sum here, since internally Quantum performs repeated substraction
      double delta = 1.0 / simulationRate;
      double result = 0;
      for (int i = 0; i < frames; ++i) {
        result += delta;
      }
      return result;
    }
  }

  [Obsolete]
  public class QuantumInstantReplayLegacy {

    public bool IsRunning { get; private set; }
    public float ReplayLength { get; set; }
    public float PlaybackSpeed { get; set; }
    public QuantumGame LiveGame => _liveGame;
    public QuantumGame ReplayGame => _replayRunner?.Game;

    public int StartFrame { get; private set; }
    public int EndFrame { get; private set; }

    public bool CanSeek => _rewindSnapshots?.Count > 0;

    public float NormalizedTime {
      get {
        if (!IsRunning) {
          throw new InvalidOperationException("Not running");
        }
        var currentFrame = _replayRunner.Game.Frames.Verified.Number;
        float result = (currentFrame - StartFrame) / (float)(EndFrame - StartFrame);
        return result;
      }
    }

    public event Action<QuantumGame> OnReplayStarted;
    public event Action<QuantumGame> OnReplayStopped;

    // We need this to fast forward the simulation and wait until is fully initialized.
    public const int InitalFramesToSimulation = 4;

    private QuantumGame _liveGame;
    private QuantumRunner _replayRunner;
    private DeterministicFrameRingBuffer _rewindSnapshots;
    private bool _loop;

    public QuantumInstantReplayLegacy(QuantumGame game) {
      _liveGame = game;
    }

    public void Shutdown() {
      if (IsRunning)
        StopInstantReplay();

      OnReplayStarted = null;
      OnReplayStopped = null;

      _liveGame = null;
    }

    public void Update() {
      if (IsRunning) {
        _replayRunner.Session.Update(Time.unscaledDeltaTime * PlaybackSpeed);

        // Stop the running instant replay.
        if (_replayRunner.Game.Frames.Verified != null &&
            _replayRunner.Game.Frames.Verified.Number >= EndFrame) {

          if (_loop) {
            SeekFrame(StartFrame);
          } else {
            StopInstantReplay();
          }
        }
      }
    }

    public void StartInstantReplay(QuantumInstantReplaySeekMode seekMode = QuantumInstantReplaySeekMode.Disabled, bool loop = false) {
      if (IsRunning) {
        Debug.LogError("Instant replay is already running.");
        return;
      }

      var inputProvider = _liveGame.Session.IsReplay ? _liveGame.Session.ReplayProvider : _liveGame.RecordedInputs;
      if (inputProvider == null) {
        Debug.LogError("Can't run instant replays without an input provider. Start the game with StartParams including RecordingFlags.Input.");
        return;
      }

      IsRunning = true;
      EndFrame = _liveGame.Frames.Verified.Number;

      var deterministicConfig = _liveGame.Session.SessionConfig;
      var desiredReplayFrame = EndFrame - Mathf.FloorToInt(ReplayLength * deterministicConfig.UpdateFPS);

      // clamp against actual start frame
      desiredReplayFrame = Mathf.Max(deterministicConfig.UpdateFPS, desiredReplayFrame);

      var snapshot = _liveGame.GetInstantReplaySnapshot(desiredReplayFrame);
      if (snapshot == null) {
        throw new InvalidOperationException("Unable to find a snapshot for frame " + desiredReplayFrame);
      }

      StartFrame = Mathf.Max(snapshot.Number, desiredReplayFrame);

      List<Frame> snapshotsForRewind = null;
      if (seekMode == QuantumInstantReplaySeekMode.FromIntermediateSnapshots) {
        snapshotsForRewind = new List<Frame>();
        _liveGame.GetInstantReplaySnapshots(desiredReplayFrame, EndFrame, snapshotsForRewind);
        Debug.Assert(snapshotsForRewind.Count >= 1);
      } else if (seekMode == QuantumInstantReplaySeekMode.FromStartSnapshot) {
        snapshotsForRewind = new List<Frame>();
        snapshotsForRewind.Add(snapshot);
      } else if (loop) {
        throw new ArgumentException(nameof(loop), $"Seek mode not compatible with looping: {seekMode}");
      }

      _loop = loop;


      // Create all required start parameters and serialize the snapshot as start data.
      var param = new QuantumRunner.StartParameters {
        RuntimeConfig = _liveGame.Configurations.Runtime,
        DeterministicConfig = deterministicConfig,
        ReplayProvider = inputProvider,
        GameMode = DeterministicGameMode.Replay,
        FrameData = snapshot.Serialize(DeterministicFrameSerializeMode.Blit),
        InitialFrame = snapshot.Number,
        RunnerId = "InstantReplay",
        PlayerCount = deterministicConfig.PlayerCount,
        LocalPlayerCount = deterministicConfig.PlayerCount,
        HeapExtraCount = snapshotsForRewind?.Count ?? 0,
      };

      _replayRunner = QuantumRunner.StartGame("INSTANTREPLAY", param);
      _replayRunner.OverrideUpdateSession = true;

      // Run a couple of frames until fully initialized (replayRunner.Session.FrameVerified is set and session state isRunning).
      for (int i = 0; i < InitalFramesToSimulation; i++) {
        _replayRunner.Session.Update(1.0f / deterministicConfig.UpdateFPS);
      }

      // clone the original snapshots
      Debug.Assert(_rewindSnapshots == null);
      if (snapshotsForRewind != null) {
        _rewindSnapshots = new DeterministicFrameRingBuffer(snapshotsForRewind.Count);
        foreach (var frame in snapshotsForRewind) {
          _rewindSnapshots.PushBack(frame, _replayRunner.Game.CreateFrame);
        }
      }

      FastForwardSimulation(desiredReplayFrame);

      if (OnReplayStarted != null)
        OnReplayStarted(_replayRunner.Game);
    }

    public void SeekNormalizedTime(float seek) {
      var frame = Mathf.FloorToInt(Mathf.Lerp(StartFrame, EndFrame, seek));
      SeekFrame(frame);
    }

    public void SeekFrame(int frameNumber) {
      if (!CanSeek) {
        throw new InvalidOperationException("Not seekable");
      }
      if (!IsRunning) {
        throw new InvalidOperationException("Not running");
      }

      Debug.Assert(_rewindSnapshots != null);
      var frame = _rewindSnapshots.Find(frameNumber, DeterministicFrameSnapshotBufferFindMode.ClosestLessThanOrEqual);
      if (frame == null) {
        throw new ArgumentOutOfRangeException(nameof(frameNumber), $"Unable to find a frame with number less or equal to {frameNumber}.");
      }

      _replayRunner.Session.ResetReplay(frame);
      FastForwardSimulation(frameNumber);
    }

    public void StopInstantReplay() {

      if (!IsRunning) {
        Debug.LogError("Instant replay is not running.");
        return;
      }

      IsRunning = false;

      if (OnReplayStopped != null)
        OnReplayStopped(_replayRunner.Game);

      _rewindSnapshots?.Clear();
      _rewindSnapshots = null;

      if (_replayRunner != null)
        _replayRunner.Shutdown();

      _replayRunner = null;
    }

    private void FastForwardSimulation(int frameNumber) {
      var simulationRate = _replayRunner.Session.SessionConfig.UpdateFPS;
      while (_replayRunner.Session.FrameVerified.Number < frameNumber) {
        _replayRunner.Session.Update(1.0f / simulationRate);
      }
    }
  }
}