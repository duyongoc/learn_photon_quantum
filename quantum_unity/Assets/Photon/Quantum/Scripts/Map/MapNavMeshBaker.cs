using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Linq;
using Photon.Deterministic;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Quantum
{
  public static class MapNavMeshBaker {

    static FP TriangleCheckEpsilon = FP._0_02;
    static readonly int MaxCandidates = 10;
    static List<TriangleCenterGrid.Pair> Candidates = new List<TriangleCenterGrid.Pair>(MaxCandidates);


    public static NavMesh BakeNavMesh(MapData data, MapNavMesh.BakeData navmeshBakeData) {
      return BakeNavMesh(data.Asset.Settings, navmeshBakeData);
    }

    public static NavMesh BakeNavMesh(Map map, MapNavMesh.BakeData navmeshBakeData) {
      NavMesh navmesh;

      navmesh = new NavMesh();
      navmesh.GridSizeX = map.GridSizeX;
      navmesh.GridSizeY = map.GridSizeY;
      navmesh.GridNodeSize = map.GridNodeSize;
      navmesh.WorldOffset = map.WorldOffset;
      navmesh.Name = navmeshBakeData.Name;
      navmesh.MinAgentRadius = navmeshBakeData.AgentRadius;

      var gridMin = navmesh.WorldOffset;
      var gridMax = navmesh.WorldOffset + new FPVector2(navmesh.GridSizeX * navmesh.GridNodeSize, navmesh.GridSizeY * navmesh.GridNodeSize);

      var regions = navmeshBakeData.Regions;
      if (regions == null) {
        regions = new List<string>();
      }

      using (var progressBar = new ProgressBar("Baking Quantum NavMesh", true, false)) {
        FPMathUtils.LoadLookupTables();

        // ADD MISSING REGIONS TO MAP

        for (int i = 0; i < regions.Count; i++) {
          if (Array.FindIndex(map.Regions, r => r == regions[i]) == -1) {
            ArrayUtils.Add(ref map.Regions, regions[i]);
          }
        }

        // VERTICES
        progressBar.Info = "Importing Vertices";
        MapNavMeshVertex[] vs_array = navmeshBakeData.Vertices.ToArray();
        navmesh.Vertices = new NavMeshVertex[vs_array.Length];
        for (int i = 0; i < vs_array.Length; i++) {
          var p = vs_array[i].Position.ToFPVector3() + navmeshBakeData.Position.ToFPVector3();
#if QUANTUM_XY
          if (navmeshBakeData.EnableQuantum_XY) {
            // Quantum navmesh has to be in XZ layout, so flip the components when QUANTUM_XY is used.
            p = new FPVector3(p.X, p.Z, p.Y);
          }
#endif
          navmesh.Vertices[i] = new NavMeshVertex { Point = p , Borders = new int[0]};

          if (navmesh.Vertices[i].Point.X < gridMin.X || navmesh.Vertices[i].Point.Z < gridMin.Y ||
              navmesh.Vertices[i].Point.X > gridMax.X || navmesh.Vertices[i].Point.Z > gridMax.Y) {
            // Navmesh outside detected. Stop the baking.
            throw new Exception("Detected navmesh vertex " + navmesh.Vertices[i].Point + " outside the grid. Make the grid larger.");
          }
        }

        // TRIANGLES
        progressBar.Info = "Importing Triangles";
        var navmeshTriangleList = new List<NavMeshTriangle>();
        for (int i = 0; i < navmeshBakeData.Triangles.Length; ++i) {
          progressBar.Progress = i / (float)navmeshBakeData.Triangles.Length;

          MapNavMeshTriangle t = navmeshBakeData.Triangles[i];

          if (t.VertexIds[0] == t.VertexIds[1] || t.VertexIds[0] == t.VertexIds[2] || t.VertexIds[1] == t.VertexIds[2]) {
            // TODO: this should go to the import..
            Debug.LogWarningFormat("Ignoring triangle {0} with duplicated vertices", i);
            continue;
          }

          int v0 = Array.FindIndex(vs_array, x => x.Id == t.VertexIds[0]);
          int v1 = Array.FindIndex(vs_array, x => x.Id == t.VertexIds[1]);
          int v2 = Array.FindIndex(vs_array, x => x.Id == t.VertexIds[2]);

          var regionFlags = 0UL;
          if (string.IsNullOrEmpty(t.RegionId) == false) {
            var regionIndex = Array.FindIndex(map.Regions, r => r == t.RegionId);
            regionFlags = 1UL << regionIndex;
          }

          // compute triangle normal with double precision
          var normal = default(Vector3Double);
          var p0 = new Vector3Double(navmesh.Vertices[v0].Point);
          var p1 = new Vector3Double(navmesh.Vertices[v1].Point);
          var p2 = new Vector3Double(navmesh.Vertices[v2].Point);
          try {
            var edge0 = (p1 - p0);
            var edge1 = (p2 - p0);
            edge0.Normalize();
            edge1.Normalize();
            normal = Vector3Double.Cross(edge0, edge1);
            normal.Normalize();
          }
          catch (ArgumentException) {
            Debug.LogWarning($"Failed to find triangle normal for triangle {i}: v0 {p0} v1 {p1} v2 {p2}");
          }

          navmeshTriangleList.Add(new NavMeshTriangle {
            Vertex0 = v0,
            Vertex1 = v1,
            Vertex2 = v2,
            Center = (navmesh.Vertices[v0].Point + navmesh.Vertices[v1].Point + navmesh.Vertices[v2].Point) / FP._3,
            Normal = normal.AsFPVector(),
            Borders = new Int32[0],
            Regions = new NavMeshRegionMask(regionFlags),
            Links = new Int32[0],
            Cost = t.Cost
          });
        }

        navmesh.Triangles = navmeshTriangleList.ToArray();

        // TRIANGLE GRID
        progressBar.Info = "Calculating Triangle Grid";
        navmesh.TrianglesGrid = GenerateTriangleGrid(navmesh, p => progressBar.Progress = p);

        // BORDER EDGES
        progressBar.Info = "Generating Borders";
        navmesh.Borders = GenerateBorders(navmesh, p => progressBar.Progress = p).ToArray();

        // BORDER GRID
        progressBar.Info = "Generating Border Grid";
        navmesh.BorderGrid = GenerateBorderGrid(navmesh, p => progressBar.Progress = p);

        // NEIGHBORS
        progressBar.Info = "Generating NavMesh Neighbors";
        GenerateTriangleNeighbors(navmesh, p => progressBar.Progress = p);

        // TRIANGLE CENTER GRID
        progressBar.Info = "Generating Triangle Center Grid";
        navmesh.TrianglesCenterGrid = GenerateTriangleCenterGrid(
          navmesh,
          navmeshBakeData.ClosestTriangleCalculation, 
          navmeshBakeData.ClosestTriangleCalculationDepth,
          regions, 
          p => progressBar.Progress = p);

        // LINKS
        navmesh.Links = new NavMeshLink[0];
        if (navmeshBakeData.Links != null) {
          progressBar.Info = "Generating NavMesh Links";
          GenerateNavMeshLinks(navmesh, navmeshBakeData.Links, map.Regions, navmeshBakeData.LinkErrorCorrection, p => progressBar.Progress = p);
        }

        return navmesh;
      }
    }

    static NavMeshTriangleNode[] GenerateTriangleGrid(NavMesh navmesh, Action<float> reporter) {
      NavMeshTriangleNode[] result = new NavMeshTriangleNode[navmesh.GridSizeX * navmesh.GridSizeY];
      for (int i = 0; i < navmesh.Triangles.Length; ++i) {
        reporter.Invoke(i / (float)navmesh.Triangles.Length);

        FPVector3 v0 = navmesh.Vertices[navmesh.Triangles[i].Vertex0].Point;
        FPVector3 v1 = navmesh.Vertices[navmesh.Triangles[i].Vertex1].Point;
        FPVector3 v2 = navmesh.Vertices[navmesh.Triangles[i].Vertex2].Point;

        int xMin = 0;
        int xMax = 0;
        int yMin = 0;
        int yMax = 0;
        if (!LocateGridCells(ref xMin, ref xMax, ref yMin, ref yMax, navmesh.WorldOffset, navmesh.GridNodeSize, v0.XZ, v1.XZ, v2.XZ)) {
          continue;
        }

        for (int z = yMin; z <= yMax; ++z) {
          for (int x = xMin; x <= xMax; ++x) {

            FPVector3 bl = navmesh.WorldOffset.XOY + new FPVector3(x * navmesh.GridNodeSize, 0, z * navmesh.GridNodeSize);
            FPVector3 br = bl + new FPVector3(navmesh.GridNodeSize, 0, 0);
            FPVector3 ur = bl + new FPVector3(navmesh.GridNodeSize, 0, navmesh.GridNodeSize);
            FPVector3 ul = bl + new FPVector3(0, 0, navmesh.GridNodeSize);

            if (
              // if any of the corners of the grid are inside the triangle
              FPCollision.TriangleContainsPointInclusive(bl, v0, v1, v2) ||
              FPCollision.TriangleContainsPointInclusive(br, v0, v1, v2) ||
              FPCollision.TriangleContainsPointInclusive(ur, v0, v1, v2) ||
              FPCollision.TriangleContainsPointInclusive(ul, v0, v1, v2) ||

              // if any of the triangle vertices are inside the grid cell (1st triangle)
              FPCollision.TriangleContainsPointInclusive(v0, ur, ul, bl) ||
              FPCollision.TriangleContainsPointInclusive(v1, ur, ul, bl) ||
              FPCollision.TriangleContainsPointInclusive(v2, ur, ul, bl) ||

              // if any of the triangle vertices are inside the grid cell (2st triangle)
              FPCollision.TriangleContainsPointInclusive(v0, bl, br, ur) ||
              FPCollision.TriangleContainsPointInclusive(v1, bl, br, ur) ||
              FPCollision.TriangleContainsPointInclusive(v2, bl, br, ur) ||

              // if the line segment of vertex 0 and vertex 1 intersects with any of the grid edges
              FPCollision.LineIntersectsLine(v0.XZ, v1.XZ, bl.XZ, br.XZ) ||
              FPCollision.LineIntersectsLine(v0.XZ, v1.XZ, br.XZ, ur.XZ) ||
              FPCollision.LineIntersectsLine(v0.XZ, v1.XZ, ur.XZ, ul.XZ) ||
              FPCollision.LineIntersectsLine(v0.XZ, v1.XZ, ul.XZ, bl.XZ) ||

              // if the line segment of vertex 1 and vertex 2 intersects with any of the grid edges
              FPCollision.LineIntersectsLine(v1.XZ, v2.XZ, bl.XZ, br.XZ) ||
              FPCollision.LineIntersectsLine(v1.XZ, v2.XZ, br.XZ, ur.XZ) ||
              FPCollision.LineIntersectsLine(v1.XZ, v2.XZ, ur.XZ, ul.XZ) ||
              FPCollision.LineIntersectsLine(v1.XZ, v2.XZ, ul.XZ, bl.XZ) ||

              // if the line segment of vertex 2 and vertex 0 intersects with any of the grid edges
              FPCollision.LineIntersectsLine(v2.XZ, v0.XZ, bl.XZ, br.XZ) ||
              FPCollision.LineIntersectsLine(v2.XZ, v0.XZ, br.XZ, ur.XZ) ||
              FPCollision.LineIntersectsLine(v2.XZ, v0.XZ, ur.XZ, ul.XZ) ||
              FPCollision.LineIntersectsLine(v2.XZ, v0.XZ, ul.XZ, bl.XZ)) {

              int idx = (z * navmesh.GridSizeX) + x;

              if (result[idx].Triangles == null) {
                result[idx].Triangles = new int[0];
              }

              // add triangle to this grid node
              ArrayUtils.Add(ref result[idx].Triangles, i);
            }
          }
        }
      }
      return result;
    }

    static List<NavMeshBorder> GenerateBorders(NavMesh navmesh, Action<float> reporter) {
      List<NavMeshBorder> borders = new List<NavMeshBorder>();
      for (int t = 0; t < navmesh.Triangles.Length; ++t) {
        reporter.Invoke(t / (float)navmesh.Triangles.Length);

        NavMeshTriangle tr = navmesh.Triangles[t];

        ProcessBorder(navmesh, t, tr.Vertex0, tr.Vertex1, borders);
        ProcessBorder(navmesh, t, tr.Vertex1, tr.Vertex2, borders);
        ProcessBorder(navmesh, t, tr.Vertex2, tr.Vertex0, borders);
      }

      return borders;
    }


    static void ProcessBorder(NavMesh navmesh, int t, int v0, int v1, List<NavMeshBorder> borders) {
      var regions = navmesh.Triangles[t].Regions;
      var otherRegions = new NavMeshRegionMask();
      if (IsBorderEdge(navmesh.Triangles, t, v0, v1, out otherRegions, navmesh.Vertices)) {

        // Make sure we look at each border the same way.
        if (v0 > v1) {
          int temp = v0;
          v0 = v1;
          v1 = temp;
        }

        // Calculate normal
        var direction = navmesh.Vertices[v1].Point - navmesh.Vertices[v0].Point;
        direction = direction.Normalized;
        var normal = FPVector3.Cross(direction, FPVector3.Up);
        normal = normal.Normalized;
        var middle = (navmesh.Vertices[v1].Point + navmesh.Vertices[v0].Point) * FP._0_50;
        if (!TriangleContains(navmesh.Triangles, navmesh.Vertices, middle + (normal * TriangleCheckEpsilon), regions) &&
             TriangleContains(navmesh.Triangles, navmesh.Vertices, middle + (-normal * TriangleCheckEpsilon), regions)) {
          // Flip normal, we want to point towards our region.
          normal = -normal;
        }

        // Generate border objects
        var border = new NavMeshBorder {
          V0 = navmesh.Vertices[v0].Point,
          V1 = navmesh.Vertices[v1].Point,
          Normal = normal,
          Regions = regions,
          RegionsOffmesh = otherRegions };
        var borderIndex = borders.FindIndex(b =>
          (b.V0 == border.V0 && b.V1 == border.V1 && b.Normal == border.Normal) ||
          (b.V0 == border.V1 && b.V1 == border.V0 && b.Normal == border.Normal));
        if (borderIndex == -1 || borders[borderIndex].Regions.HasValidRegions) {
          // Create "duplicated" borders for each Regions, we need to have them for their different normals.
          borders.Add(border);
          ArrayUtils.Add(ref navmesh.Vertices[v0].Borders, borders.Count - 1);
          ArrayUtils.Add(ref navmesh.Vertices[v1].Borders, borders.Count - 1);
          ArrayUtils.Add(ref navmesh.Triangles[t].Borders, borders.Count - 1);
        }
      }
    }

    static bool IsBorderEdge(NavMeshTriangle[] triangles, int tri, int v0, int v1, out NavMeshRegionMask otherRegion, NavMeshVertex[] vertices) {
      otherRegion = new NavMeshRegionMask();

      int borderTriangle = FindBorderTriangle(triangles, tri, v0, v1);

      // No other triangle found sharing the two vertices
      if (borderTriangle < 0)
        return true;

      if (triangles[tri].Regions.HasValidRegions && triangles[borderTriangle].Regions.IsMainArea) {
        return false;
      }

      otherRegion = triangles[borderTriangle].Regions;

      // Other triangle found but it's of a different region
      return !triangles[tri].Regions.Equals(triangles[borderTriangle].Regions);
    }

    static int FindBorderTriangle(NavMeshTriangle[] triangles, int tri, int v0, int v1) {
      for (int i = 0; i < triangles.Length; ++i) {
        if (i != tri) {
          if (triangles[i].Vertex0 == v0 || triangles[i].Vertex1 == v0 || triangles[i].Vertex2 == v0) {
            if (triangles[i].Vertex0 == v1 || triangles[i].Vertex1 == v1 || triangles[i].Vertex2 == v1) {
              return i;
            }
          }
        }
      }

      return -1;
    }

    static bool TriangleContains(NavMeshTriangle[] ts, NavMeshVertex[] vs, FPVector3 point, NavMeshRegionMask regions) {
      for (int i = 0; i < ts.Length; ++i) {
        if (ts[i].Regions.HasValidRegions && ts[i].Regions.IsSubset(regions) == false) { 
          // Don't flip when our triangle has a region and the opposing triangle has a different region.
          continue;
        }

        if (FPCollision.TriangleContainsPointInclusive(point, vs[ts[i].Vertex0].Point, vs[ts[i].Vertex1].Point, vs[ts[i].Vertex2].Point)) {
          return true;
        }
      }

      return false;
    }

    static NavMeshBorderNode[] GenerateBorderGrid(NavMesh navmesh, Action<float> reporter) {
      NavMeshBorderNode[] result = new NavMeshBorderNode[navmesh.GridSizeX * navmesh.GridSizeY];
      for (int i = 0; i < result.Length; i++) {
        // We do this because when calling and using bake during runtime, this array needs to be not null. During file serialization this is fixed automatically.
        result[i].Borders = new int[0];
      }

      for (int b = 0; b < navmesh.Borders.Length; ++b) {
        reporter.Invoke(b / (float)navmesh.Borders.Length);
        FPVector3 p0 = navmesh.Borders[b].V0;
        FPVector3 p1 = navmesh.Borders[b].V1;

        int xMin = 0;
        int xMax = 0;
        int yMin = 0;
        int yMax = 0;
        if (!LocateGridCells(ref xMin, ref xMax, ref yMin, ref yMax, navmesh.WorldOffset, navmesh.GridNodeSize, p0.XZ, p1.XZ)) {
          continue;
        }

        for (int z = yMin; z <= yMax; ++z) {
          for (int x = xMin; x <= xMax; ++x) {

            int idx = (z * navmesh.GridSizeX) + x;

            FP zn = (FP)z * navmesh.GridNodeSize;
            FP xn = (FP)x * navmesh.GridNodeSize;

            FPVector2 bl = navmesh.WorldOffset + new FPVector2(xn, zn);
            FPVector2 br = navmesh.WorldOffset + new FPVector2(xn + navmesh.GridNodeSize, zn);
            FPVector2 ur = navmesh.WorldOffset + new FPVector2(xn + navmesh.GridNodeSize, zn + navmesh.GridNodeSize);
            FPVector2 ul = navmesh.WorldOffset + new FPVector2(xn, zn + navmesh.GridNodeSize);

            if (
              // if any of the border vertices start inside the grid cell (1st triangle)
              FPCollision.TriangleContainsPointInclusive(p0.XZ, ur, ul, bl) ||
              FPCollision.TriangleContainsPointInclusive(p1.XZ, ur, ul, bl) ||

              // if any of the border vertices start inside the grid cell (2st triangle)
              FPCollision.TriangleContainsPointInclusive(p0.XZ, bl, br, ur) ||
              FPCollision.TriangleContainsPointInclusive(p1.XZ, bl, br, ur) ||

              // if the border edge intersects the grid cell
              FPCollision.LineIntersectsLine(p0.XZ, p1.XZ, bl, br) ||
              FPCollision.LineIntersectsLine(p0.XZ, p1.XZ, br, ur) ||
              FPCollision.LineIntersectsLine(p0.XZ, p1.XZ, ur, ul) ||
              FPCollision.LineIntersectsLine(p0.XZ, p1.XZ, ul, bl) ) 
            {
              ArrayUtils.Add(ref result[idx].Borders, b);
            }
          }
        }
      }
      return result;
    }

    static FP CalculateCellTriangleHeuristic(FPVector3 cellCenter, FP cellSize, NavMesh navmesh, int triangle) {
      // Does looking for the biggest triangle covering the cell area give better results?
      const int OverlappingPointCountTriangle = 4;
      const int OverlappingPointCountCell = 5;
      const int OverlappingPointCount = OverlappingPointCountTriangle + OverlappingPointCountCell;
      int overlappingPoints = 0;
      FP cellSizeOver2 = cellSize * FP._0_50;
      FPVector3 v0 = navmesh.Vertices[navmesh.Triangles[triangle].Vertex0].Point;
      FPVector3 v1 = navmesh.Vertices[navmesh.Triangles[triangle].Vertex1].Point;
      FPVector3 v2 = navmesh.Vertices[navmesh.Triangles[triangle].Vertex2].Point;
      FP shortestDist = FP.MaxValue;
      FP centerDist = FP._0;

      for (int v = 0; v < OverlappingPointCountTriangle; v++) {
        FPVector3 p = FPVector3.Zero;
        switch (v) {
          case 0: p = navmesh.Triangles[triangle].Center; break;
          case 1: p = v0; break;
          case 2: p = v1; break;
          case 3: p = v2; break;
        }

        FP dist = FPVector3.DistanceSquared(cellCenter, p);
        if (dist < shortestDist) {
          shortestDist = dist;
        }

        if (v == 0) {
          centerDist = dist;
        }

        if (p.X >= cellCenter.X - cellSizeOver2 && p.X <= cellCenter.X + cellSizeOver2 &&
            p.Z >= cellCenter.Z - cellSizeOver2 && p.Z <= cellCenter.Z + cellSizeOver2) {
          overlappingPoints++;
        }
      }

      for (int i = 0; i < OverlappingPointCountCell; i++) {
        FPVector2 p = FPVector2.Zero;
        switch (i) {
          case 0: p = cellCenter.XZ; break;
          case 1: p = cellCenter.XZ + new FPVector2 { X = -cellSizeOver2, Y = -cellSizeOver2 }; break;
          case 2: p = cellCenter.XZ + new FPVector2 { X = -cellSizeOver2, Y =  cellSizeOver2 }; break;
          case 3: p = cellCenter.XZ + new FPVector2 { X = cellSizeOver2, Y =  cellSizeOver2 }; break;
          case 4: p = cellCenter.XZ + new FPVector2 { X = cellSizeOver2, Y = -cellSizeOver2 }; break;
        }

        if (FPCollision.TriangleContainsPointInclusive(p, v0.XZ, v1.XZ, v2.XZ)) {
          overlappingPoints++;
        }
      }

      if (overlappingPoints == 0) {
        return shortestDist + centerDist + OverlappingPointCount;
      }

      return (OverlappingPointCount - overlappingPoints) + centerDist * FP.EN3;
    }

    static void CalculateCellTriangleHeuristic(NavMesh navmesh, int triangleIndex, FPVector3 cellCenter, int regionIndex) {

      if (regionIndex == -1 && navmesh.Triangles[triangleIndex].Regions.HasValidRegions) { 
        return;
      }
      else if (regionIndex >= 0 && navmesh.Triangles[triangleIndex].Regions.IsRegionEnabled(regionIndex) == false) { 
        return;
      }

      // if (Candidates.FindIndex(c => triangleIndex == c.Triangle) >= 0) {
      for (int i = 0; i < Candidates.Count; i++) {
        if (triangleIndex == Candidates[i].Triangle) {
          return;
        }
      }

      var h = CalculateCellTriangleHeuristic(cellCenter, navmesh.GridNodeSize, navmesh, triangleIndex);

      var candidate = new TriangleCenterGrid.Pair { Heuristic = h, Triangle = triangleIndex };

      //var index = Candidates.FindIndex(c => h < c.Heuristic);
      var index = -1;
      for (int i = 0; i < Candidates.Count; i++) {
        if (h < Candidates[i].Heuristic) {
          index = i;
          break;
        }
      }

      if (index >= 0) {
        Candidates.Insert(index, candidate);
      }
      else {
        if (Candidates.Count < MaxCandidates) {
          Candidates.Add(candidate);
        }
      }

      while (Candidates.Count > MaxCandidates) {
        Candidates.RemoveAt(Candidates.Count - 1);
      }
    }


    static Boolean FindClosestTriangle(NavMesh navmesh, int cellIndex, FPVector3 cellCenter, int regionIndex, MapNavMesh.FindClosestTriangleCalculation triangleCalc, int triangleCalcDepth, ref TriangleCenterGrid.Pair heuristic) {
      
      Candidates.Clear();

      if (navmesh.TrianglesGrid[cellIndex].Triangles != null) {
        for (int i = 0; i < navmesh.TrianglesGrid[cellIndex].Triangles.Length; ++i) {
          int triangleIndex = navmesh.TrianglesGrid[cellIndex].Triangles[i];
          CalculateCellTriangleHeuristic(navmesh, triangleIndex, cellCenter, regionIndex);
        }
      }

      if (Candidates.Count == 0) {
        switch (triangleCalc) {
          case MapNavMesh.FindClosestTriangleCalculation.BruteForce: {
              // Iterate through all triangles because the triangle closest can be inside a neighbor cell.
              for (int i = 0; i < navmesh.Triangles.Length; ++i) {
                CalculateCellTriangleHeuristic(navmesh, i, cellCenter, regionIndex);
              }
          }
            break;
          case MapNavMesh.FindClosestTriangleCalculation.SpiralOut: {
              // Spiral around the current cell to find a closest triangle in neighbors.
              var startX = cellIndex % navmesh.GridSizeX;
              var startY = cellIndex / navmesh.GridSizeY;

              for (Int32 i = 1; i <= triangleCalcDepth && Candidates.Count == 0; ++i) {
                Int32 x = 0;
                Int32 y = 0;

                // bottom line
                y = startY - i;
                if (y >= 0 && y < navmesh.GridSizeY) {
                  for (x = startX - i; x <= startX + i; ++x) {
                    if (x >= 0 && x < navmesh.GridSizeX) {
                      var cellIndexNew = x + y * navmesh.GridSizeX;
                      if (navmesh.TrianglesGrid[cellIndexNew].Triangles != null) {
                        for (int j = 0; j < navmesh.TrianglesGrid[cellIndexNew].Triangles.Length; ++j) {
                          int triangleIndex = navmesh.TrianglesGrid[cellIndexNew].Triangles[j];
                          CalculateCellTriangleHeuristic(navmesh, triangleIndex, cellCenter, regionIndex);
                        }
                      }
                    }
                  }
                }

                // top line
                y = startY + i;
                if (y >= 0 && y < navmesh.GridSizeY) {
                  for (x = startX - i; x <= startX + i; ++x) {
                    if (x >= 0 && x < navmesh.GridSizeX) {
                      var cellIndexNew = x + y * navmesh.GridSizeX;
                      if (navmesh.TrianglesGrid[cellIndexNew].Triangles != null) {
                        for (int j = 0; j < navmesh.TrianglesGrid[cellIndexNew].Triangles.Length; ++j) {
                          int triangleIndex = navmesh.TrianglesGrid[cellIndexNew].Triangles[j];
                          CalculateCellTriangleHeuristic(navmesh, triangleIndex, cellCenter, regionIndex);
                        }
                      }
                    }
                  }
                }

                // left line
                x = startX - i;
                if (x >= 0 && x < navmesh.GridSizeX) {
                  for (y = startY - i + 1; y <= startY + i - 1; ++y) {
                    if (y >= 0 && y < navmesh.GridSizeY) {
                      var cellIndexNew = x + y * navmesh.GridSizeX;
                      if (navmesh.TrianglesGrid[cellIndexNew].Triangles != null) {
                        for (int j = 0; j < navmesh.TrianglesGrid[cellIndexNew].Triangles.Length; ++j) {
                          int triangleIndex = navmesh.TrianglesGrid[cellIndexNew].Triangles[j];
                          CalculateCellTriangleHeuristic(navmesh, triangleIndex, cellCenter, regionIndex);
                        }
                      }
                    }
                  }
                }

                // right line
                x = startX + i;
                if (x >= 0 && x < navmesh.GridSizeX) {
                  for (y = startY - i + 1; y <= startY + i - 1; ++y) {
                    if (y >= 0 && y < navmesh.GridSizeY) {
                      var cellIndexNew = x + y * navmesh.GridSizeX;
                      if (navmesh.TrianglesGrid[cellIndexNew].Triangles != null) {
                        for (int j = 0; j < navmesh.TrianglesGrid[cellIndexNew].Triangles.Length; ++j) {
                          int triangleIndex = navmesh.TrianglesGrid[cellIndexNew].Triangles[j];
                          CalculateCellTriangleHeuristic(navmesh, triangleIndex, cellCenter, regionIndex);
                        }
                      }
                    }
                  }
                }
              }
              break;
            }
        }
      }

      if (Candidates.Count > 0) {
        heuristic = Candidates[0];
        return true;
      }

      return false;
    }

    static NavMeshTriangleCenterGridNode[] GenerateTriangleCenterGrid(NavMesh navmesh, MapNavMesh.FindClosestTriangleCalculation triangleCalc, int triangleCalcDepth, List<string> regionsList, Action<float> reporter) {
      NavMeshTriangleCenterGridNode[] result = new NavMeshTriangleCenterGridNode[navmesh.GridSizeX * navmesh.GridSizeY];

      // Triangles inside one cell are sorted by distance accompanied by the region it belongs to in a parallel array
      for (int z = 0; z < navmesh.GridSizeY; ++z) {
        for (int x = 0; x < navmesh.GridSizeX; ++x) {
          int cellIndex = (z * navmesh.GridSizeX) + x;
          reporter.Invoke(cellIndex / (float)(navmesh.GridSizeX * navmesh.GridSizeY));

          var zn = (FP)(z * navmesh.GridNodeSize);
          var xn = (FP)(x * navmesh.GridNodeSize);
          var cellCenter = (navmesh.WorldOffset + new FPVector2(xn, zn) + new FPVector2(navmesh.GridNodeSize * FP._0_50, navmesh.GridNodeSize * FP._0_50)).XOY;

          var node = new TriangleCenterGrid() { Regions = new List<TriangleCenterGrid.Pair>() };
          for (int r = 0; r < regionsList.Count + 1; r++) {
            node.Regions.Add(new TriangleCenterGrid.Pair { Triangle = -1, Heuristic = FP.UseableMax });
          }

          // Find the closest triangle for the "default" region
          TriangleCenterGrid.Pair heuristic = new TriangleCenterGrid.Pair();
          if (FindClosestTriangle(navmesh, cellIndex, cellCenter, -1, triangleCalc, triangleCalcDepth, ref heuristic)) {
            node.Regions[0] = heuristic;
          }
          else if (triangleCalc == MapNavMesh.FindClosestTriangleCalculation.BruteForce) {
            Debug.LogWarningFormat("Failed to find closest triangle to grid cell {0}", cellIndex);
          }

          // Iterate again through each of the regions
          for (int r = 0; r < regionsList.Count; r++) {
            if (FindClosestTriangle(navmesh, cellIndex, cellCenter, r, triangleCalc, triangleCalcDepth, ref heuristic)) {
              node.Regions[r + 1] = heuristic;
            }
          }

          for (int r = regionsList.Count - 1; r >= 0; r--) {
            if (node.Regions[r].Triangle == -1) {
              node.Regions.RemoveAt(r);
            }
          }

          node.Regions.Sort((a, b) => a.Heuristic.CompareTo(b.Heuristic));

          if (result[cellIndex].Triangles == null) {
            result[cellIndex].Triangles = new int[0];
          }

          foreach (var region in node.Regions) {
            if (region.Triangle >= 0) {
              ArrayUtils.Add(ref result[cellIndex].Triangles, region.Triangle);
            }
          }
        }
      }

      return result;
    }

    static void GenerateTriangleNeighbors(NavMesh navmesh, Action<float> reporter) {
      for (int i = 0; i < navmesh.Triangles.Length; i++) {
        reporter.Invoke(i / (float)navmesh.Triangles.Length);

        for (int j = 0; j < navmesh.Triangles.Length; j++) {
          if (i == j) {
            continue;
          }

          NavMeshTriangle t0 = navmesh.Triangles[i];
          NavMeshTriangle t1 = navmesh.Triangles[j];
          int V0 = 0;
          int V1 = 0;
          int found = 0;

          if (t0.Vertex0 == t1.Vertex0 || t0.Vertex0 == t1.Vertex1 || t0.Vertex0 == t1.Vertex2) {
            V0 = t0.Vertex0;
            found++;
          }

          if (t0.Vertex1 == t1.Vertex0 || t0.Vertex1 == t1.Vertex1 || t0.Vertex1 == t1.Vertex2) {
            if (found++ == 0)
              V0 = t0.Vertex1;
            else
              V1 = t0.Vertex1;
          }

          if (t0.Vertex2 == t1.Vertex0 || t0.Vertex2 == t1.Vertex1 || t0.Vertex2 == t1.Vertex2) {
            V1 = t0.Vertex2;
            found++;
          }

          if (found == 2) {
            if (t0.Neighbors == null)
              t0.Neighbors = new NavMeshPortal[0];
            FPVector3 straight = (navmesh.Vertices[V0].Point + (navmesh.Vertices[V0].Point - navmesh.Vertices[V1].Point) * FP._0_50) - t0.Center;
            FPVector3 toV0 = navmesh.Vertices[V0].Point - t0.Center;
            if (toV0.XZ.IsLeftOf(straight.XZ)) {
              int aux = V0;
              V0 = V1;
              V1 = aux;
            }
            ArrayUtils.Add<NavMeshPortal>(ref t0.Neighbors, new NavMeshPortal() {
              LeftVertex = V0,
              RightVertex = V1,
              Neighbor = j
            });
            navmesh.Triangles[i] = t0;
          }
        }
      }
    }

    static bool LocateGridCells(ref int xMin, ref int xMax, ref int yMin, ref int yMax, FPVector2 worldOffset, int gridNodeSize, params FPVector2[] positions) {
      if (positions.Length == 0) {
        return false;
      }

      FPBounds2 bounds = new FPBounds2(positions[0], FPVector2.Zero);
      for (int i = 1; i < positions.Length; ++i) {
        bounds.Encapsulate(positions[i]);
      }

      // Grow the bounds to get triangles laying on cell borders
      bounds.Expand(FP._0_10);

      var max = -worldOffset + bounds.Max;
      xMax = (max.X / gridNodeSize).AsInt;
      yMax = (max.Y / gridNodeSize).AsInt;

      var min = -worldOffset + bounds.Min;
      xMin = (min.X / gridNodeSize).AsInt;
      yMin = (min.Y / gridNodeSize).AsInt;

      return true;
    }

    static void GenerateNavMeshLinks(NavMesh navmesh, MapNavMeshLink[] links, string[] regions, bool errorCorrection, Action<float> reporter) {
      var additionalCellsToCheck = errorCorrection ? 1 : 0;
      var checkedTriangles = new HashSet<int>();
      for (var l = 0; l < links.Length; l++) {
        reporter.Invoke(links.Length / (float)l);
        var link = links[l];
        var startTriangle = -1;
        var startPosition = link.Start.ToFPVector3();
        var endTriangle = -1;
        var endPosition = link.End.ToFPVector3();

        checkedTriangles.Clear();

        {
          // find cell index (expand one cell for error correction)
          var p = link.Start.ToFPVector2() - navmesh.WorldOffset;
          var _x = (p.X / navmesh.GridNodeSize).AsInt;
          var _z = (p.Y / navmesh.GridNodeSize).AsInt;
          var closestDistance = double.MaxValue;

          var xMin = Math.Max(0, _x - additionalCellsToCheck);
          var xMax = Math.Min(navmesh.GridSizeX - 1, _x + additionalCellsToCheck);
          var zMin = Math.Max(0, _z - additionalCellsToCheck);
          var zMax = Math.Min(navmesh.GridSizeY - 1, _z + additionalCellsToCheck);

          for (int x = xMin; x <= xMax; x++) {
            for (int z = zMin; z <= zMax; z++) {
              var i = (z * navmesh.GridSizeX) + x;

              // no triangles in cell
              if (navmesh.TrianglesGrid[i].Triangles == null) {
                continue;
              }

              // already checked triangle
              if (checkedTriangles.Contains(i)) {
                continue;
              }
              checkedTriangles.Add(i);

              for (int t = 0; t < navmesh.TrianglesGrid[i].Triangles.Length; t++) {
                var triangleIndex = navmesh.TrianglesGrid[i].Triangles[t];
                var closestPoint = new Vector3Double();
                var d = Vector3Double.ClosestDistanceToTriangle(new Vector3Double(link.Start),
                                                                new Vector3Double(navmesh.Vertices[navmesh.Triangles[triangleIndex].Vertex0].Point),
                                                                new Vector3Double(navmesh.Vertices[navmesh.Triangles[triangleIndex].Vertex1].Point),
                                                                new Vector3Double(navmesh.Vertices[navmesh.Triangles[triangleIndex].Vertex2].Point), 
                                                                ref closestPoint);
                if (d < closestDistance) {
                  closestDistance = d;
                  startTriangle = triangleIndex;
                  if (errorCorrection) {
                    startPosition = closestPoint.AsFPVector();
                  }
                }
              }
            }
          }
        }

        checkedTriangles.Clear();

        {
          // find cell index (expand one cell for error correction)
          var p = link.End.ToFPVector2() - navmesh.WorldOffset;
          var _x = (p.X / navmesh.GridNodeSize).AsInt;
          var _z = (p.Y / navmesh.GridNodeSize).AsInt;
          var closestDistance = double.MaxValue;

          var xMin = Math.Max(0, _x - additionalCellsToCheck);
          var xMax = Math.Min(navmesh.GridSizeX - 1, _x + additionalCellsToCheck);
          var zMin = Math.Max(0, _z - additionalCellsToCheck);
          var zMax = Math.Min(navmesh.GridSizeY - 1, _z + additionalCellsToCheck);

          for (int x = xMin; x <= xMax; x++) {
            for (int z = zMin; z <= zMax; z++) {
              var i = (z * navmesh.GridSizeX) + x;

              // no triangles in cell
              if (navmesh.TrianglesGrid[i].Triangles == null) {
                continue;
              }

              // already checked triangle
              if (checkedTriangles.Contains(i)) {
                continue;
              }
              checkedTriangles.Add(i);

              for (int t = 0; t < navmesh.TrianglesGrid[i].Triangles.Length; t++) {
                var triangleIndex = navmesh.TrianglesGrid[i].Triangles[t];
                var closestPoint = new Vector3Double();
                var d = Vector3Double.ClosestDistanceToTriangle(new Vector3Double(link.End),
                                                                new Vector3Double(navmesh.Vertices[navmesh.Triangles[triangleIndex].Vertex0].Point),
                                                                new Vector3Double(navmesh.Vertices[navmesh.Triangles[triangleIndex].Vertex1].Point),
                                                                new Vector3Double(navmesh.Vertices[navmesh.Triangles[triangleIndex].Vertex2].Point),
                                                                ref closestPoint);
                if (d < closestDistance) {
                  closestDistance = d;
                  endTriangle = triangleIndex;
                  if (errorCorrection) {
                    endPosition = closestPoint.AsFPVector();
                  }
                }
              }
            }
          }
        }

        if (startTriangle == -1) {
          Debug.LogError($"Could not map start position {startPosition} of navmesh link (index {l}) to a triangle");
        }
        else if (endTriangle == -1) {
          Debug.LogError($"Could not map end position {endPosition} of navmesh link (index {l}) to a triangle");
        }
        else {

          var regionFlags = 0UL;
          if (string.IsNullOrEmpty(link.RegionId) == false) {
            var regionIndex = Array.FindIndex(regions, r => r == link.RegionId);
            regionFlags = 1UL << regionIndex;
          }

          ArrayUtils.Add(ref navmesh.Triangles[startTriangle].Links, navmesh.Links.Length);
          ArrayUtils.Add(ref navmesh.Links, new NavMeshLink {
            Start = startPosition,
            End = endPosition,
            Triangle = endTriangle,
            CostOverride = FP.FromFloat_UNSAFE(link.CostOverride),
            Region = new NavMeshRegionMask(regionFlags),
            Name = link.Name
          });
          

          if (link.Bidirectional) {
            ArrayUtils.Add(ref navmesh.Triangles[endTriangle].Links, navmesh.Links.Length);
            ArrayUtils.Add(ref navmesh.Links, new NavMeshLink {
              Start = endPosition,
              End = startPosition,
              Triangle = startTriangle,
              CostOverride = FP.FromFloat_UNSAFE(link.CostOverride),
              Region = new NavMeshRegionMask(regionFlags),
              Name = link.Name
            });
          }
        }
      }
    }

    struct TriangleCenterGrid {
      public struct Pair {
        public int Triangle;
        public FP Heuristic;
      }
      public List<Pair> Regions;
    }

    public struct Vector3Double {
      public double X;
      public double Y;
      public double Z;

      public Vector3Double(double x, double y, double z) {
        X = x;
        Y = y;
        Z = z;
      }

      public Vector3Double(FPVector3 v) {
        X = v.X.AsDouble;
        Y = v.Y.AsDouble;
        Z = v.Z.AsDouble;
      }

      public Vector3Double(Vector3 v) {
        X = v.x;
        Y = v.y;
        Z = v.z;
      }

      public static Vector3Double operator -(Vector3Double a, Vector3Double b) {
        return new Vector3Double(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
      }

      public static Vector3Double operator +(Vector3Double a, Vector3Double b) {
        return new Vector3Double(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
      }

      public static Vector3Double operator *(Vector3Double a, double b) {
        return new Vector3Double(a.X * b, a.Y * b, a.Z * b);
      }

      public static Vector3Double operator *(double b, Vector3Double a) {
        return new Vector3Double(a.X * b, a.Y * b, a.Z * b);
      }

      public FPVector3 AsFPVector() {
        return new FPVector3(FP.FromFloat_UNSAFE((float)X), FP.FromFloat_UNSAFE((float)Y), FP.FromFloat_UNSAFE((float)Z));
      }

      public double SqrMagnitude() {
        return X * X + Y * Y + Z * Z;
      }

      public void Normalize() {
        var d = Math.Sqrt(X * X + Y * Y + Z * Z);

        if (d == 0) {
          throw new ArgumentException("Vector magnitude is null");
        }

        X = X / d;
        Y = Y / d;
        Z = Z / d;
      }

      public override string ToString() {
        return $"{X} {Y} {Z}";
      }

      public static double Dot(Vector3Double a, Vector3Double b) {
        return a.X * b.X + a.Y * b.Y + a.Z * b.Z;
      }

      public static Vector3Double Cross(Vector3Double a, Vector3Double b) {
        return new Vector3Double(
          a.Y * b.Z - a.Z * b.Y,
          a.Z * b.X - a.X * b.Z,
          a.X * b.Y - a.Y * b.X);
      }

      public static double ClosestDistanceToTriangle(Vector3Double p, Vector3Double v0, Vector3Double v1, Vector3Double v2, ref Vector3Double closestPoint) {
        var diff = p - v0;
        var edge0 = v1 - v0;
        var edge1 = v2 - v0;
        var a00 = Dot(edge0, edge0);
        var a01 = Dot(edge0, edge1);
        var a11 = Dot(edge1, edge1);
        var b0 = -Dot(diff, edge0);
        var b1 = -Dot(diff, edge1);
        var det = a00 * a11 - a01 * a01;
        var t0 = a01 * b1 - a11 * b0;
        var t1 = a01 * b0 - a00 * b1;

        if (t0 + t1 <= det) {
          if (t0 < 0) {
            if (t1 < 0) {
              if (b0 < 0) {
                t1 = 0;
                if (-b0 >= a00) {
                  t0 = 1;
                } else {
                  t0 = -b0 / a00;
                }
              } else {
                t0 = 0;
                if (b1 >= 0) {
                  t1 = 0;
                } else if (-b1 >= a11) {
                  t1 = 1;
                } else {
                  t1 = -b1 / a11;
                }
              }
            } else {
              t0 = 0;
              if (b1 >= 0) {
                t1 = 0;
              } else if (-b1 >= a11) {
                t1 = 1;
              } else {
                t1 = -b1 / a11;
              }
            }
          } else if (t1 < 0) {
            t1 = 0;
            if (b0 >= 0) {
              t0 = 0;
            } else if (-b0 >= a00) {
              t0 = 1;
            } else {
              t0 = -b0 / a00;
            }
          } else {
            t0 /= det;
            t1 /= det;
          }
        } else {
          double tmp0, tmp1, numer, denom;

          if (t0 < 0) {
            tmp0 = a01 + b0;
            tmp1 = a11 + b1;
            if (tmp1 > tmp0) {
              numer = tmp1 - tmp0;
              denom = a00 - 2 * a01 + a11;
              if (numer >= denom) {
                t0 = 1;
                t1 = 0;
              } else {
                t0 = numer / denom;
                t1 = 1 - t0;
              }
            } else {
              t0 = 0;
              if (tmp1 <= 0) {
                t1 = 1;
              } else if (b1 >= 0) {
                t1 = 0;
              } else {
                t1 = -b1 / a11;
              }
            }
          } else if (t1 < 0) {
            tmp0 = a01 + b1;
            tmp1 = a00 + b0;
            if (tmp1 > tmp0) {
              numer = tmp1 - tmp0;
              denom = a00 - 2 * a01 + a11;
              if (numer >= denom) {
                t1 = 1;
                t0 = 0;
              } else {
                t1 = numer / denom;
                t0 = 1 - t1;
              }
            } else {
              t1 = 0;
              if (tmp1 <= 0) {
                t0 = 1;
              } else if (b0 >= 0) {
                t0 = 0;
              } else {
                t0 = -b0 / a00;
              }
            }
          } else {
            numer = a11 + b1 - a01 - b0;
            if (numer <= 0) {
              t0 = 0;
              t1 = 1;
            } else {
              denom = a00 - 2 * a01 + a11;
              if (numer >= denom) {
                t0 = 1;
                t1 = 0;
              } else {
                t0 = numer / denom;
                t1 = 1 - t0;
              }
            }
          }
        }

        closestPoint = v0 + t0 * edge0 + t1 * edge1;
        diff = p - closestPoint;
        return diff.SqrMagnitude();
      }
    }
  }
}