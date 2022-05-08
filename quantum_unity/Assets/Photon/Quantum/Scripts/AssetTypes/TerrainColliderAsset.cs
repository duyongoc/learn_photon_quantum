using UnityEngine;

[CreateAssetMenu(menuName = "Quantum/Physics/Terrain Collider 3D", order = Quantum.EditorDefines.AssetMenuPriorityStart + 15 * 26 + 19)]
public partial class TerrainColliderAsset : AssetBase {
  public Quantum.TerrainCollider Settings;

  public override Quantum.AssetObject AssetObject => Settings;

  public override void Reset() {
    if (Settings == null) {
      Settings = new Quantum.TerrainCollider();
    }

    base.Reset();
  }
}

public static partial class TerrainColliderAssetExt {
  public static TerrainColliderAsset GetUnityAsset(this Quantum.TerrainCollider data) {
    return data == null ? null : UnityDB.FindAsset<TerrainColliderAsset>(data);
  }
}
