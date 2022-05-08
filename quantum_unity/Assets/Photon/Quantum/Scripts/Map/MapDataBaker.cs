using Photon.Deterministic;
using Quantum;
using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using Assert = Quantum.Assert;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

public static class MapDataBaker {
  public static int NavMeshSerializationBufferSize = 1024 * 1024 * 60;

  public enum BuildTrigger {
    SceneSave,
    PlaymodeChange,
    Build,
    Manual
  }

  public static void BakeMapData(MapData data, Boolean inEditor, Boolean bakeColliders = true, Boolean bakePrototypes = true, QuantumMapDataBakeFlags bakeFlags = QuantumMapDataBakeFlags.None, BuildTrigger buildTrigger = BuildTrigger.Manual) {
    FPMathUtils.LoadLookupTables();

    if (inEditor == false && !data.Asset) {
      data.Asset          = ScriptableObject.CreateInstance<MapAsset>();
      data.Asset.Settings = new Quantum.Map();
    }

#if UNITY_EDITOR
    if (inEditor) {
      // set scene name
      data.Asset.Settings.Scene = data.gameObject.scene.name;

      var path = data.gameObject.scene.path;
      data.Asset.Settings.ScenePath = path;
      if (string.IsNullOrEmpty(path)) {
        data.Asset.Settings.SceneGuid = string.Empty;
      } else {
        data.Asset.Settings.SceneGuid = AssetDatabase.AssetPathToGUID(path);
      }
    }
#endif

      InvokeCallbacks("OnBeforeBake", data, buildTrigger, bakeFlags);
    InvokeCallbacks("OnBeforeBake", data);

    if (bakeColliders) {
      BakeColliders(data, inEditor);
    }

    if (bakePrototypes) {
      BakePrototypes(data);
    }

    // invoke callbacks
    InvokeCallbacks("OnBake", data);
  }

  public static void BakeMeshes(MapData data, Boolean inEditor) {
    if (inEditor) {
#if UNITY_EDITOR
      var dirPath = Path.GetDirectoryName(AssetDatabase.GetAssetPath(data.Asset));
      var assetPath = Path.Combine(dirPath, data.Asset.name + "_mesh.asset");

      var binaryDataAsset = AssetDatabase.LoadAssetAtPath<BinaryDataAsset>(assetPath);
      if (binaryDataAsset == null) {
        binaryDataAsset = ScriptableObject.CreateInstance<BinaryDataAsset>();
        AssetDatabase.CreateAsset(binaryDataAsset, assetPath);
        binaryDataAsset.Settings.Guid = AssetGuid.NewGuid();
      }

      // Serialize to binary some of the data (max 20 megabytes for now)
      var bytestream = new ByteStream(new Byte[NavMeshSerializationBufferSize]);
      data.Asset.Settings.SerializeStaticColliderTriangles(bytestream, true);

      binaryDataAsset.SetData(bytestream.ToArray(), binaryDataAsset.Settings.IsCompressed);
      EditorUtility.SetDirty(binaryDataAsset);

      data.Asset.Settings.StaticColliders3DTrianglesData = binaryDataAsset.Settings;

#pragma warning disable CS0618 // Type or member is obsolete
      RemoveLegacyResourcesBinaryFile(ref data.Asset.Settings.StaticColliders3DTrianglesBinaryFile);
#pragma warning restore CS0618 // Type or member is obsolete
#endif
    }
  }

#if !QUANTUM_DISABLE_AI

  public static IEnumerable<NavMesh> BakeNavMeshes(MapData data, Boolean inEditor) {
    FPMathUtils.LoadLookupTables();

    data.Asset.Settings.NavMeshLinks = new AssetRefNavMesh[0];
    data.Asset.Settings.Regions      = new string[0];
    
    InvokeCallbacks("OnBeforeBakeNavMesh", data);

    var navmeshes = BakeNavMeshesLoop(data).ToList();

    InvokeCallbacks("OnCollectNavMeshes", data, navmeshes);

    if (inEditor) {
#if UNITY_EDITOR
      var dirPath = Path.GetDirectoryName(AssetDatabase.GetAssetPath(data.Asset));
      ByteStream bytestream = null;
      foreach (var navmesh in navmeshes) {
        var navmeshBinaryFilename = Path.Combine(dirPath, $"{data.Asset.name}_{navmesh.Name}_data.asset");
        var navmeshAssetFilename  = $"{data.Asset.name}_{navmesh.Name}.asset";

        var binaryDataAsset = AssetDatabase.LoadAssetAtPath<BinaryDataAsset>(navmeshBinaryFilename);
        if (binaryDataAsset == null) {
          binaryDataAsset = ScriptableObject.CreateInstance<BinaryDataAsset>();
          AssetDatabase.CreateAsset(binaryDataAsset, navmeshBinaryFilename);
          binaryDataAsset.Settings.Guid = AssetGuid.NewGuid();
        }

        navmesh.DataAsset = binaryDataAsset.Settings;

        // Serialize to binary some of the data (max 60 megabytes for now)
        if (bytestream == null) {
          bytestream = new ByteStream(new Byte[NavMeshSerializationBufferSize]);
        } else {
          bytestream.Reset();
        }
        navmesh.Serialize(bytestream, true);

        binaryDataAsset.SetData(bytestream.ToArray(), binaryDataAsset.Settings.IsCompressed);
        EditorUtility.SetDirty(binaryDataAsset);

        var navmeshAssetPath = Path.Combine(dirPath, navmeshAssetFilename);

        var oldAsset         = AssetDatabase.LoadAssetAtPath<NavMeshAsset>(navmeshAssetPath);
        if (oldAsset != null) {
          navmesh.Guid = oldAsset.Settings.Guid;
        }

        if (!navmesh.Guid.IsValid) {
          navmesh.Guid = AssetGuid.NewGuid();
        }

        if (oldAsset != null) {
#pragma warning disable CS0618 // Type or member is obsolete
          RemoveLegacyResourcesBinaryFile(ref oldAsset.Settings.DataFilepath);
#pragma warning restore CS0618 // Type or member is obsolete

          // Reuse the old one
          navmesh.Path      = oldAsset.Settings.Path;
          oldAsset.Settings = navmesh;

          EditorUtility.SetDirty(oldAsset);
        } else {
          // Create scriptable object
          var navMeshAsset = ScriptableObject.CreateInstance<NavMeshAsset>();
          navmesh.Path          = navMeshAsset.GenerateDefaultPath(navmeshAssetPath);
          navMeshAsset.Settings = navmesh;
          AssetDatabase.CreateAsset(navMeshAsset, navmeshAssetPath);
          AssetDatabase.ImportAsset(navmeshAssetPath, ImportAssetOptions.ForceUpdate);
        }

        ArrayUtils.Add(ref data.Asset.Settings.NavMeshLinks, (AssetRefNavMesh)navmesh);
      }
#endif
    } else {
      // When executing this during runtime the guids of the created navmesh are added to the map.
      // Binary navmesh files are not created because the fresh navmesh object has everything it needs.
      // Caveat: the returned navmeshes need to be added to the DB by either...
      // A) overwriting the navmesh inside an already existing NavMeshAsset ScriptableObject or
      // B) Creating new NavMeshAsset ScriptableObjects (see above) and inject them into the DB (use UnityDB.OnAssetLoad callback).
      foreach (var navmesh in navmeshes) {
        navmesh.Path = data.Asset.name + "_" + navmesh.Name;
        navmesh.Guid = AssetGuid.NewGuid();
        ArrayUtils.Add(ref data.Asset.Settings.NavMeshLinks, (AssetRefNavMesh)navmesh);
      }
    }
    
    InvokeCallbacks("OnBakeNavMesh", data);

    return navmeshes;
  }

#endif

  static void RemoveLegacyResourcesBinaryFile(ref string name) {
#if UNITY_EDITOR
    if (!string.IsNullOrEmpty(name)) {
      string fullPath = Path.Combine(QuantumEditorSettings.Instance.DefaultAssetSearchPath, name);
      bool removed = AssetDatabase.DeleteAsset(fullPath);
      if (removed) {
        Debug.Log($"Removed legacy binary resource: {fullPath}");
      } else {
        Debug.LogWarning($"Failed to remove legacy binary resource: {fullPath}.");
      }
      name = "";
    }
#endif
  }

  static StaticColliderData GetStaticData(GameObject gameObject, QuantumStaticColliderSettings settings, int colliderId) {
    return new StaticColliderData {
      Asset         = settings.Asset,
      Name          = gameObject.name,
      Tag           = gameObject.tag,
      Layer         = gameObject.layer,
      IsTrigger     = settings.Trigger,
      ColliderIndex = colliderId,
      MutableMode   = settings.MutableMode,
    };
  }

  public static void BakeColliders(MapData data, Boolean inEditor) {
    var scene = data.gameObject.scene;
    Debug.Assert(scene.IsValid());

    // clear existing colliders
    data.StaticCollider2DReferences       = new List<MonoBehaviour>();
    data.StaticCollider3DReferences       = new List<MonoBehaviour>();

    // 2D
    data.Asset.Settings.StaticColliders2D = new MapStaticCollider2D[0];
    var staticCollider2DList              = new List<MapStaticCollider2D>();

#if !QUANTUM_DISABLE_PHYSICS2D
    // circle colliders
    foreach (var collider in FindLocalObjects<QuantumStaticCircleCollider2D>(scene)) {
      collider.BeforeBake();
      
      var s = collider.transform.localScale;

      staticCollider2DList.Add( new MapStaticCollider2D {
        Position = collider.transform.TransformPoint(collider.PositionOffset.ToUnityVector3()).ToFPVector2(),
        Rotation = collider.transform.rotation.ToFPRotation2D(),
#if QUANTUM_XY
        VerticalOffset = -collider.transform.position.z.ToFP(),
        Height = collider.Height * s.z.ToFP(),
#else
        VerticalOffset = collider.transform.position.y.ToFP(),
        Height         = collider.Height * s.y.ToFP(),
#endif
        PhysicsMaterial = collider.Settings.PhysicsMaterial,
        StaticData      = GetStaticData(collider.gameObject, collider.Settings, staticCollider2DList.Count),
        Layer           = collider.gameObject.layer,

        // circle
        ShapeType    = Quantum.Shape2DType.Circle,
        CircleRadius = FP.FromFloat_UNSAFE(collider.Radius.AsFloat * s.x)
      });

      data.StaticCollider2DReferences.Add(collider);
    }

    // polygon colliders
    foreach (var c in FindLocalObjects<QuantumStaticPolygonCollider2D>(scene)) {
      c.BeforeBake();

      if (c.BakeAsStaticEdges2D) {
        for (var i = 0; i < c.Vertices.Length; i++) {
          var staticEdge = BakeStaticEdge2D(c.transform, c.PositionOffset, c.RotationOffset, c.Vertices[i], c.Vertices[(i + 1) % c.Vertices.Length], c.Height, c.Settings, staticCollider2DList.Count);
          staticCollider2DList.Add(staticEdge);
          data.StaticCollider2DReferences.Add(c);
        }

        continue;
      }

      var s = c.transform.localScale;
      var vertices = c.Vertices.Select(x => {
        var v = x.ToUnityVector3();
        return new Vector3(v.x * s.x, v.y * s.y, v.z * s.z);
      }).Select(x => x.ToFPVector2()).ToArray();
      if (FPVector2.IsClockWise(vertices)) {
        FPVector2.MakeCounterClockWise(vertices);
      }


      var normals = FPVector2.CalculatePolygonNormals(vertices);
      var rotation = c.transform.rotation.ToFPRotation2D() + c.RotationOffset.FlipRotation() * FP.Deg2Rad;
      var positionOffset = FPVector2.Rotate(FPVector2.CalculatePolygonCentroid(vertices), rotation);

      staticCollider2DList.Add(new MapStaticCollider2D {
        Position = c.transform.TransformPoint(c.PositionOffset.ToUnityVector3()).ToFPVector2() + positionOffset,
        Rotation = rotation,
#if QUANTUM_XY
        VerticalOffset = -c.transform.position.z.ToFP(),
        Height = c.Height * s.z.ToFP(),
#else
        VerticalOffset = c.transform.position.y.ToFP(),
        Height         = c.Height * s.y.ToFP(),
#endif
        PhysicsMaterial = c.Settings.PhysicsMaterial,
        StaticData      = GetStaticData(c.gameObject, c.Settings, staticCollider2DList.Count),
        Layer           = c.gameObject.layer,

        // polygon
        ShapeType = Quantum.Shape2DType.Polygon,
        PolygonCollider = new PolygonCollider {
          Vertices = FPVector2.RecenterPolygon(vertices),
          Normals  = normals
        }
      });

      data.StaticCollider2DReferences.Add(c);
    }

    // edge colliders
    foreach (var c in FindLocalObjects<QuantumStaticEdgeCollider2D>(scene)) {
      c.BeforeBake();

      staticCollider2DList.Add(BakeStaticEdge2D(c.transform, c.PositionOffset, c.RotationOffset, c.VertexA, c.VertexB, c.Height, c.Settings, staticCollider2DList.Count));
      data.StaticCollider2DReferences.Add(c);
    }

    // box colliders
    foreach (var collider in FindLocalObjects<QuantumStaticBoxCollider2D>(scene)) {
      collider.BeforeBake();

      var e = collider.Size.ToUnityVector3();
      var s = collider.transform.localScale;
      
      e.x *= s.x;
      e.y *= s.y;
      e.z *= s.z;

      staticCollider2DList.Add(new MapStaticCollider2D {
        Position = collider.transform.TransformPoint(collider.PositionOffset.ToUnityVector3()).ToFPVector2(),
        Rotation = collider.transform.rotation.ToFPRotation2D() + collider.RotationOffset.FlipRotation() * FP.Deg2Rad,
#if QUANTUM_XY
        VerticalOffset = -collider.transform.position.z.ToFP(),
        Height = collider.Height * s.z.ToFP(),
#else
        VerticalOffset = collider.transform.position.y.ToFP(),
        Height         = collider.Height * s.y.ToFP(),
#endif
        PhysicsMaterial = collider.Settings.PhysicsMaterial,
        StaticData      = GetStaticData(collider.gameObject, collider.Settings, staticCollider2DList.Count),
        Layer = collider.gameObject.layer,

        // polygon
        ShapeType = Quantum.Shape2DType.Box,
        BoxExtents = e.ToFPVector2() * FP._0_50
      });

      data.StaticCollider2DReferences.Add(collider);
    }

    data.Asset.Settings.StaticColliders2D = staticCollider2DList.ToArray();
#endif

    // 3D statics

    // clear existing colliders
    var staticCollider3DList = new List<MapStaticCollider3D>();

    // clear on mono behaviour and assets
    data.Asset.Settings.StaticColliders3DTriangles = new Dictionary<int, TriangleCCW[]>();
    data.Asset.Settings.StaticColliders3D          = new MapStaticCollider3D[0];

    // initialize collider references, add default null on offset 0
    data.StaticCollider3DReferences = new List<MonoBehaviour>();

#if !QUANTUM_DISABLE_PHYSICS3D

    // sphere colliders
    foreach (var collider in FindLocalObjects<QuantumStaticSphereCollider3D>(scene)) {
      collider.BeforeBake();

      var rot = collider.transform.rotation.ToFPQuaternion();
      staticCollider3DList.Add(new MapStaticCollider3D {
        Position        = collider.transform.TransformPoint(collider.PositionOffset.ToUnityVector3()).ToFPVector3(),
        Rotation        = rot,
        PhysicsMaterial = collider.Settings.PhysicsMaterial,
        StaticData      = GetStaticData(collider.gameObject, collider.Settings, staticCollider3DList.Count),

        // circle
        ShapeType    = Quantum.Shape3DType.Sphere,
        SphereRadius = FP.FromFloat_UNSAFE(collider.Radius.AsFloat * collider.transform.localScale.x)
      });

      data.StaticCollider3DReferences.Add(collider);
    }

    // box colliders
    foreach (var collider in FindLocalObjects<QuantumStaticBoxCollider3D>(scene)) {
      collider.BeforeBake();

      var e = collider.Size.ToUnityVector3();
      var s = collider.transform.localScale;

      e.x *= s.x;
      e.y *= s.y;
      e.z *= s.z;

      staticCollider3DList.Add(new MapStaticCollider3D {
        Position        = collider.transform.TransformPoint(collider.PositionOffset.ToUnityVector3()).ToFPVector3(),
        Rotation        = FPQuaternion.Euler(collider.transform.rotation.eulerAngles.ToFPVector3() + collider.RotationOffset),
        PhysicsMaterial = collider.Settings.PhysicsMaterial,
        StaticData      = GetStaticData(collider.gameObject, collider.Settings, staticCollider3DList.Count),

        // box
        ShapeType  = Quantum.Shape3DType.Box,
        BoxExtents = e.ToFPVector3() * FP._0_50
      });

      data.StaticCollider3DReferences.Add(collider);
    }

    var meshes   = FindLocalObjects<QuantumStaticMeshCollider3D>(scene);
    
    // static 3D mesh colliders
    foreach (var collider in meshes) {
      // our assumed static collider index
      var staticColliderIndex = staticCollider3DList.Count;

      // bake mesh
      if (collider.Bake(staticColliderIndex)) {
        Quantum.Assert.Check(staticColliderIndex == staticCollider3DList.Count);

#pragma warning disable 618 // use of obsolete
        // convert obsolete mutable mode into new
        if (collider.Mode == MapStaticCollider3D.MutableModes.ToggleableStartOn) {
          collider.Settings.MutableMode = PhysicsCommon.StaticColliderMutableMode.ToggleableStartOn;
          collider.Mode                 = MapStaticCollider3D.MutableModes.Immutable;
        }

        if (collider.Mode == MapStaticCollider3D.MutableModes.ToggleableStartOff) {
          collider.Settings.MutableMode = PhysicsCommon.StaticColliderMutableMode.ToggleableStartOff;
          collider.Mode                 = MapStaticCollider3D.MutableModes.Immutable;
        }
#pragma warning restore 618

        // add on list
        staticCollider3DList.Add(new MapStaticCollider3D {
          Position        = collider.transform.position.ToFPVector3(),
          Rotation        = collider.transform.rotation.ToFPQuaternion(),
          PhysicsMaterial = collider.Settings.PhysicsMaterial,
          SmoothSphereMeshCollisions = collider.SmoothSphereMeshCollisions,

          // mesh
          ShapeType = Quantum.Shape3DType.Mesh,

          StaticData = GetStaticData(collider.gameObject, collider.Settings, staticColliderIndex),
        });

        // add to static collider lookup
        data.StaticCollider3DReferences.Add(collider);

        // add to static collider data
        data.Asset.Settings.StaticColliders3DTriangles.Add(staticColliderIndex, collider.Triangles);
      }
    }

#endif

    var terrains = FindLocalObjects<QuantumStaticTerrainCollider3D>(scene);

    // terrain colliders
    foreach (var terrain in terrains) {
      // our assumed static collider index
      var staticColliderIndex = staticCollider3DList.Count;

      // bake terrain
      terrain.Bake();

#pragma warning disable 618 // use of obsolete
      // convert obsolete mutable mode into new
      if (terrain.Mode == MapStaticCollider3D.MutableModes.ToggleableStartOn) {
        terrain.MutableMode = PhysicsCommon.StaticColliderMutableMode.ToggleableStartOn;
        terrain.Mode        = MapStaticCollider3D.MutableModes.Immutable;
      }

      if (terrain.Mode == MapStaticCollider3D.MutableModes.ToggleableStartOff) {
        terrain.MutableMode = PhysicsCommon.StaticColliderMutableMode.ToggleableStartOff;
        terrain.Mode        = MapStaticCollider3D.MutableModes.Immutable;
      }
#pragma warning restore 618

      // add to 3d collider list
      staticCollider3DList.Add(new MapStaticCollider3D {
        Position        = default(FPVector3),
        Rotation        = FPQuaternion.Identity,
        PhysicsMaterial = terrain.Asset.Settings.PhysicsMaterial,
        SmoothSphereMeshCollisions = terrain.SmoothSphereMeshCollisions,

        // terrains are meshes
        ShapeType = Quantum.Shape3DType.Mesh,

        // static data for terrain
        StaticData = new StaticColliderData {
          Name          = terrain.gameObject.name,
          Layer         = terrain.gameObject.layer,
          Tag           = terrain.gameObject.tag,
          Asset         = terrain.Asset.Settings,
          IsTrigger     = false,
          ColliderIndex = staticColliderIndex,
          MutableMode   = terrain.MutableMode,
        }
      });

      // add to 
      data.StaticCollider3DReferences.Add(terrain);

      // load all triangles
      terrain.Asset.Settings.Bake(staticColliderIndex);

      // add to static collider data
      data.Asset.Settings.StaticColliders3DTriangles.Add(staticColliderIndex, terrain.Asset.Settings.Triangles);
    }

    // this has to hold
    Assert.Check(staticCollider3DList.Count == data.StaticCollider3DReferences.Count);

    // assign collider 3d array
    data.Asset.Settings.StaticColliders3D = staticCollider3DList.ToArray();

    // clear this so it's not re-used by accident
    staticCollider3DList = null;

    BakeMeshes(data, inEditor);

    if (inEditor) {
      Debug.LogFormat("Baked {0} 2D static colliders", data.Asset.Settings.StaticColliders2D.Length);
      Debug.LogFormat("Baked {0} 3D static primitive colliders", data.Asset.Settings.StaticColliders3D.Length);
      Debug.LogFormat("Baked {0} 3D static triangles", data.Asset.Settings.StaticColliders3DTriangles.Select(x => x.Value.Length).Sum());
    }
  }

  public static void BakePrototypes(MapData data) {
    var scene = data.gameObject.scene;
    Debug.Assert(scene.IsValid());

    data.Asset.Prototypes.Clear();
    data.MapEntityReferences.Clear();

    var components = new List<EntityComponentBase>();
    var prototypes = FindLocalObjects<EntityPrototype>(scene).ToArray();
    SortBySiblingIndex(prototypes);

    var converter = new EntityPrototypeConverter(data, prototypes);
    var storeVisitor = new Quantum.Prototypes.FlatEntityPrototypeContainer.StoreVisitor();

    foreach (var prototype in prototypes) {
      prototype.GetComponents(components);

      var container = new Quantum.Prototypes.FlatEntityPrototypeContainer();
      storeVisitor.Storage = container;

      prototype.PreSerialize();
      prototype.SerializeImplicitComponents(storeVisitor, out var selfView);

      foreach (var component in components) {
        component.Refresh();
        var proto = component.CreatePrototype(converter);
        proto.Dispatch(storeVisitor);
      }


      data.Asset.Prototypes.Add(container);
      data.MapEntityReferences.Add(selfView);
      components.Clear();
    }
  }

  private static IEnumerable<Type> FindCallbackInstances() {
    var searchAssemblies = QuantumEditorSettings.Instance.SearchAssemblies.Concat(QuantumEditorSettings.Instance.SearchEditorAssemblies).ToArray();
    return TypeUtils.GetSubClasses(typeof(MapDataBakerCallback), searchAssemblies)
                                 .OrderBy(t => {
                                   var attr = TypeUtils.GetAttribute<MapDataBakerCallbackAttribute>(t);
                                   if (attr != null)
                                     return attr.InvokeOrder;
                                   return 0;
                                 });
  }

  private static void InvokeCallbacks(string callbackName, MapData data, BuildTrigger buildTrigger, QuantumMapDataBakeFlags bakeFlags) {
    var bakeCallbacks = FindCallbackInstances();
    foreach (var callback in bakeCallbacks) {
      if (callback.IsAbstract == false) {
        try {
          switch (callbackName) {
            case "OnBeforeBake":
              (Activator.CreateInstance(callback) as MapDataBakerCallback).OnBeforeBake(data, buildTrigger, bakeFlags);
              break;
            default:
              Log.Warn($"Callback `{callbackName}` not found");
              break;
          }
        } catch (Exception exn) {
          Debug.LogException(exn);
        }
      }
    }
  }

  private static void InvokeCallbacks(string callbackName, MapData data) {
  var bakeCallbacks = FindCallbackInstances();
    foreach (var callback in bakeCallbacks) {
      if (callback.IsAbstract == false) {
        try {
          switch (callbackName) {
            case "OnBeforeBake":
              (Activator.CreateInstance(callback) as MapDataBakerCallback).OnBeforeBake(data);
              break;
            case "OnBake":
              (Activator.CreateInstance(callback) as MapDataBakerCallback).OnBake(data);
              break;
            case "OnBeforeBakeNavMesh":
              (Activator.CreateInstance(callback) as MapDataBakerCallback).OnBeforeBakeNavMesh(data);
              break;
            case "OnBakeNavMesh":
              (Activator.CreateInstance(callback) as MapDataBakerCallback).OnBakeNavMesh(data);
              break;
            default:
              Log.Warn($"Callback `{callbackName}` not found");
              break;
          }
        } catch (Exception exn) {
          Debug.LogException(exn);
        }
      }
    }
  }

  private static void InvokeCallbacks(string callbackName, MapData data, List<MapNavMesh.BakeData> bakeData) {
    var bakeCallbacks = FindCallbackInstances();
    foreach (var callback in bakeCallbacks) {
      if (callback.IsAbstract == false) {
        try {
          switch (callbackName) {
            case "OnCollectNavMeshBakeData":
              (Activator.CreateInstance(callback) as MapDataBakerCallback).OnCollectNavMeshBakeData(data, bakeData);
              break;
            default:
              Log.Warn($"Callback `{callbackName}` not found");
              break;
          }
        } catch (Exception exn) {
          Debug.LogException(exn);
        }
      }
    }
  }

  private static void InvokeCallbacks(string callbackName, MapData data, List<NavMesh> navmeshes) {
    var bakeCallbacks = FindCallbackInstances();
    foreach (var callback in bakeCallbacks) {
      if (callback.IsAbstract == false) {
        try {
          switch (callbackName) {
            case "OnCollectNavMeshes":
              (Activator.CreateInstance(callback) as MapDataBakerCallback).OnCollectNavMeshes(data, navmeshes);
              break;
            default:
              Log.Warn($"Callback `{callbackName}` not found");
              break;
          }
        } catch (Exception exn) {
          Debug.LogException(exn);
        }
      }
    }
  }

#if !QUANTUM_DISABLE_AI

  static IEnumerable<NavMesh> BakeNavMeshesLoop(MapData data) {
    MapNavMesh.InvalidateGizmos();

    var scene = data.gameObject.scene;
    Debug.Assert(scene.IsValid());

    var allBakeData = new List<MapNavMesh.BakeData>();

    // Collect unity navmeshes
    {
      var unityNavmeshes = data.GetComponentsInChildren<MapNavMeshUnity>().ToList();

      // The sorting is important to always generate the same order of regions name list.
      unityNavmeshes.Sort((a, b) => string.Compare(a.name, b.name, StringComparison.Ordinal));

      for (int i = 0; i < unityNavmeshes.Count; i++) {
        // If NavMeshSurface installed, this will deactivate non linked surfaces 
        // to make the CalculateTriangulation work only with the selected Unity navmesh.
        List<GameObject> deactivatedObjects = new List<GameObject>();

        try {
          if (unityNavmeshes[i].NavMeshSurfaces != null && unityNavmeshes[i].NavMeshSurfaces.Length > 0) {
            if (MapNavMesh.NavMeshSurfaceType != null) {
              var surfaces = FindLocalObjects(scene, MapNavMesh.NavMeshSurfaceType);
              foreach (MonoBehaviour surface in surfaces) {
                if (unityNavmeshes[i].NavMeshSurfaces.Contains(surface.gameObject) == false) {
                  surface.gameObject.SetActive(false);
                  deactivatedObjects.Add(surface.gameObject);
                }
              }
            }
          }

          var bakeData = MapNavMesh.ImportFromUnity(scene, unityNavmeshes[i].Settings, unityNavmeshes[i].name);
          if (bakeData == null) {
            Debug.LogErrorFormat("Could not import navmesh '{0}'", unityNavmeshes[i].name);
          } else {
            bakeData.Name             = unityNavmeshes[i].name;
            bakeData.AgentRadius      = MapNavMesh.FindSmallestAgentRadius(unityNavmeshes[i].NavMeshSurfaces);
            bakeData.EnableQuantum_XY = unityNavmeshes[i].Settings.EnableQuantum_XY;
            bakeData.ClosestTriangleCalculation      = unityNavmeshes[i].Settings.ClosestTriangleCalculation;
            bakeData.ClosestTriangleCalculationDepth = unityNavmeshes[i].Settings.ClosestTriangleCalculationDepth;
            bakeData.LinkErrorCorrection             = unityNavmeshes[i].Settings.LinkErrorCorrection;
            allBakeData.Add(bakeData);
          }
        }
        catch (Exception exn) {
          Debug.LogException(exn);
        }

        foreach (var go in deactivatedObjects) {
          go.SetActive(true);
        }
      }
    }

    // Collect navmeshes definitions
    {
      var navmeshDefinitions = data.GetComponentsInChildren<MapNavMeshDefinition>().ToList();

      // The sorting is important to always generate the same order of regions name list.
      navmeshDefinitions.Sort((a, b) => string.Compare(a.name, b.name, StringComparison.Ordinal));

      for (int i = 0; i < navmeshDefinitions.Count; i++) {
        try {
          allBakeData.Add(MapNavMeshDefinition.CreateBakeData(navmeshDefinitions[i]));
        }
        catch (Exception exn) {
          Debug.LogException(exn);
        }
      }
    }

    // Collect custom bake data
    InvokeCallbacks("OnCollectNavMeshBakeData", data, allBakeData);

    // Bake all collected bake data
    for (int i = 0; i < allBakeData.Count; i++) {
      var navmesh = default(NavMesh);
      var bakeData = allBakeData[i];
      if (bakeData == null) {
        Debug.LogErrorFormat("Navmesh bake data at index {0} is null", i);
        continue;
      }

      try {
        navmesh = MapNavMeshBaker.BakeNavMesh(data, bakeData);
        Debug.LogFormat("Baking Quantum NavMesh '{0}' complete ({1}/{2})", bakeData.Name, i + 1, allBakeData.Count);
      } catch (Exception exn) {
        Debug.LogException(exn);
      }

      if (navmesh != null) {
        yield return navmesh;
      } else {
        Debug.LogErrorFormat("Baking Quantum NavMesh '{0}' failed", bakeData.Name);
      }
    }
  }

#endif

  private static void SortBySiblingIndex<T>(T[] array) where T : Component {
    // sort by sibling indices; this should be uniform across machines
    List<int> list0 = new List<int>();
    List<int> list1 = new List<int>();
    Array.Sort(array, (a, b) => CompareLists(GetSiblingIndexPath(a.transform, list0), GetSiblingIndexPath(b.transform, list1)));
  }

  static List<int> GetSiblingIndexPath(Transform t, List<int> buffer) {
    buffer.Clear();
    while (t != null) {
      buffer.Add(t.GetSiblingIndex());
      t = t.parent;
    }

    buffer.Reverse();
    return buffer;
  }

  static int CompareLists(List<int> left, List<int> right) {
    while (left.Count > 0 && right.Count > 0) {
      if (left[0] < right[0]) {
        return -1;
      }

      if (left[0] > right[0]) {
        return 1;
      }

      left.RemoveAt(0);
      right.RemoveAt(0);
    }

    return 0;
  }

  static MapStaticCollider2D BakeStaticEdge2D(Transform t, FPVector2 positionOffset, FP rotationOffset, FPVector2 vertexA, FPVector2 vertexB, FP height, QuantumStaticColliderSettings settings, int colliderId) {
    var trs = Matrix4x4.TRS(t.TransformPoint(positionOffset.ToUnityVector3()), t.rotation * rotationOffset.FlipRotation().ToUnityQuaternionDegrees(), t.localScale);

    var start = trs.MultiplyPoint(vertexA.ToUnityVector3());
    var end   = trs.MultiplyPoint(vertexB.ToUnityVector3());

    var startToEnd = end - start;

    var pos = (start + end) / 2.0f;
    var rot = Quaternion.FromToRotation(Vector3.right, startToEnd);

    return new MapStaticCollider2D {
      Position = pos.ToFPVector2(),
      Rotation = rot.ToFPRotation2D(),
#if QUANTUM_XY
      VerticalOffset = -t.position.z.ToFP(),
      Height = height * t.localScale.z.ToFP(),
#else
      VerticalOffset = t.position.y.ToFP(),
      Height         = height * t.localScale.y.ToFP(),
#endif
      PhysicsMaterial = settings.PhysicsMaterial,
      StaticData      = GetStaticData(t.gameObject, settings, colliderId),
      Layer           = t.gameObject.layer,

      // edge
      ShapeType  = Quantum.Shape2DType.Edge,
      EdgeExtent = (startToEnd.magnitude / 2.0f).ToFP(),
    };
  }

  public static List<T> FindLocalObjects<T>(Scene scene) where T : Component {
    List<T> partialResult = new List<T>();
    List<T> fullResult = new List<T>();
    foreach (var gameObject in scene.GetRootGameObjects()) {
      // GetComponentsInChildren seems to clear the list first, but we're not going to depend
      // on this implementation detail
      if (!gameObject.activeInHierarchy)
        continue;
      partialResult.Clear();
      gameObject.GetComponentsInChildren(partialResult);
      fullResult.AddRange(partialResult);
    }
    return fullResult;
  }

  public static List<Component> FindLocalObjects(Scene scene, Type type) {
    List<Component> result = new List<Component>();
    foreach (var gameObject in scene.GetRootGameObjects()) {
      if (!gameObject.activeInHierarchy)
        continue;
      foreach (var component in gameObject.GetComponentsInChildren(type)) {
        result.Add(component);
      }
    }
    return result;
  }
}