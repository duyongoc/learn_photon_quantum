using Quantum;
using System;
using System.Collections.Generic;
using UnityEngine;

public static class QuantumCallbackHandler_DebugDraw {
  public static IDisposable Initialize() {
    var disposables = new CompositeDisposable();

    try {
      disposables.Add(QuantumCallback.SubscribeManual((CallbackGameStarted c) => {
        DebugDraw.Clear();
      }));
      disposables.Add(QuantumCallback.SubscribeManual((CallbackGameDestroyed c) => {
        DebugDraw.Clear();
      }));
      disposables.Add(QuantumCallback.SubscribeManual((CallbackSimulateFinished c) => {
        DebugDraw.TakeAll();
      }));
      disposables.Add(QuantumCallback.SubscribeManual((CallbackUpdateView c) => {
        DebugDraw.DrawAll();
      }));
    }
    catch {
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