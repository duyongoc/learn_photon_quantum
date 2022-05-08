using UnityEngine;
using Photon.Deterministic;

[CreateAssetMenu(menuName = "Quantum/Physics/Character Controller 2D", order = Quantum.EditorDefines.AssetMenuPriorityStart + (15 * 26) + 2)]
public partial class CharacterController2DConfigAsset : AssetBase
{
  public Quantum.CharacterController2DConfig Settings;

  public override Quantum.AssetObject AssetObject => Settings;

  public override void Reset() {
    if (Settings == null) {
      Settings = new Quantum.CharacterController2DConfig();
    }

    base.Reset();
  }

  public override void Loaded() {
    Settings.PenetrationCorrection = FPMath.Clamp01(Settings.PenetrationCorrection);
  }
}

public static partial class CharacterController3DConfigAssetExt
{
  public static CharacterController2DConfigAsset GetUnityAsset(this Quantum.CharacterController2DConfig data)
  {
    return data == null ? null : UnityDB.FindAsset<CharacterController2DConfigAsset>(data);
  }
}
