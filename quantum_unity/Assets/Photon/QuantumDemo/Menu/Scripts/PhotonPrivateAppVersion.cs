using System;
using UnityEngine;

namespace Quantum.Demo {
  [CreateAssetMenu(menuName = "Quantum/Demo/PhotonPrivateAppVersion", order = EditorDefines.AssetMenuPriorityDemo)]
  public class PhotonPrivateAppVersion : ScriptableObject {
    public string Value;

    public void Reset() {
      if (string.IsNullOrEmpty(Value)) {
        Value = Guid.NewGuid().ToString();
      }
    }
  }
}
