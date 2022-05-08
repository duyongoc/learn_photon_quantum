using System;

public class QuantumEvent : QuantumUnityStaticDispatcherAdapter<QuantumUnityEventDispatcher, Quantum.EventBase> {
  private QuantumEvent() {
    throw new NotSupportedException();
  }
}

public class QuantumUnityEventDispatcher : Quantum.EventDispatcher, IQuantumUnityDispatcher {

  protected override ListenerStatus GetListenerStatus(object listener, uint flags) {
    return this.GetUnityListenerStatus(listener, flags);
  }
}
