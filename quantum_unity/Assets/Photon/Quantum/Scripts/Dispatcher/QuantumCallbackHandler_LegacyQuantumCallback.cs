using System;
using System.Collections.Generic;
using System.Linq;
using Quantum;
using UnityEngine;

public static class QuantumCallbackHandler_LegacyQuantumCallback {

  public static IDisposable Initialize() {
    var disposable = new CompositeDisposabe();

    try {
#pragma warning disable CS0618 // Type or member is obsolete
      disposable.Add(QuantumCallback.SubscribeManual((CallbackChecksumError c) => {
        for (Int32 i = QuantumCallbacks.Instances.Count - 1; i >= 0; --i) {
          try {
            QuantumCallbacks.Instances[i].OnChecksumError(c.Game, c.Error, c.Frames);
          } catch (Exception exn) {
            Log.Exception(exn);
          }
        }
      }));

      disposable.Add(QuantumCallback.SubscribeManual((CallbackGameDestroyed c) => {
        var instancesCopy = QuantumCallbacks.Instances.ToList();
        for (Int32 i = instancesCopy.Count - 1; i >= 0; --i) {
          try {
            instancesCopy[i].OnGameDestroyed(c.Game);
          } catch (Exception exn) {
            Log.Exception(exn);
          }
        }
      }));

      disposable.Add(QuantumCallback.SubscribeManual((CallbackGameStarted c) => {
        for (Int32 i = QuantumCallbacks.Instances.Count - 1; i >= 0; --i) {
          try {
            QuantumCallbacks.Instances[i].OnGameStart(c.Game);
          } catch (Exception exn) {
            Log.Exception(exn);
          }
        }
      }));

      disposable.Add(QuantumCallback.SubscribeManual((CallbackGameResynced c) => {
        for (Int32 i = QuantumCallbacks.Instances.Count - 1; i >= 0; --i)
        {
          try
          {
            QuantumCallbacks.Instances[i].OnGameResync(c.Game);
          }
          catch (Exception exn)
          {
            Log.Exception(exn);
          }
        }
      }));

      disposable.Add(QuantumCallback.SubscribeManual((CallbackSimulateFinished c) => {
        for (Int32 i = QuantumCallbacks.Instances.Count - 1; i >= 0; --i) {
          try {
            QuantumCallbacks.Instances[i].OnSimulateFinished(c.Game, c.Frame);
          } catch (Exception exn) {
            Log.Exception(exn);
          }
        }
      }));

      disposable.Add(QuantumCallback.SubscribeManual((CallbackUpdateView c) => {
        for (Int32 i = QuantumCallbacks.Instances.Count - 1; i >= 0; --i) {
          try {
            QuantumCallbacks.Instances[i].OnUpdateView(c.Game);
          } catch (Exception exn) {
            Log.Exception(exn);
          }
        }
      }));

      disposable.Add(QuantumCallback.SubscribeManual((CallbackUnitySceneLoadBegin c) => {
        for (Int32 i = QuantumCallbacks.Instances.Count - 1; i >= 0; --i) {
          try {
            QuantumCallbacks.Instances[i].OnUnitySceneLoadBegin(c.Game);
          } catch (Exception exn) {
            Log.Exception(exn);
          }
        }
      }));

      disposable.Add(QuantumCallback.SubscribeManual((CallbackUnitySceneLoadDone c) => {
        for (Int32 i = QuantumCallbacks.Instances.Count - 1; i >= 0; --i) {
          try {
            QuantumCallbacks.Instances[i].OnUnitySceneLoadDone(c.Game);
          } catch (Exception exn) {
            Log.Exception(exn);
          }
        }
      }));

      disposable.Add(QuantumCallback.SubscribeManual((CallbackUnitySceneUnloadBegin c) => {
        for (Int32 i = QuantumCallbacks.Instances.Count - 1; i >= 0; --i) {
          try {
            QuantumCallbacks.Instances[i].OnUnitySceneUnloadBegin(c.Game);
          } catch (Exception exn) {
            Log.Exception(exn);
          }
        }
      }));

      disposable.Add(QuantumCallback.SubscribeManual((CallbackUnitySceneUnloadDone c) => {
        for (Int32 i = QuantumCallbacks.Instances.Count - 1; i >= 0; --i) {
          try {
            QuantumCallbacks.Instances[i].OnUnitySceneUnloadDone(c.Game);
          } catch (Exception exn) {
            Log.Exception(exn);
          }
        }
      }));
#pragma warning restore CS0618 // Type or member is obsolete
    } catch {
      // if something goes wrong clean up subscriptions
      disposable.Dispose();
      throw;
    }

    return disposable;
  }

  private class CompositeDisposabe : IDisposable {
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