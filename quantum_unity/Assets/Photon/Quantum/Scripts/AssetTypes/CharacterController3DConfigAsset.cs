using UnityEngine;
using Photon.Deterministic;

[CreateAssetMenu(menuName = "Quantum/Physics/Character Controller 3D", order = Quantum.EditorDefines.AssetMenuPriorityStart + (15 * 26) + 2)]
public partial class CharacterController3DConfigAsset : AssetBase
{
  public Quantum.CharacterController3DConfig Settings;

  public override Quantum.AssetObject AssetObject => Settings;

  public override void Reset() {
    if (Settings == null) {
      Settings = new Quantum.CharacterController3DConfig();
    }

    base.Reset();
  }
  
  public override void Loaded() {
    Settings.PenetrationCorrection = FPMath.Clamp01(Settings.PenetrationCorrection);
  }
}

public static partial class CharacterController3DConfigAssetExt
{
  public static CharacterController3DConfigAsset GetUnityAsset(this Quantum.CharacterController3DConfig data)
  {
    return data == null ? null : UnityDB.FindAsset<CharacterController3DConfigAsset>(data);
  }
}
