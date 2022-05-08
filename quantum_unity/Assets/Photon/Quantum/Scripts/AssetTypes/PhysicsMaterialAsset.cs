using UnityEngine;

[CreateAssetMenu(menuName = "Quantum/Physics/Physics Material", order = Quantum.EditorDefines.AssetMenuPriorityStart + 15 * 26 + 15)]
public partial class PhysicsMaterialAsset : AssetBase {
  public Quantum.PhysicsMaterial Settings;

  public override Quantum.AssetObject AssetObject => Settings;

  public override void Reset() {
    if (Settings == null) {
      Settings = new Quantum.PhysicsMaterial();
    }

    base.Reset();
  }
}

public static partial class PhysicsMaterialAssetExt {
  public static PhysicsMaterialAsset GetUnityAsset(this Quantum.PhysicsMaterial data) {
    return data == null ? null : UnityDB.FindAsset<PhysicsMaterialAsset>(data);
  }
}
