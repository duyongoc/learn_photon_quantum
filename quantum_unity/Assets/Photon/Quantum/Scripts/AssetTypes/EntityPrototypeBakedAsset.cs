using System.Collections.Generic;
using Quantum;

internal class EntityPrototypeBakedAsset : EntityPrototypeAsset, IQuantumPrefabBakedAsset {

  public Quantum.Prototypes.FlatEntityPrototypeContainer Container;

  public override void Loaded() {
    PrepareAsset();
  }

  public override void PrepareAsset() {
    try {
      UnityEngine.Debug.Assert(prototypeBuffer.Count == 0);
      Container.Collect(prototypeBuffer);
      Settings.Container.Components = prototypeBuffer.ToArray();
      Settings.Container.SetDirty();
    } finally {
      prototypeBuffer.Clear();
    }
  }

  void IQuantumPrefabBakedAsset.Import(QuantumPrefabAsset prefab, IQuantumPrefabNestedAsset asset) {
    Settings = ((EntityPrototypeAsset)asset).Settings;

    var prototype = (EntityPrototypeAsset)asset;
    prototype.PrepareAsset();
    Container = new Quantum.Prototypes.FlatEntityPrototypeContainer();
    Container.Store(prototype.Settings.Container.Components);
  }
}