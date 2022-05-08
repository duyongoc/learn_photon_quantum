using System;
using System.Collections.Generic;
using Quantum;
using UnityEngine;

public unsafe class QuantumCallbackHandler_StartRecording {

  public static IDisposable Initialize() {
    var disposables = new CompositeDisposable();

    try {
      disposables.Add(QuantumCallback.SubscribeManual((CallbackGameStarted c) => {
        var runner = QuantumRunner.FindRunner(c.Game);
        Debug.Assert(runner);

        if (runner.RecordingFlags.HasFlag(RecordingFlags.Input) && runner.Session.IsPaused == false) {
          c.Game.StartRecordingInput();
        }

        if (runner.RecordingFlags.HasFlag(RecordingFlags.Checksums)) {
          c.Game.StartRecordingChecksums();
        }
      }));
      
      disposables.Add(QuantumCallback.SubscribeManual((CallbackGameResynced c) => {
        var runner = QuantumRunner.FindRunner(c.Game);
        Debug.Assert(runner);

        if (runner.RecordingFlags.HasFlag(RecordingFlags.Input)) {
          Assert.Check(runner.Session.IsPaused == false);
          
          // on a resync, start recording from the next frame on
          c.Game.StartRecordingInput(c.Game.Frames.Verified.Number + 1);
        }
      }));
    } catch {
      // if something goes wrong clean up subscriptions
      disposables.Dispose();
      throw;
    }

    return disposables;
  }
  
  private class CompositeDisposable : IDisposable {
    private List<IDisposable> _disposables = new List<IDisposable>();

    public void Add(IDisposable disposable) {
      _disposables.Add(disposable);
    }

    public void Dispose() {
      foreach (var disposable in _disposables) {
        try { disposable.Dispose(); } catch (Exception ex) { Debug.LogException(ex); }
      }
    }
  }
}