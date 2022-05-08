using Quantum;
using UnityEngine;

using ListenerStatus = Quantum.DispatcherBase.ListenerStatus;

public interface IQuantumUnityDispatcher {
}

public static class IQuantumUnityDispatcherExtensions {
  public const uint CustomFlag_IsUnityObject = 1 << (DispatcherHandlerFlags.CustomFlagsShift + 0);
  public const uint CustomFlag_OnlyIfActiveAndEnabled = 1 << (DispatcherHandlerFlags.CustomFlagsShift + 1);

  internal static ListenerStatus GetUnityListenerStatus(this IQuantumUnityDispatcher _, object listener, uint flags) {
    if (listener == null) {
      return ListenerStatus.Dead;
    }

    if ((flags & CustomFlag_IsUnityObject) == 0) {
      // not an unity object, so can't be dead
      return ListenerStatus.Active;
    }

    // needs to be Unity object now
    Debug.Assert(listener is UnityEngine.Object);

    var asUnityObject = (UnityEngine.Object)listener;

    if (!asUnityObject) {
      return ListenerStatus.Dead;
    }

    if ((flags & CustomFlag_OnlyIfActiveAndEnabled) != 0) {
      if (listener is Behaviour behaviour) {
        return behaviour.isActiveAndEnabled ? ListenerStatus.Active : ListenerStatus.Inactive;
      } else if (listener is GameObject gameObject) {
        return gameObject.activeInHierarchy ? ListenerStatus.Active : ListenerStatus.Inactive;
      }
    }

    return ListenerStatus.Active;
  }

  internal static Quantum.DispatcherSubscription Subscribe<TDispatcher, T>(this TDispatcher dispatcher, UnityEngine.Object listener, Quantum.DispatchableHandler<T> handler, bool once = false, bool onlyIfActiveAndEnabled = false, DispatchableFilter filter = null)
      where TDispatcher : Quantum.DispatcherBase, IQuantumUnityDispatcher
    where T : IDispatchable {
    return dispatcher.Subscribe(listener, handler, once, CustomFlag_IsUnityObject | (onlyIfActiveAndEnabled ? CustomFlag_OnlyIfActiveAndEnabled : 0), filter: filter);
  }
}