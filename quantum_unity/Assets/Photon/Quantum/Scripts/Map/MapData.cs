using System;
using UnityEngine;
using Quantum;
using Photon.Deterministic;
using System.Collections.Generic;
using EnumFlagsAttribute = Quantum.Inspector.EnumFlagsAttribute;

[ExecuteInEditMode]
public unsafe class MapData : MonoBehaviour {
  [Flags]
  public enum DrawMode {
    PhysicsArea    = 1 << 2,
    PhysicsBuckets = 1 << 3,
    NavMeshArea    = 1 << 4,
    NavMeshGrid    = 1 << 5,
    All = PhysicsArea | PhysicsBuckets | NavMeshArea | NavMeshGrid,
  }

  public MapAsset Asset;
  
  [EnumFlags]
  public DrawMode DrawGridMode = DrawMode.All;

  [EnumFlags, HideInInspector]
  public QuantumMapDataBakeFlags BakeAllMode = QuantumMapDataBakeFlags.BakeMapData | QuantumMapDataBakeFlags.GenerateAssetDB;

  // One-to-one mapping of Quantum static collider entries in MapAsset to their original source scripts. 
  // Purely for convenience to do post bake mappings and not required by the Quantum simulation.
  public List<MonoBehaviour> StaticCollider2DReferences = new List<MonoBehaviour>();
  public List<MonoBehaviour> StaticCollider3DReferences = new List<MonoBehaviour>();
  public List<EntityView>    MapEntityReferences        = new List<EntityView>();

  void Update() {
    transform.position = Vector3.zero;
    transform.rotation = Quaternion.identity;
  }

  void OnDrawGizmos() {
    DrawGizmos(false);
  }
  
  private void OnDrawGizmosSelected() {
    DrawGizmos(true);
  }

  void DrawGizmos(bool selected) {
#if UNITY_EDITOR
    if (Asset) {
      if (DrawGridMode != default) {
        var worldSize = FPMath.Min(Asset.Settings.WorldSize, FP.UseableMax);
        var physicsArea = new FPVector2(worldSize, worldSize);

        if (Asset.Settings.SortingAxis == PhysicsCommon.SortAxis.X) {
          physicsArea.X = FPMath.Min(physicsArea.X, FP.UseableMax / 2);
        }
        else {
          physicsArea.Y = FPMath.Min(physicsArea.Y, FP.UseableMax / 2);
        }
        
        if ((DrawGridMode & DrawMode.PhysicsArea) ==  DrawMode.PhysicsArea) {
          GizmoUtils.DrawGizmosBox(transform, physicsArea.ToUnityVector3(), QuantumEditorSettings.Instance.PhysicsGridColor);
        }

        if ((DrawGridMode & DrawMode.PhysicsBuckets) ==  DrawMode.PhysicsBuckets) {
          var bottomLeft = transform.position - physicsArea.ToUnityVector3() / 2;  

          if (Asset.Settings.BucketingAxis == PhysicsCommon.BucketAxis.X) {
            var bucketSize = physicsArea.X.AsFloat / Asset.Settings.BucketsCount;
            GizmoUtils.DrawGizmoGrid(bottomLeft, Asset.Settings.BucketsCount, 1, bucketSize, physicsArea.Y.AsFloat, QuantumEditorSettings.Instance.PhysicsGridColor.Alpha(0.4f));
          }
          else {
            var bucketSize = physicsArea.Y.AsFloat / Asset.Settings.BucketsCount;
            GizmoUtils.DrawGizmoGrid(bottomLeft, 1, Asset.Settings.BucketsCount, physicsArea.X.AsFloat, bucketSize, QuantumEditorSettings.Instance.PhysicsGridColor.Alpha(0.4f));
          }
        }
        
        if ((DrawGridMode & DrawMode.NavMeshArea) ==  DrawMode.NavMeshArea) {
          GizmoUtils.DrawGizmosBox(transform, new FPVector2(Asset.Settings.WorldSizeX, Asset.Settings.WorldSizeY).ToUnityVector3(), QuantumEditorSettings.Instance.NavMeshGridColor);
        }

        if ((DrawGridMode & DrawMode.NavMeshGrid) ==  DrawMode.NavMeshGrid) {
          var bottomLeft = transform.position - (-Asset.Settings.WorldOffset).ToUnityVector3();
          GizmoUtils.DrawGizmoGrid(bottomLeft, Asset.Settings.GridSizeX, Asset.Settings.GridSizeY, Asset.Settings.GridNodeSize, QuantumEditorSettings.Instance.NavMeshGridColor.Alpha(0.4f));
        }
      }

      if (QuantumEditorSettings.Instance.DrawStaticMeshTriangles || QuantumEditorSettings.Instance.DrawStaticMeshNormals) {
        if (Asset.Settings.StaticColliders3DTriangles == null && Asset.Settings.StaticColliders3DTrianglesData.Id.IsValid) {
          BinaryData dataAsset;
          bool disposeAsset = true;

#if UNITY_EDITOR
          if (!Application.isPlaying) {
            var asset = (BinaryDataAsset)UnityDB.FindAssetForInspector(Asset.Settings.StaticColliders3DTrianglesData.Id);
            dataAsset = asset?.Settings;
            disposeAsset = false;
          } else
#endif
          {
            dataAsset = (BinaryData)UnityDB.DefaultResourceManager.GetAsset(Asset.Settings.StaticColliders3DTrianglesData.Id, mainThread: true);
          }

          if (dataAsset != null) {
            try {
              using (var stream = dataAsset.CreateByteStream()) {
                Asset.Settings.SerializeStaticColliderTriangles(stream, false);
              }
            } finally {
              if (disposeAsset) {
                UnityDB.DefaultResourceManager.DisposeAsset(Asset.Settings.StaticColliders3DTrianglesData.Id);
              }
            }
          } else {
            Log.Warn("Map cannot load triangle data file from " + Asset.Settings.StaticColliders3DTrianglesData.Id);
          }

          if (Asset.Settings.StaticColliders3DTriangles == null) {
            Asset.Settings.StaticColliders3DTriangles = new Dictionary<int, TriangleCCW[]>();
          }
        }
        
        if (Asset.Settings.StaticColliders3DTriangles != null) {
          if (QuantumEditorSettings.Instance.DrawStaticMeshTriangles) {
            foreach (var meshCollider in Asset.Settings.StaticColliders3DTriangles) {
              foreach (var tri in meshCollider.Value) {
                GizmoUtils.DrawGizmosTriangle(tri.A.ToUnityVector3(), tri.B.ToUnityVector3(), tri.C.ToUnityVector3(), selected, QuantumEditorSettings.Instance.StaticColliderColor);
              }
            }
          }
      
          if (QuantumEditorSettings.Instance.DrawStaticMeshNormals) {
            foreach (var meshCollider in Asset.Settings.StaticColliders3DTriangles) {
              foreach (var tri in meshCollider.Value) {
                Debug.DrawRay(tri.Center.ToUnityVector3(), tri.Normal.ToUnityVector3(), Color.red);
              }
            }
          }
        }
      }

      if (QuantumRunner.Default) {
        var mesh = QuantumRunner.Default?.Game?.Frames?.Verified?.Physics3D.SceneMesh;
        if (mesh != null) {
          if (QuantumEditorSettings.Instance.DrawSceneMeshCells) {
            mesh.VisitCells((x, y, z, tris, count) => {
              if (count > 0) {
                var c = mesh.GetNodeCenter(x, y, z).ToUnityVector3();
                var s = default(Vector3);
                s.x = mesh.CellSize;
                s.y = mesh.CellSize;
                s.z = mesh.CellSize;
                GizmoUtils.DrawGizmosBoxWire(c, s, QuantumEditorSettings.Instance.PhysicsGridColor);
              }
            });
          }

          if (QuantumEditorSettings.Instance.DrawSceneMeshTriangles) {
            mesh.VisitCells((x, y, z, tris, count) => {
              for (int i = 0; i < count; ++i) {
                var t = mesh.GetTriangle(tris[i]);
                Gizmos.color = QuantumEditorSettings.Instance.PhysicsGridColor;
                Gizmos.DrawLine(t->A.ToUnityVector3(), t->B.ToUnityVector3());
                Gizmos.DrawLine(t->B.ToUnityVector3(), t->C.ToUnityVector3());
                Gizmos.DrawLine(t->C.ToUnityVector3(), t->A.ToUnityVector3());
              }
            });
          }
        }
      }
    }
#endif
  }
}