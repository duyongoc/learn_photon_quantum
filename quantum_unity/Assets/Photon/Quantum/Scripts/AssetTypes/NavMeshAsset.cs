using UnityEngine;

[CreateAssetMenu(menuName = "Quantum/NavMeshAsset", order = Quantum.EditorDefines.AssetMenuPriorityStart + 13 * 26)]
public class NavMeshAsset : AssetBase {

  public Quantum.NavMesh Settings;

  public override Quantum.AssetObject AssetObject => Settings;

  public override void Reset() {
    if (Settings == null) {
      Settings = new Quantum.NavMesh();
    }

    base.Reset();
  }
}

public static partial class NavMeshAssetExts {
  public static NavMeshAsset GetUnityAsset(this Quantum.NavMesh data) {
    return data == null ? null : UnityDB.FindAsset<NavMeshAsset>(data);
  }
}
