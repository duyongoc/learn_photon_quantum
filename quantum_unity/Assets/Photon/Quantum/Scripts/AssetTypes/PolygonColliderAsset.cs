using UnityEngine;

[CreateAssetMenu(menuName = "Quantum/Physics/Polygon Collider", order = Quantum.EditorDefines.AssetMenuPriorityStart + 15 * 26 + 15)]
public partial class PolygonColliderAsset : AssetBase {
  public Quantum.PolygonCollider Settings;

  public override Quantum.AssetObject AssetObject => Settings;

  public override void Reset() {
    if (Settings == null) {
      Settings = new Quantum.PolygonCollider();
    }

    base.Reset();
  }
}

public static partial class PolygonColliderAssetExt {
  public static PolygonColliderAsset GetUnityAsset(this Quantum.PolygonCollider data) {
    return data == null ? null : UnityDB.FindAsset<PolygonColliderAsset>(data);
  }
}