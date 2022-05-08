using UnityEngine;
using System.Linq;
using Photon.Deterministic;
using System;
using System.Collections.Generic;
using Quantum;

#if UNITY_EDITOR
using UnityEditor;
#endif

[System.Serializable]
public struct MapNavMeshTriangle {
  public String Id;
  public String[] VertexIds;
  public Int32[] VertexIds2;
  public Int32 Area;
  public String RegionId;
  public FP Cost;
}

[System.Serializable]
public struct MapNavMeshVertex {
  public String Id;
  public Vector3 Position;
  public List<Int32> Neighbors;
  public List<Int32> Triangles;
}

[System.Serializable]
public struct MapNavMeshLink {
  public Vector3 Start;
  public Vector3 End;
  public bool Bidirectional;
  public float CostOverride;
  public String RegionId;
  public String Name;
}

[Obsolete("Use MapNavMesh.BakeData")]
public class MapNavMeshBakeData : MapNavMesh.BakeData { }

public class MapNavMeshDefinition : MonoBehaviour {
  [System.Serializable]
  [Obsolete("Use MapNavMesh.FindClosestTriangleCalculation")]
  public enum FindClosestTriangleCalculation {
    BruteForce,
    SpiralOut
  }

  public GameObject[] NavMeshSurfaces;
  public FP AgentRadius;
  public bool WeldIdenticalVertices = true;
  public float WeldVertexEpsilon = 0.0001f;
  public bool DelaunayTriangulation = false;
  public bool DelaunayTriangulationRestrictToPlanes = false;
  public bool FixTrianglesOnEdges = true;
  public float FixTrianglesOnEdgesEpsilon = float.Epsilon;
  public bool ImportRegions = true;
  public float RegionDetectionMargin = 0.4f;
  public List<Int32> RegionAreaIds;
  public MapNavMesh.FindClosestTriangleCalculation ClosestTriangleCalculation;
  public int ClosestTriangleCalculationDepth = 3;
  public bool LinkErrorCorrection = false;
  public bool EnableQuantum_XY;

  [QuantumInspector, Quantum.Inspector.ReadOnly]
  public MapNavMeshVertex[] Vertices;

  [QuantumInspector, Quantum.Inspector.ReadOnly]
  public MapNavMeshTriangle[] Triangles;

  [QuantumInspector, Quantum.Inspector.ReadOnly]
  public List<string> Regions;

  [QuantumInspector, Quantum.Inspector.ReadOnly]
  public MapNavMeshLink[] Links;

#if UNITY_EDITOR
  private Mesh _gizmoMesh;
#endif

#if !QUANTUM_DISABLE_AI

  public static MapNavMesh.BakeData CreateBakeData(MapNavMeshDefinition definition) {
    return new MapNavMesh.BakeData {
      AgentRadius = definition.AgentRadius,
      ClosestTriangleCalculation = definition.ClosestTriangleCalculation,
      ClosestTriangleCalculationDepth = definition.ClosestTriangleCalculationDepth,
      LinkErrorCorrection = definition.LinkErrorCorrection,
      Links = definition.Links,
      Name = definition.name,
      Position = definition.transform.position,
      Regions = definition.Regions,
      Triangles = definition.Triangles,
      Vertices = definition.Vertices,
      EnableQuantum_XY = definition.EnableQuantum_XY
    };
  }

  public static MapNavMesh.ImportSettings CreateImportSettings(MapNavMeshDefinition definition) {
    return new MapNavMesh.ImportSettings {
      ClosestTriangleCalculation = definition.ClosestTriangleCalculation,
      ClosestTriangleCalculationDepth = definition.ClosestTriangleCalculationDepth,
      LinkErrorCorrection = definition.LinkErrorCorrection,
      DelaunayTriangulation = definition.DelaunayTriangulation,
      DelaunayTriangulationRestrictToPlanes = definition.DelaunayTriangulationRestrictToPlanes,
      EnableQuantum_XY = definition.EnableQuantum_XY,
      FixTrianglesOnEdges = definition.FixTrianglesOnEdges,
      FixTrianglesOnEdgesEpsilon = definition.FixTrianglesOnEdgesEpsilon,
      ImportRegions = definition.ImportRegions,
      MinAgentRadius = MapNavMesh.FindSmallestAgentRadius(definition.NavMeshSurfaces),
      RegionAreaIds = definition.RegionAreaIds,
      RegionDetectionMargin = definition.RegionDetectionMargin,
      WeldIdenticalVertices = definition.WeldIdenticalVertices,
      WeldVertexEpsilon = definition.WeldVertexEpsilon
    };
  }

#endif

  public MapNavMeshVertex GetVertex(String id) {
    for (Int32 i = 0; i < Vertices.Length; ++i) {
      if (Vertices[i].Id == id) {
        return Vertices[i];
      }
    }

    throw new System.InvalidOperationException();
  }

  public Int32 GetVertexIndex(String id) {
    return Array.FindIndex(Vertices, x => x.Id == id);
  }

  public Boolean Contains(FPVector2 point) {
    return Contains(point.ToUnityVector3());
  }

  public Boolean Contains(Vector3 point) {
    point = point - transform.position;
    for (Int32 i = 0; i < Triangles.Length; ++i) {
      var tri = Triangles[i].VertexIds.Select(x => GetVertex(x).Position).ToArray();

      var v0 = tri[2] - tri[0];
      var v1 = tri[1] - tri[0];
      var v2 = point - tri[0];

      var dot00 = Vector3.Dot(v0, v0);
      var dot01 = Vector3.Dot(v0, v1);
      var dot02 = Vector3.Dot(v0, v2);
      var dot11 = Vector3.Dot(v1, v1);
      var dot12 = Vector3.Dot(v1, v2);

      var invDenom = 1f / (dot00 * dot11 - dot01 * dot01);
      var u = (dot11 * dot02 - dot01 * dot12) * invDenom;
      var v = (dot00 * dot12 - dot01 * dot02) * invDenom;

      // check if point is in triangle
      if ((u >= 0) && (v >= 0) && (u + v < 1)) {
        return true;
      }
    }

    return false;
  }

#if UNITY_EDITOR

  public Mesh CreateMesh() {
    var mesh = new Mesh();
    mesh.vertices = Vertices.Select(x => x.Position + transform.position).ToArray();
    mesh.triangles = Triangles.SelectMany(x => x.VertexIds.Select(GetVertexIndex)).ToArray();
    mesh.RecalculateNormals();
    return mesh;
  }

  public void InvalidateGizmoMesh() {
    _gizmoMesh = null;
  }

  void OnDrawGizmos() {
    if (QuantumEditorSettings.Instance.DrawNavMeshDefinitionAlways == false)
      return;

    if (Triangles == null)
      return;

    if (QuantumEditorSettings.Instance.DrawNavMeshDefinitionOptimized)
      DrawGizmoMesh();
    else
      DrawGizmoTriangles();

    Gizmos.color = Color.white;
  }

  void OnDrawGizmosSelected() {
    if (QuantumEditorSettings.Instance.DrawNavMeshDefinitionAlways == true)
      return;

    if (Triangles == null)
      return;

    if (QuantumEditorSettings.Instance.DrawNavMeshDefinitionOptimized)
      DrawGizmoMesh();
    else
      DrawGizmoTriangles();

    Gizmos.color = Color.white;
  }

  void DrawGizmoMesh() {
    if (_gizmoMesh == null) {
      _gizmoMesh = CreateMesh();
    }

    var color = QuantumEditorSettings.Instance.NavMeshDefaultColor;

    if (QuantumEditorSettings.Instance.DrawNavMeshDefinitionMesh) {
      Gizmos.color = color;
      Gizmos.DrawMesh(_gizmoMesh);
      Gizmos.color = Color.black;
    }
    else {
      Gizmos.color = color;
    }

    Gizmos.DrawWireMesh(_gizmoMesh);
  }

  void DrawGizmoTriangles() {
    var pos = transform.position;
    foreach (var t in Triangles) {
      var v0 = GetVertex(t.VertexIds[0]).Position + pos;
      var v1 = GetVertex(t.VertexIds[1]).Position + pos;
      var v2 = GetVertex(t.VertexIds[2]).Position + pos;

      var color = QuantumEditorSettings.Instance.GetNavMeshColor(string.IsNullOrEmpty(t.RegionId) ? new Quantum.NavMeshRegionMask() : Quantum.NavMeshRegionMask.Default);

      if (QuantumEditorSettings.Instance.DrawNavMeshDefinitionMesh) {
        Handles.color = color;
        Handles.lighting = true;
        Handles.DrawAAConvexPolygon(v0, v1, v2);
        Gizmos.color = Color.black;
      }
      else {
        Gizmos.color = color;
      }

      Gizmos.DrawLine(v0, v1);
      Gizmos.DrawLine(v1, v2);
      Gizmos.DrawLine(v2, v0);
    }
  }
#endif
}
