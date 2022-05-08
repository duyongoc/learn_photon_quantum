using System;
using Quantum;
using UnityEngine;
using Photon.Deterministic;

[ExecuteInEditMode]
public class QuantumStaticTerrainCollider3D : MonoBehaviour {
  public TerrainColliderAsset Asset;
  public PhysicsCommon.StaticColliderMutableMode MutableMode;
  
  [HideInInspector]
  public Boolean SmoothSphereMeshCollisions = false;
  
#pragma warning disable 618 // use of obsolete
  [HideInInspector]
  [Obsolete("Use 'MutableMode' instead.")]
  public Quantum.MapStaticCollider3D.MutableModes Mode;
#pragma warning restore 618

  public void Bake() {
#if !QUANTUM_DISABLE_TERRAIN
    FPMathUtils.LoadLookupTables();

    var t = GetComponent<Terrain>();

#if UNITY_2019_3_OR_NEWER
    Asset.Settings.Resolution = t.terrainData.heightmapResolution;
#else
    Asset.Settings.Resolution = t.terrainData.heightmapResolution;
#endif

    Asset.Settings.HeightMap = new FP[Asset.Settings.Resolution * Asset.Settings.Resolution];
    Asset.Settings.Position  = transform.position.ToFPVector3();
    Asset.Settings.Scale     = t.terrainData.heightmapScale.ToFPVector3();

    for (int i = 0; i < Asset.Settings.Resolution; i++) {
      for (int j = 0; j < Asset.Settings.Resolution; j++) {
        Asset.Settings.HeightMap[j + i * Asset.Settings.Resolution] = FP.FromFloat_UNSAFE(t.terrainData.GetHeight(i, j));
      }
    }

#if UNITY_2019_3_OR_NEWER
    // support to Terrain Paint Holes: https://docs.unity3d.com/2019.4/Documentation/Manual/terrain-PaintHoles.html
    Asset.Settings.HoleMask = new ulong[(Asset.Settings.Resolution * Asset.Settings.Resolution - 1) / 64 + 1];

    for (int i = 0; i < Asset.Settings.Resolution - 1; i++) {
      for (int j = 0; j < Asset.Settings.Resolution - 1; j++) {
        if (t.terrainData.IsHole(i, j)) {
          Asset.Settings.SetHole(i, j);
        }
      }
    }
#else
    Asset.Settings.HoleMask = null;
#endif

#if UNITY_EDITOR
    UnityEditor.EditorUtility.SetDirty(Asset);
    UnityEditor.EditorUtility.SetDirty(this);
#endif
#endif
  }
}