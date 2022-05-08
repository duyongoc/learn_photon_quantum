using UnityEngine;

namespace Quantum {
  public static class DebugMesh {
    private static Mesh _circleMesh;
    private static Mesh _quadMesh;
    private static Mesh _cylinderMesh;
    private static Mesh _cubeMesh;
    private static Material _debugMaterial;

    public static Mesh CircleMesh {
      get {
        if (!_circleMesh) {
          _circleMesh = UnityEngine.Resources.Load<Mesh>("DEV/Mesh/CircleMesh");
        }

        return _circleMesh;
      }
    }

    public static Mesh QuadMesh {
      get {
        if (!_quadMesh) {
          _quadMesh = UnityEngine.Resources.Load<Mesh>("DEV/Mesh/QuadMesh");
        }

        return _quadMesh;
      }
    }

    public static Mesh CubeMesh {
      get {
        if (!_cubeMesh) {
          _cubeMesh = UnityEngine.Resources.Load<Mesh>("DEV/Mesh/CubeMesh");
        }

        return _cubeMesh;
      }
    }

    public static Mesh CylinderMesh {
      get {
        if (!_cylinderMesh) {
          _cylinderMesh = UnityEngine.Resources.Load<Mesh>("DEV/Mesh/CylinderMesh");
        }

        return _cylinderMesh;
      }
    }

    public static Material DebugMaterial {
      get {
        if (!_debugMaterial) {
          _debugMaterial = UnityEngine.Resources.Load<Material>("DEV/DebugDraw");
        }

        return _debugMaterial;
      }
    }
  }
}
