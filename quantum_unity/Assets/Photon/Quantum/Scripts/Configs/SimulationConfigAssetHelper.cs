using System;
using UnityEngine;

public static class SimulationConfigAssetHelper {

  public enum PhysicsType {
    Physics3D,
    Physics2D
  }

  public static void ImportLayersFromUnity(this SimulationConfigAsset data, PhysicsType physicsType = PhysicsType.Physics3D) {
    data.Settings.Physics.Layers = GetUnityLayerNameArray();
    data.Settings.Physics.LayerMatrix = GetUnityLayerMatrix(physicsType);
  }

  public static String[] GetUnityLayerNameArray() {
    var layers = new String[32];

    for (Int32 i = 0; i < layers.Length; ++i) {
      try {
        layers[i] = LayerMask.LayerToName(i);
      }
      catch {
        // just eat exceptions
      }
    }

    return layers;
  }

  public static Int32[] GetUnityLayerMatrix(PhysicsType physicsType) {
    var matrix = new Int32[32];

    for (Int32 a = 0; a < 32; ++a) {
      for (Int32 b = 0; b < 32; ++b) {
        bool ignoreLayerCollision = false;

        switch (physicsType) {
#if !QUANTUM_DISABLE_PHYSICS3D
          case PhysicsType.Physics3D: ignoreLayerCollision = Physics.GetIgnoreLayerCollision(a, b); break;
#endif
#if !QUANTUM_DISABLE_PHYSICS2D
          case PhysicsType.Physics2D: ignoreLayerCollision = Physics2D.GetIgnoreLayerCollision(a, b); break;
#endif
          default:
            break;
        }

        if (ignoreLayerCollision == false) {
          matrix[a] |= (1 << b);
          matrix[b] |= (1 << a);
        }
      }
    }

    return matrix;
  }
}
