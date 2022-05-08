using UnityEngine;
using Photon.Deterministic;

[CreateAssetMenu(menuName = "Quantum/Configurations/DeterministicConfig", order = Quantum.EditorDefines.AssetMenuPriorityConfigurations)]
public class DeterministicSessionConfigAsset : ScriptableObject {
  public DeterministicSessionConfig Config;

  public static DeterministicSessionConfigAsset Instance {
    get {
      return Resources.Load<DeterministicSessionConfigAsset>("DeterministicConfig");
    }
  }
}
