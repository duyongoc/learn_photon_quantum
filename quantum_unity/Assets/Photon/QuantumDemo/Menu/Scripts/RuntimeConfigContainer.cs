using UnityEngine;

namespace Quantum.Demo {
  public class RuntimeConfigContainer : MonoBehaviour {
    [Tooltip("This RuntimeConfig is used when starting the Quantum game from the menu scene, change it here instead of inside the UIRoom source code for example.")]
    public RuntimeConfig Config;
  }
}
