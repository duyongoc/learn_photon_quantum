using System;
using System.Collections.Generic;
using Quantum;
using UnityEngine;

namespace Quantum {
  public enum UnityCallbackId {
    UnitySceneLoadBegin = CallbackId.UserCallbackIdStart,
    UnitySceneLoadDone,
    UnitySceneUnloadBegin,
    UnitySceneUnloadDone,
  }

  public interface ICallbackUnityScene {
    string SceneName { get; set; }
  }

  public class CallbackUnitySceneLoadBegin : QuantumGame.CallbackBase, ICallbackUnityScene {
    public new const Int32 ID = (int)UnityCallbackId.UnitySceneLoadBegin;
    public CallbackUnitySceneLoadBegin(QuantumGame game) : base(ID, game) { }
    public string SceneName { get; set; }
  }

  public class CallbackUnitySceneLoadDone : QuantumGame.CallbackBase, ICallbackUnityScene {
    public new const Int32 ID = (int)UnityCallbackId.UnitySceneLoadDone;
    public CallbackUnitySceneLoadDone(QuantumGame game) : base(ID, game) { }
    public string SceneName { get; set; }
  }

  public class CallbackUnitySceneUnloadBegin : QuantumGame.CallbackBase, ICallbackUnityScene {
    public new const Int32 ID = (int)UnityCallbackId.UnitySceneUnloadBegin;
    public CallbackUnitySceneUnloadBegin(QuantumGame game) : base(ID, game) { }
    public string SceneName { get; set; }
  }

  public class CallbackUnitySceneUnloadDone : QuantumGame.CallbackBase, ICallbackUnityScene {
    public new const Int32 ID = (int)UnityCallbackId.UnitySceneUnloadDone;
    public CallbackUnitySceneUnloadDone(QuantumGame game) : base(ID, game) { }
    public string SceneName { get; set; }
  }
}

public partial class QuantumCallback : QuantumUnityStaticDispatcherAdapter<QuantumUnityCallbackDispatcher, Quantum.CallbackBase> {
  private QuantumCallback() {
    throw new NotSupportedException();
  }

  [RuntimeInitializeOnLoadMethod]
  static void SetupDefaultHandlers() {

    // default callbacks handlers are initialised here; if you want them disabled, implement partial
    // method IsDefaultHandlerEnabled

    {
      bool enabled = true;
      IsDefaultHandlerEnabled(typeof(QuantumCallbackHandler_DebugDraw), ref enabled);
      if (enabled) {
        QuantumCallbackHandler_DebugDraw.Initialize();
      }
    }
    {
      bool enabled = true;
      IsDefaultHandlerEnabled(typeof(QuantumCallbackHandler_FrameDiffer), ref enabled);
      if (enabled) {
        QuantumCallbackHandler_FrameDiffer.Initialize();
      }
    }
    {
      bool enabled = true;
      IsDefaultHandlerEnabled(typeof(QuantumCallbackHandler_LegacyQuantumCallback), ref enabled);
      if (enabled) {
        QuantumCallbackHandler_LegacyQuantumCallback.Initialize();
      }
    }
    {
      bool enabled = true;
      IsDefaultHandlerEnabled(typeof(QuantumCallbackHandler_StartRecording), ref enabled);
      if (enabled) {
        QuantumCallbackHandler_StartRecording.Initialize();
      }
    }
    {
      bool enabled = true;
      IsDefaultHandlerEnabled(typeof(QuantumCallbackHandler_UnityCallbacks), ref enabled);
      if (enabled) {
        QuantumCallbackHandler_UnityCallbacks.Initialize();
      }
    }
  }

  static partial void IsDefaultHandlerEnabled(Type type, ref bool enabled);
}

public partial class QuantumUnityCallbackDispatcher : Quantum.CallbackDispatcher, IQuantumUnityDispatcher {

  public QuantumUnityCallbackDispatcher() : base(GetCallbackTypes()) { }

  protected override ListenerStatus GetListenerStatus(object listener, uint flags) {
    return this.GetUnityListenerStatus(listener, flags);
  }

  static partial void AddUserTypes(Dictionary<Type, Int32> dict);

  private static Dictionary<Type, Int32> GetCallbackTypes() {
    var types = Quantum.CallbackDispatcher.GetBuiltInTypes();

    // unity-side callback types
    types.Add(typeof(CallbackUnitySceneLoadBegin), CallbackUnitySceneLoadBegin.ID);
    types.Add(typeof(CallbackUnitySceneLoadDone), CallbackUnitySceneLoadDone.ID);
    types.Add(typeof(CallbackUnitySceneUnloadBegin), CallbackUnitySceneUnloadBegin.ID);
    types.Add(typeof(CallbackUnitySceneUnloadDone), CallbackUnitySceneUnloadDone.ID);


    AddUserTypes(types);
    return types;
  }
}
