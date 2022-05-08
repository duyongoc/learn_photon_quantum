using System;
using System.Collections.Generic;
using Photon.Deterministic;
using Quantum;
using UnityEngine;

public abstract class QuantumCallbacks : MonoBehaviour {
  public static readonly List<QuantumCallbacks> Instances = new List<QuantumCallbacks>();

  protected virtual void OnEnable() {
    Instances.Add(this);
  }

  protected virtual void OnDisable() {
    Instances.Remove(this);
  }

  public virtual void OnGameStart(QuantumGame game) { }

  public virtual void OnGameResync(QuantumGame game) { }

  //public virtual void OnGameStartFromSnapshot(QuantumGame game, int frameNumber) { }
  public virtual void OnGameDestroyed(QuantumGame game) { }
  public virtual void OnUpdateView(QuantumGame game) { }
  public virtual void OnSimulateFinished(QuantumGame game, Frame frame) { }
  public virtual void OnUnitySceneLoadBegin(QuantumGame game) { }
  public virtual void OnUnitySceneLoadDone(QuantumGame  game) { }
  public virtual void OnUnitySceneUnloadBegin(QuantumGame game) { }
  public virtual void OnUnitySceneUnloadDone(QuantumGame  game) { }
  public virtual void OnChecksumError(QuantumGame game, DeterministicTickChecksumError error, Frame[] frames) { }
}
