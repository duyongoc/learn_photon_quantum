using System;

public interface IQuantumPrefabNestedAssetHost {
  Type NestedAssetType { get; }
  Type SplitAssetType { get; }
}