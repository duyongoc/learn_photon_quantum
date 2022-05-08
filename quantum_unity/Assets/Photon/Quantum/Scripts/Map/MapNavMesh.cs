using Photon.Deterministic;
using Quantum;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

#if !QUANTUM_DISABLE_AI
using UnityEngine.AI;
#endif

public static class MapNavMesh {
  [Serializable]
  public enum FindClosestTriangleCalculation {
    BruteForce,
    SpiralOut
  }

  [Serializable]
  public class BakeData {
    public string Name;
    public Vector3 Position;
    public FP AgentRadius;
    public List<string> Regions;
    public MapNavMeshVertex[] Vertices;
    public MapNavMeshTriangle[] Triangles;
    public MapNavMeshLink[] Links;
    public FindClosestTriangleCalculation ClosestTriangleCalculation;
    public int ClosestTriangleCalculationDepth;
    public bool EnableQuantum_XY;
    public bool LinkErrorCorrection;
  }

  #region Importing From Unity

  public static float DefaultMinAgentRadius = 0.25f;

  [Serializable]
  public class ImportSettings {
    [Tooltip("The Unity NavMesh is a collection of non - connected triangles, this option is very important and combines shared vertices.")]
    public bool WeldIdenticalVertices = true;
    [Tooltip("Don't make the epsilon too small, vertices to fuse are missed, also don't make the value too big as it will deform your navmesh.")]
    public float WeldVertexEpsilon = 0.0001f;
    [Tooltip("Post processes imported Unity navmesh with a Delaunay triangulation to reduce long triangles.")]
    public bool DelaunayTriangulation = false;
    [Tooltip("In 3D the triangulation can deform the navmesh on slopes, check this option to restrict the triangulation to triangles that lie in the same plane.")]
    public bool DelaunayTriangulationRestrictToPlanes = false;
    [Tooltip("Sometimes vertices are lying on other triangle edges, this will lead to unwanted borders being detected, this option splits those vertices.")]
    public bool FixTrianglesOnEdges = true;
    [Tooltip("Larger scaled navmeshes may require to increase this value (e.g. 0.001) when false-positive borders are detected.")]
    public float FixTrianglesOnEdgesEpsilon = float.Epsilon;
    [Tooltip("Automatically correct navmesh link position to the closest triangle (default is off).")]
    public bool LinkErrorCorrection = false;
    [Tooltip("SpiralOut will be considerably faster but fallback triangles can be null.")]
    public FindClosestTriangleCalculation ClosestTriangleCalculation = FindClosestTriangleCalculation.SpiralOut;
    [Tooltip("Number of cells to search triangles in neighbors.")]
    public int ClosestTriangleCalculationDepth = 3;
    [Tooltip("Activate this and the navmesh baking will flip Y and Z to support navmeshes generated in the XY plane.")]
    public bool EnableQuantum_XY;
    [Tooltip("The agent radius that the navmesh is build for. The value is retrieved from Unity settings when baking in Editor.")]
    public FP MinAgentRadius = FP._0_25;
    [Tooltip("Toggle the Quantum region import.")]
    public bool ImportRegions = true;
    [Tooltip("The artificial margin is necessary because the Unity NavMesh does not fit the source size very well. The value is added to the navmesh area and checked against all Quantum Region scripts to select the correct region id.")]
    public float RegionDetectionMargin = 0.4f;
    public List<Int32> RegionAreaIds;
  }

#if !QUANTUM_DISABLE_AI

  public static class ImportUtils {
    public static void WeldIdenticalVertices(ref MapNavMeshVertex[] vertices, ref MapNavMeshTriangle[] triangles, float cleanupEpsilon, Action<float> reporter) {
      int[] vertexRemapTable = new int[vertices.Length];
      for (int i = 0; i < vertexRemapTable.Length; ++i) {
        vertexRemapTable[i] = i;
      }

      for (int i = 0; i < vertices.Length; ++i) {
        reporter.Invoke(i / (float)vertices.Length);
        Vector3 v = vertices[i].Position;

        for (int j = i + 1; j < vertices.Length; ++j) {
          if (j != vertexRemapTable[j]) {
            continue;
          }

          Vector3 v2 = vertices[j].Position;
          if (Mathf.Abs(Vector3.SqrMagnitude(v2 - v)) <= cleanupEpsilon) {
            vertexRemapTable[j] = i;
          }
        }
      }

      for (int i = 0; i < triangles.Length; ++i) {
        for (int v = 0; v < 3; v++) {
          triangles[i].VertexIds2[v] =
            vertexRemapTable[triangles[i].VertexIds2[v]];
        }
      }
    }

    public static void RemoveUnusedVertices(ref MapNavMeshVertex[] vertices, ref MapNavMeshTriangle[] triangles, Action<float> reporter) {
      List<MapNavMeshVertex> newVertices = new List<MapNavMeshVertex>();
      int[] remapArray = new int[vertices.Length];
      for (int i = 0; i < remapArray.Length; ++i) {
        remapArray[i] = -1;
      }

      for (int t = 0; t < triangles.Length; ++t) {
        reporter.Invoke(t / (float)triangles.Length);
        for (int v = 0; v < 3; v++) {
          int newIndex = remapArray[triangles[t].VertexIds2[v]];
          if (newIndex < 0) {
            newIndex = newVertices.Count;
            remapArray[triangles[t].VertexIds2[v]] = newIndex;
            newVertices.Add(vertices[triangles[t].VertexIds2[v]]);
          }
          triangles[t].VertexIds2[v] = newIndex;
        }
      }

      //Debug.Log("Removed Unused Vertices: " + (vertices.Length - newVertices.Count));

      vertices = newVertices.ToArray();
    }

    public static void ImportRegions(Scene scene, ref MapNavMeshVertex[] vertices, ref MapNavMeshTriangle[] triangles, int t, ref List<string> regionMap, float regionDetectionMargin) {
      // Expand the triangle until we have an isolated island containing all connected triangles of the same region
      HashSet<int> island = new HashSet<int>();
      HashSet<int> verticies = new HashSet<int>();
      island.Add(t);
      verticies.Add(triangles[t].VertexIds2[0]);
      verticies.Add(triangles[t].VertexIds2[1]);
      verticies.Add(triangles[t].VertexIds2[2]);
      bool isIslandComplete = false;
      while (!isIslandComplete) {
        isIslandComplete = true;
        for (int j = 0; j < triangles.Length; j++) {
          if (triangles[t].Area == triangles[j].Area && !island.Contains(j)) {
            for (int v = 0; v < 3; v++) {
              if (verticies.Contains(triangles[j].VertexIds2[v])) {
                island.Add(j);
                verticies.Add(triangles[j].VertexIds2[0]);
                verticies.Add(triangles[j].VertexIds2[1]);
                verticies.Add(triangles[j].VertexIds2[2]);
                isIslandComplete = false;
                break;
              }
            }
          }
        }
      }

      // Go through all MapNavMeshRegion scripts in the scene and check if all vertices of the islands
      // are within its bounds. Use the smallest possible bounds/region found. Use the RegionIndex from that for all triangles.
      if (island.Count > 0) {
        string regionId = string.Empty;
        FP regionCost = FP._1;
        float smallestRegionBounds = float.MaxValue;
        var regions = MapDataBaker.FindLocalObjects<MapNavMeshRegion>(scene);
        foreach (var region in regions) {
          if (region.CastRegion != MapNavMeshRegion.RegionCastType.CastRegion) {
            continue;
          }

          var meshRenderer = region.gameObject.GetComponent<MeshRenderer>();
          if (meshRenderer == null) {
            Debug.LogErrorFormat("MeshRenderer missing on MapNavMeshRegion object {0} with active RegionCasting", region.name);
          }
          else {
            Bounds bounds = region.gameObject.GetComponent<MeshRenderer>().bounds;
            // Grow the bounds, because the generated map is not exact
            bounds.Expand(regionDetectionMargin);
            bool isInsideBounds = true;
            foreach (var triangleIndex in island) {
              for (int v = 0; v < 3; v++) {
                Vector3 position = vertices[triangles[triangleIndex].VertexIds2[v]].Position;
                if (bounds.Contains(position) == false) {
                  isInsideBounds = false;
                  break;
                }
              }
            }

            if (isInsideBounds) {
              float size = bounds.extents.sqrMagnitude;
              if (size < smallestRegionBounds) {
                smallestRegionBounds = size;
                regionId = region.Id;
                regionCost = region.Cost;

                if (region.OverwriteCost == false) {
                  // Grab the most recent area cost from Unity (ignore the one in the scene)
                  regionCost = UnityEngine.AI.NavMesh.GetAreaCost(triangles[t].Area).ToFP();
                }
              }
            }
          }
        }

        // Save the toggle region index on the triangles imported from Unity
        if (string.IsNullOrEmpty(regionId) == false) {
          if (regionMap.Contains(regionId) == false) {

            if (regionMap.Count >= Navigation.Constants.MaxRegions) {
              // Still add to region map, but it won't be set on the triangles.
              Debug.LogErrorFormat("Failed to create region '{0}' because Quantum max region ({1}) reached. Reduce the number of regions.", regionId, Navigation.Constants.MaxRegions);
            }

            regionMap.Add(regionId);
          }


          foreach (var triangleIndex in island) {
            triangles[triangleIndex].RegionId = regionId;
            triangles[triangleIndex].Cost = regionCost;
          }
        }
        else {
          Debug.LogWarningFormat("A triangle island (count = {0}) can not be matched with any region bounds, try to increase the RegionDetectionMargin.\n Triangle Ids: {1}", island.Count, String.Join(", ", island.Select(sdfdsf => sdfdsf.ToString()).ToArray()));
        }
      }
    }

    public static void FixTrianglesOnEdges(ref MapNavMeshVertex[] vertices, ref MapNavMeshTriangle[] triangles, int t, int v0, float epsilon) {
      int v1 = (v0 + 1) % 3;
      int vOther;
      int otherTriangle = FindTriangleOnEdge(ref vertices, ref triangles, t, triangles[t].VertexIds2[v0], triangles[t].VertexIds2[v1], epsilon, out vOther);
      if (otherTriangle >= 0) {
        SplitTriangle(ref triangles, t, v0, triangles[otherTriangle].VertexIds2[vOther]);
        //Debug.LogFormat("Split triangle {0} at position {1}", t, vertices[triangles[otherTriangle].VertexIds2[vOther]].Position);
      }
    }

    public static int FindTriangleOnEdge(ref MapNavMeshVertex[] vertices, ref MapNavMeshTriangle[] triangles, int tri, int v0, int v1, float epsilon, out int triangleVertexIndex) {
      triangleVertexIndex = -1;
      for (int t = 0; t < triangles.Length; ++t) {
        if (t == tri) {
          continue;
        }

        // Triangle shares at least one vertex?
        if (triangles[t].VertexIds2[0] == v0 || triangles[t].VertexIds2[1] == v0 ||
            triangles[t].VertexIds2[2] == v0 || triangles[t].VertexIds2[0] == v1 ||
            triangles[t].VertexIds2[1] == v1 || triangles[t].VertexIds2[2] == v1) {

          if (triangles[t].VertexIds2[0] == v0 || triangles[t].VertexIds2[1] == v0 || triangles[t].VertexIds2[2] == v0) {
            if (triangles[t].VertexIds2[0] == v1 || triangles[t].VertexIds2[1] == v1 || triangles[t].VertexIds2[2] == v1) {
              // Triangle shares two vertices, not interested in that
              return -1;
            }
          }

          if (IsPointBetween(vertices[triangles[t].VertexIds2[0]].Position, vertices[v0].Position, vertices[v1].Position, epsilon)) {
            // Returns the triangle that has a vertex on the provided segment and the vertex index that lies on it
            triangleVertexIndex = 0;
            return t;
          }

          if (IsPointBetween(vertices[triangles[t].VertexIds2[1]].Position, vertices[v0].Position, vertices[v1].Position, epsilon)) {
            triangleVertexIndex = 1;
            return t;
          }

          if (IsPointBetween(vertices[triangles[t].VertexIds2[2]].Position, vertices[v0].Position, vertices[v1].Position, epsilon)) {
            triangleVertexIndex = 2;
            return t;
          }

        }
      }

      return -1;
    }

    public static bool IsPointBetween(Vector3 p, Vector3 v0, Vector3 v1, float epsilon) {
      // We don't want to compare end points only is p is "really" in between
      if (p == v0 || p == v1 || v0 == v1)
        return false;

      var p0 = Vector3.Distance(p, v0);
      var p1 = Vector3.Distance(p, v1);
      var v = Vector3.Distance(v0, v1);
      return Mathf.Abs(p0 + p1 - v) < epsilon;
    }

    public static void SplitTriangle(ref MapNavMeshTriangle[] triangles, int t, int v0, int vNew) {
      // Split edge is between vertex index 0 and 1
      int v1 = (v0 + 1) % 3;
      // Vertex index 2 is opposite of split edge
      int v2 = (v0 + 2) % 3;

      MapNavMeshTriangle newTriangle = new MapNavMeshTriangle {
        Area = triangles[t].Area,
        RegionId = triangles[t].RegionId,
        Cost = triangles[t].Cost,
        VertexIds2 = new int[3]
      };

      // Map new triangle
      newTriangle.VertexIds2[0] = vNew;
      newTriangle.VertexIds2[1] = triangles[t].VertexIds2[v1];
      newTriangle.VertexIds2[2] = triangles[t].VertexIds2[v2];
      ArrayUtils.Add(ref triangles, newTriangle);

      // Remap old triangle
      triangles[t].VertexIds2[v1] = vNew;
    }
  }

  public static BakeData ImportFromUnity(Scene scene, ImportSettings settings, string name) {
    var result = new BakeData();

    using (var progressBar = new ProgressBar("Importing Unity NavMesh", true)) {

      progressBar.Info = "Calculate Triangulation";
      var unityNavMeshTriangulation = UnityEngine.AI.NavMesh.CalculateTriangulation();

      if (unityNavMeshTriangulation.vertices.Length == 0) {
        Debug.LogError("Unity NavMesh not found");
        return null;
      }

      progressBar.Info = "Loading Vertices";
      MapNavMeshVertex[] Vertices = new MapNavMeshVertex[unityNavMeshTriangulation.vertices.Length];
      for (int i = 0; i < Vertices.Length; ++i) {
        progressBar.Progress = i / (float)Vertices.Length;
        Vertices[i].Position = unityNavMeshTriangulation.vertices[i];
        Vertices[i].Neighbors = new List<int>();
        Vertices[i].Triangles = new List<int>();
      }

      progressBar.Info = "Loading Triangles";
      int triangleCount = unityNavMeshTriangulation.indices.Length / 3;
      MapNavMeshTriangle[] Triangles = new MapNavMeshTriangle[triangleCount];
      for (int i = 0; i < triangleCount; ++i) {
        progressBar.Progress = i / (float)triangleCount;
        int area = unityNavMeshTriangulation.areas[i];
        int baseIndex = i * 3;
        Triangles[i] = new MapNavMeshTriangle() {
          VertexIds2 = new int[] {
              unityNavMeshTriangulation.indices[baseIndex + 0],
              unityNavMeshTriangulation.indices[baseIndex + 1],
              unityNavMeshTriangulation.indices[baseIndex + 2] },
          Area = area,
          RegionId = null,
          Cost = FP._1
        };
      }

      // Weld vertices
      if (settings.WeldIdenticalVertices) {
        progressBar.Info = "Welding Identical Vertices";
        ImportUtils.WeldIdenticalVertices(ref Vertices, ref Triangles, settings.WeldVertexEpsilon, p => progressBar.Progress = p);

        progressBar.Info = "Removing Unused Vertices";
        ImportUtils.RemoveUnusedVertices(ref Vertices, ref Triangles, p => progressBar.Progress = p);
      }

      // Merge vertices that lie on triangle edges
      if (settings.FixTrianglesOnEdges) {
        progressBar.Info = "Fixing Triangles On Edges";
        for (int t = 0; t < Triangles.Length; ++t) {
          progressBar.Progress = t / (float)Triangles.Length;
          for (int v = 0; v < 3; ++v) {
            ImportUtils.FixTrianglesOnEdges(ref Vertices, ref Triangles, t, v, settings.FixTrianglesOnEdgesEpsilon);
          }
        }

        progressBar.Info = "Removing Unused Vertices";
        ImportUtils.RemoveUnusedVertices(ref Vertices, ref Triangles, p => progressBar.Progress = p);
      }

      if (settings.DelaunayTriangulation) {
        progressBar.Info = "Delaunay Triangulation";
        progressBar.Progress = 0;
        var progressStep = 0.1f / (float)Triangles.Length;

        var triangles = new List<DelaunayTriangulation.Triangle>();

        for (int i = 0; i < Triangles.Length; i++) {
          progressBar.Progress += progressStep;
          triangles.Add(new DelaunayTriangulation.Triangle {
            v1 = new DelaunayTriangulation.HalfEdgeVertex(Vertices[Triangles[i].VertexIds2[0]].Position, Triangles[i].VertexIds2[0]),
            v2 = new DelaunayTriangulation.HalfEdgeVertex(Vertices[Triangles[i].VertexIds2[1]].Position, Triangles[i].VertexIds2[1]),
            v3 = new DelaunayTriangulation.HalfEdgeVertex(Vertices[Triangles[i].VertexIds2[2]].Position, Triangles[i].VertexIds2[2]),
            t = i
          });
        }

        progressBar.Progress = 0.1f;
        triangles = DelaunayTriangulation.TriangulateByFlippingEdges(triangles, settings.DelaunayTriangulationRestrictToPlanes, () => progressBar.Progress = (Mathf.Min(progressBar.Progress + 0.1f, 0.9f)));

        progressBar.Progress = 0.9f;
        foreach (var t in triangles) {
          progressBar.Progress += progressStep;
          Triangles[t.t].VertexIds2[0] = t.v1.index;
          Triangles[t.t].VertexIds2[1] = t.v2.index;
          Triangles[t.t].VertexIds2[2] = t.v3.index;
        }

        progressBar.Progress = 1;
      }

      // Import regions
      List<string> regions = new List<string>();
      if (settings.ImportRegions) {
        progressBar.Info = "Importing Regions";
        for (int t = 0; t < Triangles.Length; t++) {
          progressBar.Progress = t / (float)Triangles.Length;
          if (settings.RegionAreaIds != null && settings.RegionAreaIds.Contains(Triangles[t].Area) && string.IsNullOrEmpty(Triangles[t].RegionId)) {
            ImportUtils.ImportRegions(scene, ref Vertices, ref Triangles, t, ref regions, settings.RegionDetectionMargin);
          }
        }
      }

      // Set all vertex string ids (to work with manual editor)
      {
        progressBar.Info = "Finalizing Triangles";
        for (int t = 0; t < Triangles.Length; ++t) {
          Triangles[t].VertexIds = new string[3];
          for (int v = 0; v < 3; v++) {
            Triangles[t].VertexIds[v] = Triangles[t].VertexIds2[v].ToString();
          }
        }

        progressBar.Info = "Finalizing Vertices";
        progressBar.Progress = 0.5f;
        for (int v = 0; v < Vertices.Length; v++) {
          Vertices[v].Id = v.ToString();
        }
      }

      var links = MapDataBaker.FindLocalObjects<OffMeshLink>(scene);
      result.Links = new MapNavMeshLink[0];
      foreach (var link in links) {
        if (link.startTransform == null || link.endTransform == null) {
          Debug.LogErrorFormat(link, "Failed to import Off Mesh Link '{0}' start or end transforms are invalid.", link.name);
          continue;
        }

        var navMeshRegion = link.GetComponent<MapNavMeshRegion>();
        var regionId = navMeshRegion != null && string.IsNullOrEmpty(navMeshRegion.Id) == false ? navMeshRegion.Id : string.Empty;
        if (string.IsNullOrEmpty(regionId) == false && regions.Contains(regionId) == false) {
          // Add new region to global list
          regions.Add(regionId);
        }

        // Add link
        ArrayUtils.Add(ref result.Links, new MapNavMeshLink {
          Start = link.startTransform.position,
          End = link.endTransform.position,
          Bidirectional = link.biDirectional,
          CostOverride = link.costOverride,
          RegionId = regionId,
          Name = link.name
        });
      }

      result.Vertices = Vertices.ToArray();
      result.Triangles = Triangles.ToArray();

      regions.Sort((a, b) => string.CompareOrdinal(a, b));
      result.Regions = regions;

      Debug.LogFormat("Imported Unity NavMesh '{0}', cleaned up {1} vertices, found {2} region(s), found {3} link(s)", name, unityNavMeshTriangulation.vertices.Length - Vertices.Length, result.Regions.Count, result.Links.Length);
    }

    return result;
  }
  
  public static FP FindSmallestAgentRadius(GameObject[] navmeshSurfaces) {
    if (MapNavMesh.NavMeshSurfaceType != null && navmeshSurfaces != null) {
      // Try Unity Navmesh Surface tool
      float agentRadius = float.MaxValue;
      foreach (var surface in navmeshSurfaces) {
        var surfaceComponent = surface.GetComponent(MapNavMesh.NavMeshSurfaceType);
        if (surfaceComponent == null) {
          Debug.LogErrorFormat("No NavMeshSurface found on '{0}'", surface.name);
        }
        else {
          var agentTypeID = (int)MapNavMesh.NavMeshSurfaceType.GetField("m_AgentTypeID", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(surfaceComponent);
          if (agentTypeID != -1) {
            var settings = UnityEngine.AI.NavMesh.GetSettingsByID(agentTypeID);
            if (settings.agentRadius < agentRadius) {
              agentRadius = settings.agentRadius;
            }
          }
        }
      }

      if (agentRadius < float.MaxValue) {
        return FP.FromFloat_UNSAFE(agentRadius);
      }
    }

    return FP.FromFloat_UNSAFE(DefaultMinAgentRadius);
  }

#endif

  #endregion

  #region Gizmos

  private struct GizmoNavmeshData {
    public Mesh GizmoMesh;
    public NavMeshRegionMask CurrentRegionMask;
  }

  private static Dictionary<string, GizmoNavmeshData> _navmeshGizmoMap;

  public static void InvalidateGizmos() {
    if (_navmeshGizmoMap != null) {
      _navmeshGizmoMap.Clear();
    }
  }

  /// <summary>
  /// Creates a Unity mesh from the navmesh data and renders it as a gizmo. Uses submeshes to draw main mesh, regions and deactivated regions in different colors.
  /// The meshes are cached in a static dictionary by their NavMesh.Name. Call InvalidateGizmos() to reset the cache manually.
  /// New meshes are created when the region mask changed.
  /// </summary>
  public static void CreateAndDrawGizmoMesh(Quantum.NavMesh navmesh, NavMeshRegionMask regionMask) {
    var mesh = CreateGizmoMesh(navmesh, regionMask);
    DrawGizmoMesh(mesh);
  }

  public static Mesh CreateGizmoMesh(Quantum.NavMesh navmesh, NavMeshRegionMask regionMask) {
    if (_navmeshGizmoMap == null) {
      _navmeshGizmoMap = new Dictionary<string, GizmoNavmeshData>();
    }

    if (!_navmeshGizmoMap.TryGetValue(navmesh.Name, out GizmoNavmeshData gizmoNavmeshData) || 
      gizmoNavmeshData.CurrentRegionMask.Equals(regionMask) == false ||
      gizmoNavmeshData.GizmoMesh == null) {

      var mesh = new Mesh();
      mesh.subMeshCount = 3;

#if QUANTUM_XY
      mesh.vertices = navmesh.Vertices.Select(x => new Vector3(x.Point.X.AsFloat, x.Point.Z.AsFloat, x.Point.Y.AsFloat)).ToArray();
#else
      mesh.vertices = navmesh.Vertices.Select(x => x.Point.ToUnityVector3()).ToArray();
#endif

      mesh.SetTriangles(navmesh.Triangles.SelectMany(x => x.Regions.IsMainArea ? new int[] { x.Vertex0, x.Vertex1, x.Vertex2 } : new int[0]).ToArray(), 0);
      mesh.SetTriangles(navmesh.Triangles.SelectMany(x => x.Regions.HasValidRegions && x.Regions.IsSubset(regionMask) ? new int[] { x.Vertex0, x.Vertex1, x.Vertex2 } : new int[0]).ToArray(), 1);
      mesh.SetTriangles(navmesh.Triangles.SelectMany(x => x.Regions.HasValidRegions && !x.Regions.IsSubset(regionMask) ? new int[] { x.Vertex0, x.Vertex1, x.Vertex2 } : new int[0]).ToArray(), 2);
      mesh.RecalculateNormals();

      gizmoNavmeshData = new GizmoNavmeshData() { GizmoMesh = mesh, CurrentRegionMask = regionMask };
      _navmeshGizmoMap[navmesh.Name] = gizmoNavmeshData;
    }

    return gizmoNavmeshData.GizmoMesh;
  }

  public static void DrawGizmoMesh(Mesh mesh) {
    var originalColor = Gizmos.color;
    Gizmos.color = QuantumEditorSettings.Instance.NavMeshDefaultColor;
    Gizmos.DrawMesh(mesh, 0);
    Gizmos.color = Gizmos.color.Alpha(Gizmos.color.a * 0.75f);
    Gizmos.DrawWireMesh(mesh, 0);
    Gizmos.color = QuantumEditorSettings.Instance.NavMeshRegionColor;
    Gizmos.DrawMesh(mesh, 1);
    Gizmos.color = Gizmos.color.Alpha(Gizmos.color.a * 0.75f);
    Gizmos.DrawWireMesh(mesh, 1);
    var greyValue = (Gizmos.color.r + Gizmos.color.g + Gizmos.color.b) / 3.0f;
    Gizmos.color = new Color(greyValue, greyValue, greyValue, Gizmos.color.a);
    Gizmos.DrawMesh(mesh, 2);
    Gizmos.DrawWireMesh(mesh, 2);
    Gizmos.color = Gizmos.color.Alpha(Gizmos.color.a * 0.75f);
    Gizmos.color = originalColor;
  }

  #endregion

  #region Delaunay Triangulation

#if !QUANTUM_DISABLE_AI

  //MIT License
  //Copyright(c) 2020 Erik Nordeus
  //Permission is hereby granted, free of charge, to any person obtaining a copy
  //of this software and associated documentation files (the "Software"), to deal
  //in the Software without restriction, including without limitation the rights
  //to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
  //copies of the Software, and to permit persons to whom the Software is
  //furnished to do so, subject to the following conditions:
  //The above copyright notice and this permission notice shall be included in all
  //copies or substantial portions of the Software.
  //THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
  //IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
  //FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
  //AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
  //LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
  //OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
  //SOFTWARE.
  public static class DelaunayTriangulation {

    //An edge going in a direction
    public class HalfEdge {

      //The vertex it points to
      public HalfEdgeVertex v;

      //The next half-edge inside the face (ordered clockwise)
      //The document says counter-clockwise but clockwise is easier because that's how Unity is displaying triangles
      public HalfEdge nextEdge;

      //The opposite half-edge belonging to the neighbor
      public HalfEdge oppositeEdge;

      //(optionally) the previous halfedge in the face
      //If we assume the face is closed, then we could identify this edge by walking forward
      //until we reach it
      public HalfEdge prevEdge;

      public Triangle t;

      public HalfEdge(HalfEdgeVertex v) {
        this.v = v;
      }
    }

    public class HalfEdgeVertex {
      //The position of the vertex
      public Vector3 position;

      public int index;

      //Each vertex references an half-edge that starts at this point
      //Might seem strange because each halfEdge references a vertex the edge is going to?
      public HalfEdge edge;

      public HalfEdgeVertex(Vector3 position, int index) {
        this.position = position;
        this.index = index;
      }
    }

    //To store triangle data to get cleaner code
    public class Triangle {
      //Corners of the triangle
      public HalfEdgeVertex v1, v2, v3;
      public int t;

      public HalfEdge edge;

      public void ChangeOrientation() {
        var temp = v1;
        v1 = v2;
        v2 = temp;
      }
    }


    //Alternative 1. Triangulate with some algorithm - then flip edges until we have a delaunay triangulation
    public static List<Triangle> TriangulateByFlippingEdges(List<Triangle> triangles, bool retrictToPlanes, Action reporter) {
      // Change the structure from triangle to half-edge to make it faster to flip edges
      List<HalfEdge> halfEdges = TransformFromTriangleToHalfEdge(triangles);

      //Flip edges until we have a delaunay triangulation
      int safety = 0;
      int flippedEdges = 0;
      while (true) {
        safety += 1;

        if (safety > 100000) {
          Debug.Log("Stuck in endless loop");

          break;
        }

        bool hasFlippedEdge = false;

        //Search through all edges to see if we can flip an edge
        for (int i = 0; i < halfEdges.Count; i++) {
          HalfEdge thisEdge = halfEdges[i];

          //Is this edge sharing an edge, otherwise its a border, and then we cant flip the edge
          if (thisEdge.oppositeEdge == null) {
            continue;
          }

          //The vertices belonging to the two triangles, c-a are the edge vertices, b belongs to this triangle
          var a = thisEdge.v;
          var b = thisEdge.nextEdge.v;
          var c = thisEdge.prevEdge.v;
          var d = thisEdge.oppositeEdge.nextEdge.v;

          if (retrictToPlanes) {
            // Both triangles must be in one plane
            var plane = new UnityEngine.Plane(a.position, b.position, c.position);
            var isOnPlane = Mathf.Abs(plane.GetDistanceToPoint(d.position));
            if (isOnPlane > float.Epsilon) {
              continue;
            }
          }

          Vector2 aPos = new Vector2(a.position.x, a.position.z);
          Vector2 bPos = new Vector2(b.position.x, b.position.z);
          Vector2 cPos = new Vector2(c.position.x, c.position.z);
          Vector2 dPos = new Vector2(d.position.x, d.position.z);

          //Use the circle test to test if we need to flip this edge
          if (IsPointInsideOutsideOrOnCircle(aPos, bPos, cPos, dPos) < 0f) {
            //Are these the two triangles that share this edge forming a convex quadrilateral?
            //Otherwise the edge cant be flipped
            if (IsQuadrilateralConvex(aPos, bPos, cPos, dPos)) {
              //If the new triangle after a flip is not better, then dont flip
              //This will also stop the algoritm from ending up in an endless loop
              if (IsPointInsideOutsideOrOnCircle(bPos, cPos, dPos, aPos) < 0f) {
                continue;
              }

              //Flip the edge
              flippedEdges += 1;

              hasFlippedEdge = true;

              FlipEdge(thisEdge);
            }
          }
        }

        reporter.Invoke();

        //We have searched through all edges and havent found an edge to flip, so we have a Delaunay triangulation!
        if (!hasFlippedEdge) {
          //Debug.Log("Found a delaunay triangulation");
          break;
        }
      }

      Debug.Log("Delaunay triangulation flipped edges: " + flippedEdges);

      //Dont have to convert from half edge to triangle because the algorithm will modify the objects, which belongs to the 
      //original triangles, so the triangles have the data we need

      return triangles;
    }

    //From triangle where each triangle has one vertex to half edge
    private static List<HalfEdge> TransformFromTriangleToHalfEdge(List<Triangle> triangles) {
      //Make sure the triangles have the same orientation
      OrientTrianglesClockwise(triangles);

      //First create a list with all possible half-edges
      List<HalfEdge> halfEdges = new List<HalfEdge>(triangles.Count * 3);

      for (int i = 0; i < triangles.Count; i++) {
        Triangle t = triangles[i];

        HalfEdge he1 = new HalfEdge(t.v1);
        HalfEdge he2 = new HalfEdge(t.v2);
        HalfEdge he3 = new HalfEdge(t.v3);

        he1.nextEdge = he2;
        he2.nextEdge = he3;
        he3.nextEdge = he1;

        he1.prevEdge = he3;
        he2.prevEdge = he1;
        he3.prevEdge = he2;

        //The vertex needs to know of an edge going from it
        he1.v.edge = he2;
        he2.v.edge = he3;
        he3.v.edge = he1;

        //The face the half-edge is connected to
        t.edge = he1;

        he1.t = t;
        he2.t = t;
        he3.t = t;

        //Add the half-edges to the list
        halfEdges.Add(he1);
        halfEdges.Add(he2);
        halfEdges.Add(he3);
      }

      //Find the half-edges going in the opposite direction
      for (int i = 0; i < halfEdges.Count; i++) {
        HalfEdge he = halfEdges[i];

        var goingToVertex = he.v;
        var goingFromVertex = he.prevEdge.v;

        for (int j = 0; j < halfEdges.Count; j++) {
          //Dont compare with itself
          if (i == j) {
            continue;
          }

          HalfEdge heOpposite = halfEdges[j];

          //Is this edge going between the vertices in the opposite direction
          if (goingFromVertex.position == heOpposite.v.position && goingToVertex.position == heOpposite.prevEdge.v.position) {
            he.oppositeEdge = heOpposite;

            break;
          }
        }
      }


      return halfEdges;
    }

    //Orient triangles so they have the correct orientation
    private static void OrientTrianglesClockwise(List<Triangle> triangles) {
      for (int i = 0; i < triangles.Count; i++) {
        Triangle tri = triangles[i];

        Vector2 v1 = new Vector2(tri.v1.position.x, tri.v1.position.z);
        Vector2 v2 = new Vector2(tri.v2.position.x, tri.v2.position.z);
        Vector2 v3 = new Vector2(tri.v3.position.x, tri.v3.position.z);

        if (!IsTriangleOrientedClockwise(v1, v2, v3)) {
          tri.ChangeOrientation();
        }
      }
    }

    //Is a triangle in 2d space oriented clockwise or counter-clockwise
    //https://math.stackexchange.com/questions/1324179/how-to-tell-if-3-connected-points-are-connected-clockwise-or-counter-clockwise
    //https://en.wikipedia.org/wiki/Curve_orientation
    private static bool IsTriangleOrientedClockwise(Vector2 p1, Vector2 p2, Vector2 p3) {
      bool isClockWise = true;

      float determinant = p1.x * p2.y + p3.x * p1.y + p2.x * p3.y - p1.x * p3.y - p3.x * p2.y - p2.x * p1.y;

      if (determinant > 0f) {
        isClockWise = false;
      }

      return isClockWise;
    }

    //Is a point d inside, outside or on the same circle as a, b, c
    //https://gamedev.stackexchange.com/questions/71328/how-can-i-add-and-subtract-convex-polygons
    //Returns positive if inside, negative if outside, and 0 if on the circle
    private static float IsPointInsideOutsideOrOnCircle(Vector2 aVec, Vector2 bVec, Vector2 cVec, Vector2 dVec) {
      //This first part will simplify how we calculate the determinant
      float a = aVec.x - dVec.x;
      float d = bVec.x - dVec.x;
      float g = cVec.x - dVec.x;

      float b = aVec.y - dVec.y;
      float e = bVec.y - dVec.y;
      float h = cVec.y - dVec.y;

      float c = a * a + b * b;
      float f = d * d + e * e;
      float i = g * g + h * h;

      float determinant = (a * e * i) + (b * f * g) + (c * d * h) - (g * e * c) - (h * f * a) - (i * d * b);

      return determinant;
    }

    //Is a quadrilateral convex? Assume no 3 points are colinear and the shape doesnt look like an hourglass
    private static bool IsQuadrilateralConvex(Vector2 a, Vector2 b, Vector2 c, Vector2 d) {
      bool isConvex = false;

      bool abc = IsTriangleOrientedClockwise(a, b, c);
      bool abd = IsTriangleOrientedClockwise(a, b, d);
      bool bcd = IsTriangleOrientedClockwise(b, c, d);
      bool cad = IsTriangleOrientedClockwise(c, a, d);

      if (abc && abd && bcd & !cad) {
        isConvex = true;
      }
      else if (abc && abd && !bcd & cad) {
        isConvex = true;
      }
      else if (abc && !abd && bcd & cad) {
        isConvex = true;
      }
      //The opposite sign, which makes everything inverted
      else if (!abc && !abd && !bcd & cad) {
        isConvex = true;
      }
      else if (!abc && !abd && bcd & !cad) {
        isConvex = true;
      }
      else if (!abc && abd && !bcd & !cad) {
        isConvex = true;
      }


      return isConvex;
    }

    //Flip an edge
    private static void FlipEdge(HalfEdge one) {
      //The data we need
      //This edge's triangle
      HalfEdge two = one.nextEdge;
      HalfEdge three = one.prevEdge;
      //The opposite edge's triangle
      HalfEdge four = one.oppositeEdge;
      HalfEdge five = one.oppositeEdge.nextEdge;
      HalfEdge six = one.oppositeEdge.prevEdge;
      //The vertices
      var a = one.v;
      var b = one.nextEdge.v;
      var c = one.prevEdge.v;
      var d = one.oppositeEdge.nextEdge.v;



      //Flip

      //Change vertex
      a.edge = one.nextEdge;
      c.edge = one.oppositeEdge.nextEdge;

      //Change half-edge
      //Half-edge - half-edge connections
      one.nextEdge = three;
      one.prevEdge = five;

      two.nextEdge = four;
      two.prevEdge = six;

      three.nextEdge = five;
      three.prevEdge = one;

      four.nextEdge = six;
      four.prevEdge = two;

      five.nextEdge = one;
      five.prevEdge = three;

      six.nextEdge = two;
      six.prevEdge = four;

      //Half-edge - vertex connection
      one.v = b;
      two.v = b;
      three.v = c;
      four.v = d;
      five.v = d;
      six.v = a;

      //Half-edge - triangle connection
      Triangle t1 = one.t;
      Triangle t2 = four.t;

      one.t = t1;
      three.t = t1;
      five.t = t1;

      two.t = t2;
      four.t = t2;
      six.t = t2;

      //Opposite-edges are not changing!

      //Triangle connection
      t1.v1 = b;
      t1.v2 = c;
      t1.v3 = d;

      t2.v1 = b;
      t2.v2 = d;
      t2.v3 = a;

      t1.edge = three;
      t2.edge = four;
    }
  }

#endif

  #endregion

  #region NavMeshSurfaceType

  static bool _navMeshSurfaceTypeSearched;
  static Type _navMeshSurfaceType;

  public static Type NavMeshSurfaceType {
    get {
      // TypeUtils.FindType can be quite slow
      if (_navMeshSurfaceTypeSearched == false) {
        _navMeshSurfaceTypeSearched = true;
        _navMeshSurfaceType = TypeUtils.FindType("NavMeshSurface");
      }

      return _navMeshSurfaceType;
    }
  }

  #endregion
}
