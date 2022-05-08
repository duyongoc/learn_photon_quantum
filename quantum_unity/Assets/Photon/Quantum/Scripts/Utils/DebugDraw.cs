using System;
using System.Collections.Generic;
using UnityEngine;

namespace Quantum {
  public static class DebugDraw {
    static Queue<Draw.DebugRay> _rays = new Queue<Draw.DebugRay>();
    static Queue<Draw.DebugLine> _lines = new Queue<Draw.DebugLine>();
    static Queue<Draw.DebugCircle> _circles = new Queue<Draw.DebugCircle>();
    static Queue<Draw.DebugSphere> _spheres = new Queue<Draw.DebugSphere>();
    static Queue<Draw.DebugRectangle> _rectangles = new Queue<Draw.DebugRectangle>();
    static Queue<Draw.DebugBox> _boxes = new Queue<Draw.DebugBox>();
    static Dictionary<ColorRGBA, Material> _materials = new Dictionary<ColorRGBA, Material>(ColorRGBA.EqualityComparer.Instance);

    static Draw.DebugRay[] _raysArray = new Draw.DebugRay[64];
    static Draw.DebugLine[] _linesArray = new Draw.DebugLine[64];
    static Draw.DebugCircle[] _circlesArray = new Draw.DebugCircle[64];
    static Draw.DebugSphere[] _spheresArray = new Draw.DebugSphere[64];
    static Draw.DebugRectangle[] _rectanglesArray = new Draw.DebugRectangle[64];
    static Draw.DebugBox[] _boxesArray = new Draw.DebugBox[64];

    static int _raysCount;
    static int _linesCount;
    static int _circlesCount;
    static int _spheresCount;
    static int _rectanglesCount;
    static int _boxesCount;

    static Mesh _solidSphere;

    public static void Ray(Draw.DebugRay ray) {
      lock (_rays) {
        _rays.Enqueue(ray);
      }
    }

    public static void Line(Draw.DebugLine line) {
      lock (_lines) {
        _lines.Enqueue(line);
      }
    }

    public static void Circle(Draw.DebugCircle circle) {
      lock (_circles) {
        _circles.Enqueue(circle);
      }
    }

    public static void Sphere(Draw.DebugSphere sphere) {
      lock (_spheres) {
        _spheres.Enqueue(sphere);
      }
    }

    public static void Rectangle(Draw.DebugRectangle rectangle) {
      lock (_rectangles) {
        _rectangles.Enqueue(rectangle);
      }
    }

    public static void Box(Draw.DebugBox box) {
      lock (_boxes) {
        _boxes.Enqueue(box);
      }
    }

    public static Material GetMaterial(ColorRGBA color) {
      if (_materials.TryGetValue(color, out var mat) == false) {
        mat = new Material(DebugMesh.DebugMaterial);
        mat.SetColor("_Color", color.ToColor());

        _materials.Add(color, mat);
      }

      return mat;
    }

    public static void Clear() {
      TakeAllFromQueueAndClearLocked(_rays,       ref _raysArray);
      TakeAllFromQueueAndClearLocked(_lines,      ref _linesArray);
      TakeAllFromQueueAndClearLocked(_circles,    ref _circlesArray);
      TakeAllFromQueueAndClearLocked(_spheres,    ref _spheresArray);
      TakeAllFromQueueAndClearLocked(_rectangles, ref _rectanglesArray);
      TakeAllFromQueueAndClearLocked(_boxes,      ref _boxesArray);

      _raysCount       = 0;
      _linesCount      = 0;
      _circlesCount    = 0;
      _spheresCount    = 0;
      _rectanglesCount = 0;
      _boxesCount      = 0;
    }

    public static void TakeAll() {
      _raysCount       = TakeAllFromQueueAndClearLocked(_rays,       ref _raysArray);
      _linesCount      = TakeAllFromQueueAndClearLocked(_lines,      ref _linesArray);
      _circlesCount    = TakeAllFromQueueAndClearLocked(_circles,    ref _circlesArray);
      _spheresCount    = TakeAllFromQueueAndClearLocked(_spheres,    ref _spheresArray);
      _rectanglesCount = TakeAllFromQueueAndClearLocked(_rectangles, ref _rectanglesArray);
      _boxesCount      = TakeAllFromQueueAndClearLocked(_boxes,      ref _boxesArray);
    }

    public static void DrawAll() {
      for (Int32 i = 0; i < _raysCount; ++i) {
        DrawRay(_raysArray[i]);
      }

      for (Int32 i = 0; i < _linesCount; ++i) {
        DrawLine(_linesArray[i]);
      }

      for (Int32 i = 0; i < _circlesCount; ++i) {
        DrawCircle(_circlesArray[i]);
      }
      //Debug.Log(spheresCount);
      for (Int32 i = 0; i < _spheresCount; ++i) {
        DrawSphere(_spheresArray[i]);
      }

      for (Int32 i = 0; i < _rectanglesCount; ++i) {
        DrawRectangle(_rectanglesArray[i]);
      }

      for (Int32 i = 0; i < _boxesCount; ++i) {
        DrawBox(_boxesArray[i]);
      }
    }

    static void DrawRay(Draw.DebugRay ray) {
      Debug.DrawRay(ray.Origin.ToUnityVector3(ray.Is2D), ray.Direction.ToUnityVector3(ray.Is2D), ray.Color.ToColor());
    }

    static void DrawLine(Draw.DebugLine line) {
      Debug.DrawLine(line.Start.ToUnityVector3(line.Is2D), line.End.ToUnityVector3(line.Is2D), line.Color.ToColor());
    }

    static Mesh GetSphere() {
      if (_solidSphere != null)
        return _solidSphere;
      var s = UnityEngine.GameObject.CreatePrimitive(UnityEngine.PrimitiveType.Sphere);
      _solidSphere = s.GetComponent<UnityEngine.MeshFilter>().mesh;
      UnityEngine.GameObject.Destroy(s);
      return _solidSphere;
    }


    static void DrawSphere(Draw.DebugSphere sphere) {
      Matrix4x4 mat = Matrix4x4.TRS(sphere.Center.ToUnityVector3(), Quaternion.identity, 2 * sphere.Radius.AsFloat * Vector3.one);
      Graphics.DrawMesh(GetSphere(), mat, GetMaterial(sphere.Color), 0, null, 0);
    }

    static void DrawCircle(Draw.DebugCircle circle) {
      Quaternion rot;

#if QUANTUM_XY
      rot = Quaternion.Euler(180, 0, 0);
#else
      rot = Quaternion.Euler(-90, 0, 0);
#endif

      // matrix for mesh
      var m = Matrix4x4.TRS(circle.Center.ToUnityVector3(circle.Is2D), rot, Vector3.one * (circle.Radius.AsFloat + circle.Radius.AsFloat));

      // draw
      Graphics.DrawMesh(DebugMesh.CircleMesh, m, GetMaterial(circle.Color), 0, null);
    }

    static void DrawRectangle(Draw.DebugRectangle rectangle) {
      Quaternion rot;

#if QUANTUM_XY
      rot = Quaternion.Euler(0, 0, rectangle.Rotation.AsFloat * Mathf.Rad2Deg) * Quaternion.Euler(90, 0, 0);
#else
      rot = Quaternion.Euler(0, -rectangle.Rotation.AsFloat * Mathf.Rad2Deg, 0);
#endif

      var m = Matrix4x4.TRS(rectangle.Center.ToUnityVector3(rectangle.Is2D), rot, rectangle.Size.XYY.ToUnityVector3());

      if (rectangle.Wire) {
        // GL.QUADS is faster but requires OnPostRender()
        Debug.DrawLine(m.MultiplyPoint3x4(new Vector3(0.5f, 0, 0.5f)), m.MultiplyPoint3x4(new Vector3(0.5f, 0, -0.5f)), rectangle.Color.ToColor());
        Debug.DrawLine(m.MultiplyPoint3x4(new Vector3(0.5f, 0, -0.5f)), m.MultiplyPoint3x4(new Vector3(-0.5f, 0, -0.5f)), rectangle.Color.ToColor());
        Debug.DrawLine(m.MultiplyPoint3x4(new Vector3(-0.5f, 0, -0.5f)), m.MultiplyPoint3x4(new Vector3(-0.5f, 0, 0.5f)), rectangle.Color.ToColor());
        Debug.DrawLine(m.MultiplyPoint3x4(new Vector3(-0.5f, 0, 0.5f)), m.MultiplyPoint3x4(new Vector3(0.5f, 0, 0.5f)), rectangle.Color.ToColor());
      } else {
        Graphics.DrawMesh(DebugMesh.QuadMesh, m, GetMaterial(rectangle.Color), 0, null);
      }
    }

    static void DrawBox(Draw.DebugBox box) {
      var m = Matrix4x4.TRS(box.Center.ToUnityVector3(), box.Rotation.ToUnityQuaternion(), box.Size.ToUnityVector3());

      if (box.Wire) {
        // GL.QUADS is faster but requires OnPostRender()
        var v0 = m.MultiplyPoint3x4(new Vector3(0.5f, 0.5f, 0.5f));
        var v1 = m.MultiplyPoint3x4(new Vector3(-0.5f, 0.5f, 0.5f));
        var v2 = m.MultiplyPoint3x4(new Vector3(-0.5f, -0.5f, 0.5f));
        var v3 = m.MultiplyPoint3x4(new Vector3(0.5f, -0.5f, 0.5f));

        var v4 = m.MultiplyPoint3x4(new Vector3(0.5f, 0.5f, -0.5f));
        var v5 = m.MultiplyPoint3x4(new Vector3(-0.5f, 0.5f, -0.5f));
        var v6 = m.MultiplyPoint3x4(new Vector3(-0.5f, -0.5f, -0.5f));
        var v7 = m.MultiplyPoint3x4(new Vector3(0.5f, -0.5f, -0.5f));

        // back face
        Debug.DrawLine(v0, v1, box.Color.ToColor());
        Debug.DrawLine(v1, v2, box.Color.ToColor());
        Debug.DrawLine(v2, v3, box.Color.ToColor());
        Debug.DrawLine(v3, v0, box.Color.ToColor());

        // front face
        Debug.DrawLine(v4, v5, box.Color.ToColor());
        Debug.DrawLine(v5, v6, box.Color.ToColor());
        Debug.DrawLine(v6, v7, box.Color.ToColor());
        Debug.DrawLine(v7, v4, box.Color.ToColor());

        // connect faces
        Debug.DrawLine(v0, v4, box.Color.ToColor());
        Debug.DrawLine(v1, v5, box.Color.ToColor());
        Debug.DrawLine(v2, v6, box.Color.ToColor());
        Debug.DrawLine(v3, v7, box.Color.ToColor());
      } else {
        Graphics.DrawMesh(DebugMesh.CubeMesh, m, GetMaterial(box.Color), 0, null);
      }
    }

    static Int32 TakeAllFromQueueAndClearLocked<T>(Queue<T> queue, ref T[] result) {
      lock (queue) {
        var count = 0;

        if (queue.Count > 0) {
          // if result array size is less than queue count
          if (result.Length < queue.Count) {

            // find the next new size that is a multiple of the current result size
            var newSize = result.Length;

            while (newSize < queue.Count) {
              newSize = newSize * 2;
            }

            // and re-size array
            Array.Resize(ref result, newSize);
          }

          // grab all
          while (queue.Count > 0) {
            result[count++] = queue.Dequeue();
          }

          // clear queue
          queue.Clear();
        }

        return count;
      }
    }
  }
}