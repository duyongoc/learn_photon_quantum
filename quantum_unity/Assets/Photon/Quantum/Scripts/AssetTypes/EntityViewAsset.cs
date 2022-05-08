using System;
using UnityEngine;

public class EntityViewAsset : AssetBase, IQuantumPrefabNestedAsset<EntityView> {
  public Quantum.EntityView Settings;

  public EntityView Parent;

  [Obsolete("Use View instead")]
  public EntityView Prefab => View;
  public EntityView View => Parent;

  public override Quantum.AssetObject AssetObject => Settings;

  Component IQuantumPrefabNestedAsset.Parent => Parent;

  public override void Reset() {
    if (Settings == null) {
      Settings = new Quantum.EntityView();
    }

    base.Reset();
  }

  public virtual EntityViewAssetStatus ViewStatus => EntityViewAssetStatus.Loaded;

  public virtual void LoadViewPrefab(bool async = false) {
    Debug.LogWarning($"There should be no need to call this method, View is referenced statically");
  }
}

public enum EntityViewAssetStatus {
  NotLoaded,
  Loading,
  Loaded,
  Error
}
