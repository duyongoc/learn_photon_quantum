using System;
using Photon.Deterministic;
using Quantum;
using UnityEngine;


public abstract class QuantumUnityStaticDispatcherAdapter {
  protected sealed class Worker : MonoBehaviour {
    public DispatcherBase Dispatcher;
    private void LateUpdate() {
      if (Dispatcher == null) {
        // this may happen when scripts get reloaded in editor
        Destroy(gameObject);
      } else {
        Dispatcher.RemoveDeadListners();
      }
    }
  }
}

public abstract class QuantumUnityStaticDispatcherAdapter<TDispatcher, TDispatchableBase> : QuantumUnityStaticDispatcherAdapter
  where TDispatcher : DispatcherBase, IQuantumUnityDispatcher, new()
  where TDispatchableBase : IDispatchable {

  protected static Worker _worker;

  public static TDispatcher Dispatcher { get; } = new TDispatcher();

  public static void Clear() {
    Dispatcher.Clear();
    if (_worker) {
      UnityEngine.Object.Destroy(_worker.gameObject);
      _worker = null;
    }
  }

  public static void RemoveDeadListeners() {
    Dispatcher.RemoveDeadListners();
  }

  public static Quantum.DispatcherSubscription Subscribe<TDispatchable>(UnityEngine.Object listener, Quantum.DispatchableHandler<TDispatchable> handler, DispatchableFilter filter = null,
    bool once = false, bool onlyIfActiveAndEnabled = false, bool onlyIfEntityViewBound = false)
    where TDispatchable : TDispatchableBase {

    if (onlyIfEntityViewBound) {
      EntityView view;
      if (listener is Component comp) {
        view = comp.GetComponentInParent<EntityView>();
      } else if (listener is GameObject go) {
        view = go.GetComponentInParent<EntityView>();
      } else {
        throw new ArgumentException($"To use {nameof(onlyIfEntityViewBound)} parameter, {nameof(listener)} needs to be a Component or a GameObject", nameof(listener));
      }

      if (view == null) {
        throw new ArgumentException($"Unable to find {nameof(EntityView)} component in {listener} or any of its parents", nameof(listener));
      }

      filter = ComposeFilters((_) => view.EntityRef.IsValid, filter);
    }

    EnsureWorkerExistsAndIsActive();
    return Dispatcher.Subscribe(listener, handler, once, onlyIfActiveAndEnabled, filter: filter);
  }

  public static Quantum.DispatcherSubscription Subscribe<TDispatchable>(UnityEngine.Object listener, Quantum.DispatchableHandler<TDispatchable> handler, DeterministicGameMode gameMode, bool exclude = false,
    bool once = false, bool onlyIfActiveAndEnabled = false, bool onlyIfEntityViewBound = false)
    where TDispatchable : TDispatchableBase {
    return Subscribe(listener, handler, (game) => (game.Session.GameMode == gameMode) ^ exclude, once, onlyIfActiveAndEnabled, onlyIfEntityViewBound);
  }

  public static Quantum.DispatcherSubscription Subscribe<TDispatchable>(UnityEngine.Object listener, Quantum.DispatchableHandler<TDispatchable> handler, DeterministicGameMode[] gameModes, bool exclude = false,
    bool once = false, bool onlyIfActiveAndEnabled = false, bool onlyIfEntityViewBound = false)
    where TDispatchable : TDispatchableBase {
    return Subscribe(listener, handler, (game) => (Array.IndexOf(gameModes, game.Session.GameMode) >= 0) ^ exclude, once, onlyIfActiveAndEnabled, onlyIfEntityViewBound);
  }


  public static Quantum.DispatcherSubscription Subscribe<TDispatchable>(UnityEngine.Object listener, Quantum.DispatchableHandler<TDispatchable> handler, string runnerId,
    bool once = false, bool onlyIfActiveAndEnabled = false, bool onlyIfEntityViewBound = false)
    where TDispatchable : TDispatchableBase {
    return Subscribe(listener, handler, (game) => QuantumRunner.FindRunner(game)?.Id == runnerId, once, onlyIfActiveAndEnabled, onlyIfEntityViewBound);
  }

  public static Quantum.DispatcherSubscription Subscribe<TDispatchable>(UnityEngine.Object listener, Quantum.DispatchableHandler<TDispatchable> handler, QuantumRunner runner,
    bool once = false, bool onlyIfActiveAndEnabled = false, bool onlyIfEntityViewBound = false)
    where TDispatchable : TDispatchableBase {
    var runnerInstanceId = runner.GetInstanceID();
    return Subscribe(listener, handler, (game) => QuantumRunner.FindRunner(game)?.GetInstanceID() == runnerInstanceId, once, onlyIfActiveAndEnabled, onlyIfEntityViewBound);
  }

  public static Quantum.DispatcherSubscription Subscribe<TDispatchable>(UnityEngine.Object listener, Quantum.DispatchableHandler<TDispatchable> handler, QuantumGame game,
    bool once = false, bool onlyIfActiveAndEnabled = false, bool onlyIfEntityViewBound = false)
    where TDispatchable : TDispatchableBase {
    return Subscribe(listener, handler, g => g == game, once, onlyIfActiveAndEnabled, onlyIfEntityViewBound);
  }

  public static IDisposable SubscribeManual<TDispatchable>(object listener, Quantum.DispatchableHandler<TDispatchable> handler, DispatchableFilter filter = null, bool once = false)
    where TDispatchable : TDispatchableBase {
    return Dispatcher.SubscribeManual(listener, handler, once, filter);
  }

  public static IDisposable SubscribeManual<TDispatchable>(Quantum.DispatchableHandler<TDispatchable> handler, DispatchableFilter filter = null, bool once = false)
    where TDispatchable : TDispatchableBase {
    return Dispatcher.SubscribeManual(handler, once, filter);
  }

  public static bool Unsubscribe(DispatcherSubscription subscription) {
    return Dispatcher.Unsubscribe(subscription);
  }

  public static bool UnsubscribeListener(object listener) {
    return Dispatcher.UnsubscribeListener(listener);
  }
  public static bool UnsubscribeListener<TDispatchable>(object listener) where TDispatchable : TDispatchableBase {
    return Dispatcher.UnsubscribeListener<TDispatchable>(listener);
  }

  private static void EnsureWorkerExistsAndIsActive() {
    if (_worker) {
      if (!_worker.isActiveAndEnabled)
        throw new InvalidOperationException($"{typeof(Worker)} is disabled");

      return;
    }

    var go = new GameObject(typeof(TDispatcher).Name + nameof(Worker), typeof(Worker));
    go.hideFlags = HideFlags.HideAndDontSave;
    GameObject.DontDestroyOnLoad(go);

    _worker = go.GetComponent<Worker>();
    if (!_worker)
      throw new InvalidOperationException($"Unable to create {typeof(Worker)}");

    _worker.Dispatcher = Dispatcher;
  }

  private static DispatchableFilter ComposeFilters(DispatchableFilter first, DispatchableFilter second) {
    if (first == null && second == null) {
      throw new ArgumentException($"{nameof(first)} and {nameof(second)} can't both be null");
    } else if (first == null) {
      return second;
    } else if (second == null) {
      return first;
    } else {
      return x => first(x) && second(x);
    }
  }
}