using System;
using Quantum;
using UnityEngine;

internal class EntityViewBakedAsset : EntityViewAsset, IQuantumPrefabBakedAsset, QuantumPrefabAsset.IListener {
  public QuantumPrefabAsset PrefabAsset;
  private EntityViewAssetStatus _status;

  public override EntityViewAssetStatus ViewStatus => _status;

  public override void LoadViewPrefab(bool async) {
    _status = EntityViewAssetStatus.Loading;
    PrefabAsset.Load(this, async);
  }

  public override void Loaded() {
    base.Loaded();
  }

  public override void PrepareAsset() {
    base.PrepareAsset();
  }

  void QuantumPrefabAsset.IListener.Error(QuantumPrefabAsset source, Exception error) {
    _status = EntityViewAssetStatus.Error;
    Debug.LogError($"Prefab load error: {error}");
  }

  void QuantumPrefabAsset.IListener.Loaded(QuantumPrefabAsset source, GameObject prefab) {
    _status = EntityViewAssetStatus.Loaded;
    base.Parent = prefab.GetComponent<global::EntityView>();
  }

  void IQuantumPrefabBakedAsset.Import(QuantumPrefabAsset prefab, IQuantumPrefabNestedAsset asset) {
    PrefabAsset = prefab;
    Settings = ((EntityViewAsset)asset).Settings;
  }

  
}
