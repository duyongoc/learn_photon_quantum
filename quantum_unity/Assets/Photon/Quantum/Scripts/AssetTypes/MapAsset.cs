using System;
using Quantum;
using System.Collections.Generic;
using UnityEngine;
using Quantum.Prototypes;

[CreateAssetMenu(menuName = "Quantum/Map", order = EditorDefines.AssetMenuPriorityStart + 12 * 26)]
public partial class MapAsset : AssetBase {
  public Map Settings;
  public List<FlatEntityPrototypeContainer> Prototypes;

  public override AssetObject AssetObject => Settings;

  public override void PrepareAsset() {
    base.PrepareAsset();

    Settings.MapEntities = new EntityPrototypeContainer[Prototypes.Count];
    
    var buffer = new List<ComponentPrototype>();
    for (int i = 0; i < Prototypes.Count; ++i) {
      try {
        Prototypes[i].Collect(buffer);
        Settings.MapEntities[i] = new EntityPrototypeContainer() {
          Components = buffer.ToArray()
        };
      } finally {
        buffer.Clear();
      }
    }
  }

  public override void Reset() {
    if (Settings == null) {
      Settings = new Quantum.Map();
    }
    if (Prototypes == null) {
      Prototypes = new List<FlatEntityPrototypeContainer>();
    }

    base.Reset();
  }
}

public static partial class MapAssetExts {
  public static MapAsset GetUnityAsset(this Map data) {
    return data == null ? null : UnityDB.FindAsset<MapAsset>(data);
  }
}
