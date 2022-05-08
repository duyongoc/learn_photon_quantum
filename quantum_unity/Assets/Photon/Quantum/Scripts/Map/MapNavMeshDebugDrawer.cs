using Quantum;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class MapNavMeshDebugDrawer : MonoBehaviour {
  public AssetRefBinaryData BinaryAsset;
  public bool DrawBorders = true;
  public bool DrawLinks = true;
  public bool DrawBorderNormals;
  public bool DrawVertexNormals;
  public bool DrawTriangleNeighbors;
  public bool DrawVertexIds;
  public bool DrawTrianglesIds;

#if UNITY_EDITOR

  private int MaxBordersPerCell;
  private int MaxTrianglesPerCell;
  private NavMesh _navmesh;

  void OnDrawGizmosSelected() {
    if (Selection.activeGameObject != gameObject) {
      return;
    }

    if (BinaryAsset == null) {
      return;
    }

    var originalColor = Gizmos.color;

    var asset = UnityDB.FindAsset<BinaryDataAsset>(BinaryAsset.Id);
    if (asset == null) {
      return;
    }

    var stream = new ByteStream(asset.Settings.Data);
    _navmesh = new NavMesh();
    _navmesh.Serialize(stream, false);
    _navmesh.Name = asset.Settings.Identifier.Path;

    MapNavMesh.CreateAndDrawGizmoMesh(_navmesh, NavMeshRegionMask.Default);

    var editorSettings = QuantumEditorSettings.Instance;

    if (DrawLinks) {
      for (int i = 0; i < _navmesh.Links.Length; i++) {
        Gizmos.color = Color.blue;
        GizmoUtils.DrawGizmoVector(
          _navmesh.Links[i].Start.ToUnityVector3(true), 
          _navmesh.Links[i].End.ToUnityVector3(true), 
          GizmoUtils.DefaultArrowHeadLength * editorSettings.GizmoIconScale.AsFloat);
      }
    }

    if (DrawTrianglesIds || DrawLinks || DrawTriangleNeighbors) {
      for (int i = 0; i < _navmesh.Triangles.Length; i++) {
        if (DrawTrianglesIds) {
          Handles.color = Color.white;
          Handles.Label(_navmesh.Triangles[i].Center.ToUnityVector3(true), i.ToString());
        }

        if (DrawTriangleNeighbors) {
          var t = _navmesh.Triangles[i];
          if (t.Neighbors == null)
            continue;

          foreach (var p in t.Neighbors) {
            Gizmos.color = Color.green;
            var n = _navmesh.Triangles[p.Neighbor];
            Gizmos.DrawLine(t.Center.ToUnityVector3(true), n.Center.ToUnityVector3(true));
          }
        }
      }
    }


    if (DrawVertexIds || DrawVertexNormals) {
      for (int i = 0; i < _navmesh.Vertices.Length; i++) {
        if (DrawVertexIds) {
          Handles.color = Color.green;
          Handles.Label(_navmesh.Vertices[i].Point.ToUnityVector3(true), i.ToString());
        }

        if (DrawVertexNormals) {
          FPMathUtils.LoadLookupTables();
          Gizmos.color = Color.blue;
          var p = _navmesh.Vertices[i].Point.ToUnityVector3(true);
          var normal = NavMeshVertex.CalculateNormal(i, _navmesh, new NavMeshRegionMask()).ToUnityVector3(true);
          GizmoUtils.DrawGizmoVector(p, 
            p + normal * editorSettings.GizmoIconScale.AsFloat * 0.33f,
            editorSettings.GizmoIconScale.AsFloat * 0.33f * GizmoUtils.DefaultArrowHeadLength);
        }
      }
    }

    if (DrawBorders || DrawBorderNormals) {
      Gizmos.color = Color.black;
      for (int i = 0; i < _navmesh.Borders.Length; i++) {
        if (DrawBorders) {
          Gizmos.DrawLine(_navmesh.Borders[i].V0.ToUnityVector3(true), _navmesh.Borders[i].V1.ToUnityVector3(true));
        }

        if (DrawBorderNormals) {
          var middle = (_navmesh.Borders[i].V0.ToUnityVector3(true) + _navmesh.Borders[i].V1.ToUnityVector3(true)) * 0.5f;
          GizmoUtils.DrawGizmoVector(middle, 
            middle + _navmesh.Borders[i].Normal.ToUnityVector3(true) * editorSettings.GizmoIconScale.AsFloat * 0.33f,
            editorSettings.GizmoIconScale.AsFloat * 0.33f * GizmoUtils.DefaultArrowHeadLength);
        }
      }
    }

    for (int i = 0; i < _navmesh.BorderGrid.Length; i++) {
      MaxBordersPerCell = Mathf.Max(MaxBordersPerCell, _navmesh.BorderGrid[i].Borders.Length);
    }

    for (int i = 0; i < _navmesh.TrianglesGrid.Length; i++) {
      MaxTrianglesPerCell = Mathf.Max(MaxTrianglesPerCell, _navmesh.TrianglesGrid[i].Triangles.Length);
    }

    Gizmos.color = originalColor;
  }

  static void DrawTriangle(int i, NavMesh navmesh) {
    var t = navmesh.Triangles[i];
    var vertex0 = navmesh.Vertices[t.Vertex0].Point.ToUnityVector3(true);
    var vertex1 = navmesh.Vertices[t.Vertex1].Point.ToUnityVector3(true);
    var vertex2 = navmesh.Vertices[t.Vertex2].Point.ToUnityVector3(true);
    Gizmos.DrawLine(vertex0, vertex1);
    Gizmos.DrawLine(vertex1, vertex2);
    Gizmos.DrawLine(vertex2, vertex0);
  }

  static void DrawTriangleMesh(int i, NavMesh navmesh, Color color) {
    var t = navmesh.Triangles[i];
    var vertex0 = navmesh.Vertices[t.Vertex0].Point.ToUnityVector3(true);
    var vertex1 = navmesh.Vertices[t.Vertex1].Point.ToUnityVector3(true);
    var vertex2 = navmesh.Vertices[t.Vertex2].Point.ToUnityVector3(true);
    Handles.color = color;
    Handles.lighting = true;
    Handles.DrawAAConvexPolygon(vertex0, vertex1, vertex2);

  }
#endif

#if UNITY_EDITOR

  [CustomEditor(typeof(MapNavMeshDebugDrawer))]
  private class MapNavMeshDebugDrawerEditor : Editor {
    public override void OnInspectorGUI() {
      base.OnInspectorGUI();

      var data = (MapNavMeshDebugDrawer)target;

      if (data._navmesh != null && data._navmesh.Triangles != null) {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Information", EditorStyles.boldLabel);
        GUI.enabled = false;
        EditorGUILayout.TextField("NavMesh Name", data._navmesh.Name);
        EditorGUILayout.IntField("Number Of Triangles", data._navmesh.Triangles.Length);
        EditorGUILayout.IntField("Number Of Vertices", data._navmesh.Vertices.Length);
        EditorGUILayout.IntField("Number Of Borders", data._navmesh.Borders.Length);
        EditorGUILayout.IntField("Max Borders / Cell", data.MaxBordersPerCell);
        EditorGUILayout.IntField("Max Triangles / Cell", data.MaxTrianglesPerCell);
      }
    }
  }

#endif
}
