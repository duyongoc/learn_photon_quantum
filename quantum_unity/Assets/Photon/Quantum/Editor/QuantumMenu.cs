

#region quantum_unity/Assets/Photon/Quantum/Editor/MenuItems/EditorSessionShutdown.cs
namespace Quantum.Editor {

  using UnityEditor;

  [InitializeOnLoad]
  public class QuantumEditorSessionShutdown {
    static QuantumEditorSessionShutdown() {
      EditorApplication.update += EditorUpdate;
    }

    static void EditorUpdate() {
      if (EditorApplication.isPlaying == false)
        QuantumRunner.ShutdownAll(true);
    }
  }
}
#endregion

#region quantum_unity/Assets/Photon/Quantum/Editor/MenuItems/GenerateLookUpTables.cs
namespace Quantum.Editor {

  using Photon.Deterministic;
  using UnityEditor;
  using UnityEngine;

  public static class GenerateLookUpTables {
    [MenuItem("Quantum/Generate Math Lookup Tables", false, 22)]
    public static void Generate() {
      FPLut.GenerateTables(PathUtils.Combine(Application.dataPath, "Photon/Quantum/Resources/LUT"));

      // this makes sure the tables are loaded into unity
      AssetDatabase.Refresh();
    }
  }
}
#endregion

#region quantum_unity/Assets/Photon/Quantum/Editor/MenuItems/QuantumAbout.cs
namespace Quantum.Editor {
  using System;
  using System.Diagnostics;
  using System.Reflection;
  using System.Text.RegularExpressions;
  using UnityEditor;

  public class QuantumAbout : EditorWindow {
    private static FileVersionInfo QuantumCoreDllInfo;

    [MenuItem("Window/Quantum/About")]
    [MenuItem("Quantum/About", false, -10)]
    public static void ShowWindow() {
      GetWindow(typeof(QuantumAbout), false, "Quantum About");
    }
    
    public virtual void OnGUI() {
      if (QuantumCoreDllInfo == null) {
        try {
          string codeBase = Assembly.GetAssembly(typeof(Quantum.Map)).CodeBase;
          UriBuilder uri = new UriBuilder(codeBase);
          string path = Uri.UnescapeDataString(uri.Path);
          QuantumCoreDllInfo = FileVersionInfo.GetVersionInfo(path);
        }
        catch { }
      }

      if (QuantumCoreDllInfo != null) {
        try {
          //2.1.0.0 Release 2.1/develop (01b43be9c)
          EditorGUILayout.LabelField("Quantum Core Dll", EditorStyles.boldLabel);
          var r = new Regex(@"(?<version>\d+(?:\.\d+){2,3})(?: (?<prerelease>[\w\d]+))? (?<configuration>[\w\-]+) (?<branch>[\d\.\w\/\-]+ \([\dabcdef]+\))");
          var matches = r.Matches(QuantumCoreDllInfo.ProductVersion);
          var groups = matches[0].Groups;
          EditorGUILayout.TextField("Version", $"{groups["version"].Value} {groups["prerelease"].Value}");
          EditorGUILayout.TextField("Configuration", groups["configuration"].Value);
          EditorGUILayout.TextField("Branch", groups["branch"].Value);
        } catch {
          EditorGUILayout.TextField(QuantumCoreDllInfo.ProductVersion);
        }
      }
    }
  }
}

#endregion

#region quantum_unity/Assets/Photon/Quantum/Editor/MenuItems/QuantumAutoBaker.cs
namespace Quantum.Editor {
  using System.Collections.Generic;
  using System.IO;
  using System.Linq;
  using UnityEditor;
  using UnityEditor.Build;
  using UnityEditor.Build.Reporting;
  using UnityEditor.SceneManagement;
  using UnityEngine;
  using UnityEngine.SceneManagement;
  using BuildTrigger = MapDataBaker.BuildTrigger;

  [InitializeOnLoad]
  public class QuantumAutoBaker : IProcessSceneWithReport {
    const int MenuItemPriority = 60;
    const string BakeMapDataAndNavMeshSuffix      = "MapData";
    const string BakeWithNavMeshSuffix            = "MapData with NavMesh";
    const string BakeWithUnityNavMeshImportSuffix = "MapData with Unity NavMesh Import";

    [MenuItem("Quantum/Bake/" + BakeMapDataAndNavMeshSuffix, false, MenuItemPriority)]
    public static void BakeCurrentScene_MapData() => BakeLoadedScenes(QuantumMapDataBakeFlags.BakeMapData | QuantumMapDataBakeFlags.GenerateAssetDB);
    [MenuItem("Quantum/Bake/" + BakeWithNavMeshSuffix, false, MenuItemPriority)]
    public static void BakeCurrentScene_NavMesh() => BakeLoadedScenes(QuantumMapDataBakeFlags.BakeMapData | QuantumMapDataBakeFlags.GenerateAssetDB | QuantumMapDataBakeFlags.BakeNavMesh);
    [MenuItem("Quantum/Bake/" + BakeWithUnityNavMeshImportSuffix, false, MenuItemPriority)]
    public static void BakeCurrentScene_ImportNavMesh() => BakeLoadedScenes(QuantumMapDataBakeFlags.BakeMapData | QuantumMapDataBakeFlags.GenerateAssetDB | QuantumMapDataBakeFlags.BakeNavMesh | QuantumMapDataBakeFlags.ImportUnityNavMesh | QuantumMapDataBakeFlags.BakeUnityNavMesh);

    [MenuItem("Quantum/Bake/All Scenes/" + BakeMapDataAndNavMeshSuffix, false, MenuItemPriority + 11)]
    public static void BakeAllScenes_MapData() => BakeAllScenes(QuantumMapDataBakeFlags.BakeMapData | QuantumMapDataBakeFlags.GenerateAssetDB);
    [MenuItem("Quantum/Bake/All Scenes/" + BakeWithNavMeshSuffix, false, MenuItemPriority + 11)]
    public static void BakeAllScenes_NavMesh() => BakeAllScenes(QuantumMapDataBakeFlags.BakeMapData | QuantumMapDataBakeFlags.GenerateAssetDB | QuantumMapDataBakeFlags.BakeNavMesh);
    [MenuItem("Quantum/Bake/All Scenes/" + BakeWithUnityNavMeshImportSuffix, false, MenuItemPriority + 11)]
    public static void BakeAllScenes_ImportNavMesh() => BakeAllScenes(QuantumMapDataBakeFlags.BakeMapData | QuantumMapDataBakeFlags.GenerateAssetDB | QuantumMapDataBakeFlags.BakeNavMesh | QuantumMapDataBakeFlags.ImportUnityNavMesh | QuantumMapDataBakeFlags.BakeUnityNavMesh);

    [MenuItem("Quantum/Bake/All Enabled Scenes/" + BakeMapDataAndNavMeshSuffix, false, MenuItemPriority + 12)]
    public static void BakeEnabledScenes_MapData() => BakeEnabledScenes(QuantumMapDataBakeFlags.BakeMapData | QuantumMapDataBakeFlags.GenerateAssetDB);
    [MenuItem("Quantum/Bake/All Enabled Scenes/" + BakeWithNavMeshSuffix, false, MenuItemPriority + 12)]
    public static void BakeEnabledScenes_NavMesh() => BakeEnabledScenes(QuantumMapDataBakeFlags.BakeMapData | QuantumMapDataBakeFlags.GenerateAssetDB | QuantumMapDataBakeFlags.BakeNavMesh);
    [MenuItem("Quantum/Bake/All Enabled Scenes/" + BakeWithUnityNavMeshImportSuffix, false, MenuItemPriority + 12)]
    public static void BakeEnabledScenes_ImportNavMesh() => BakeEnabledScenes(QuantumMapDataBakeFlags.BakeMapData | QuantumMapDataBakeFlags.GenerateAssetDB | QuantumMapDataBakeFlags.BakeNavMesh | QuantumMapDataBakeFlags.ImportUnityNavMesh | QuantumMapDataBakeFlags.BakeUnityNavMesh);

    private static void BakeLoadedScenes(QuantumMapDataBakeFlags flags) {
      for (int i = 0; i < EditorSceneManager.sceneCount; ++i) {
        BakeScene(EditorSceneManager.GetSceneAt(i), flags);
      }
    }

    private static void BakeAllScenes(QuantumMapDataBakeFlags flags) {
      var scenes = AssetDatabase.FindAssets("t:scene")
        .Select(x => AssetDatabase.GUIDToAssetPath(x));
      BakeScenes(scenes, flags);
    }

    private static void BakeEnabledScenes(QuantumMapDataBakeFlags flags) {
      var enabledScenes = EditorBuildSettings.scenes
          .Where(x => x.enabled)
          .Select(x => x.path);
      BakeScenes(enabledScenes, flags);
    }


    static QuantumAutoBaker() {
      EditorSceneManager.sceneSaving += OnSceneSaving;
      EditorApplication.playModeStateChanged += OnPlaymodeChange;
    }

    private static void OnPlaymodeChange(PlayModeStateChange change) {
      if (change != PlayModeStateChange.ExitingEditMode) {
        return;
      }
      for (int i = 0; i < EditorSceneManager.sceneCount; ++i) {
        AutoBakeMapData(EditorSceneManager.GetSceneAt(i), BuildTrigger.PlaymodeChange);
      }
    }

    private static void OnSceneSaving(Scene scene, string path) {
      AutoBakeMapData(scene, BuildTrigger.SceneSave);
    }

    private static void AutoBakeMapData(Scene scene, BuildTrigger buildTrigger) {
      var settings = QuantumEditorSettings.Instance;
      if (settings == null)
        return;

      Debug.LogFormat("Auto baking {0}", scene.path);

      switch (buildTrigger) {
        case BuildTrigger.Build:
          BakeScene(scene, settings.AutoBuildOnBuild, buildTrigger);
          break;
        case BuildTrigger.SceneSave:
          BakeScene(scene, settings.AutoBuildOnSceneSave, buildTrigger);
          break;
        case BuildTrigger.PlaymodeChange:
          BakeScene(scene, settings.AutoBuildOnPlaymodeChanged, buildTrigger);
          break;
      }
    }

    private static void BakeScenes(IEnumerable<string> scenes, QuantumMapDataBakeFlags mode) {
      if (mode == QuantumMapDataBakeFlags.None)
        return;

      if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) {
        return;
      }

      var currentScenes = Enumerable.Range(0, EditorSceneManager.sceneCount)
        .Select(x => EditorSceneManager.GetSceneAt(x))
        .Where(x => x.IsValid())
        .Select(x => new { Path = x.path, IsLoaded = x.isLoaded })
        .ToList();

      // we don't want to generate db for each scene
      bool generateDB = mode.HasFlag(QuantumMapDataBakeFlags.GenerateAssetDB);
      mode &= ~QuantumMapDataBakeFlags.GenerateAssetDB;

      try {
        var mapDataAssets = AssetDatabase.FindAssets($"t:{nameof(MapAsset)}")
            .Select(x => AssetDatabase.GUIDToAssetPath(x))
            .Select(x => AssetDatabase.LoadAssetAtPath<MapAsset>(x));

        var lookup = scenes
          .ToLookup(x => Path.GetFileNameWithoutExtension(x));

        foreach (var mapData in mapDataAssets) {

          var path = lookup[mapData.Settings.Scene].FirstOrDefault();
          if (string.IsNullOrEmpty(path))
            continue;

          var id = mapData.AssetObject?.Identifier;

          try {
            Debug.Log($"Baking map {id} (scene: {path})");

            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            if (!scene.IsValid())
              continue;

            BakeScene(scene, mode);
            EditorSceneManager.SaveOpenScenes();
          } catch (System.Exception ex) {
            Debug.LogError($"Error when baking map {id} (scene: {path}): {ex}");
          } 
        }
      } finally {
        var sceneLoadMode = OpenSceneMode.Single;
        foreach (var sceneInfo in currentScenes) {
          try {
            if (string.IsNullOrEmpty(sceneInfo.Path)) {
              continue;
            }
            var scene = EditorSceneManager.OpenScene(sceneInfo.Path, sceneLoadMode);
            if (scene.isLoaded && !sceneInfo.IsLoaded)
              EditorSceneManager.CloseScene(scene, false);
            sceneLoadMode = OpenSceneMode.Additive;
          } catch (System.Exception ex) {
            Debug.LogWarning($"Failed to restore scene: {sceneInfo.Path}: {ex}");
          }
        }
      }

      if (generateDB) {
        AssetDBGeneration.Generate();
      }
    }

    private static void BakeScene(Scene scene, QuantumMapDataBakeFlags mode, BuildTrigger buildTrigger = BuildTrigger.Manual) {
      if (mode == QuantumMapDataBakeFlags.None)
        return;

      var mapsData = scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<MapData>()).ToList();

      if (mapsData.Count == 1) {
        BakeMap(mapsData[0], mode, buildTrigger);
      } else if (mapsData.Count > 1) {
        Debug.LogError($"There are multiple {nameof(MapData)} components on scene {scene.name}. This is not supported.");
      }

      AssetDatabase.Refresh();
      
      if (mode.HasFlag(QuantumMapDataBakeFlags.GenerateAssetDB)) {
        AssetDBGeneration.Generate();
      }
    }

    public static void BakeMap(MapData data, QuantumMapDataBakeFlags buildFlags, BuildTrigger buildTrigger = BuildTrigger.Manual) {
      if (data.Asset == null)
        return;

#pragma warning disable CS0618 // Type or member is obsolete
      if (buildFlags.HasFlag(QuantumMapDataBakeFlags.Obsolete_BakeMapData)) {
#pragma warning restore CS0618 // Type or member is obsolete
        MapDataBaker.BakeMapData(data, true, 
          bakeColliders: true, 
          bakePrototypes: true,
          bakeFlags: buildFlags,
          buildTrigger: buildTrigger);
      } else if (buildFlags.HasFlag(QuantumMapDataBakeFlags.BakeMapPrototypes) || buildFlags.HasFlag(QuantumMapDataBakeFlags.BakeMapColliders)) {
        MapDataBaker.BakeMapData(data, true,
          bakeColliders: buildFlags.HasFlag(QuantumMapDataBakeFlags.BakeMapColliders),
          bakePrototypes: buildFlags.HasFlag(QuantumMapDataBakeFlags.BakeMapPrototypes),
          bakeFlags: buildFlags,
          buildTrigger: buildTrigger);
      }

#if !QUANTUM_DISABLE_AI
      
      if (buildFlags.HasFlag(QuantumMapDataBakeFlags.BakeUnityNavMesh)) {
        foreach (var navmeshDefinition in data.GetComponentsInChildren<MapNavMeshDefinition>()) {
          if (MapNavMeshEditor.BakeUnityNavmesh(navmeshDefinition.gameObject)) {
            break;
          }
        }

        foreach (var navmesh in data.GetComponentsInChildren<MapNavMeshUnity>()) {
          if (MapNavMeshEditor.BakeUnityNavmesh(navmesh.gameObject)) {
            break;
          }
        }
      }

      if (buildFlags.HasFlag(QuantumMapDataBakeFlags.ImportUnityNavMesh) || buildFlags.HasFlag(QuantumMapDataBakeFlags.BakeNavMesh)) {
        MapNavMeshEditor.UpdateDefaultMinAgentRadius();

        var sw = System.Diagnostics.Stopwatch.StartNew();

        if (buildFlags.HasFlag(QuantumMapDataBakeFlags.ImportUnityNavMesh)) {
          foreach (var navmeshDefinition in data.GetComponentsInChildren<MapNavMeshDefinition>()) {
            MapNavMeshDefinitionEditor.ImportFromUnity(navmeshDefinition);
          }
        }

        if (buildFlags.HasFlag(QuantumMapDataBakeFlags.BakeNavMesh)) {
          MapDataBaker.BakeNavMeshes(data, true);
        }

        Debug.Log($"Baking Quantum navmeshes took {sw.Elapsed.TotalSeconds:0.00} sec");
      }

      if (buildFlags.HasFlag(QuantumMapDataBakeFlags.ClearUnityNavMesh)) {
        foreach (var navmeshDefinition in data.GetComponentsInChildren<MapNavMeshDefinition>()) {
          if (MapNavMeshEditor.ClearUnityNavmesh(navmeshDefinition.gameObject)) {
            break;
          }
        }

        foreach (var navmesh in data.GetComponentsInChildren<MapNavMeshUnity>()) {
          if (MapNavMeshEditor.ClearUnityNavmesh(navmesh.gameObject)) {
            break;
          }
        }
      }

#endif

      EditorUtility.SetDirty(data);
      EditorUtility.SetDirty(data.Asset);

      if (buildFlags.HasFlag(QuantumMapDataBakeFlags.SaveUnityAssets)) {
        AssetDatabase.SaveAssets();
      }

      if (buildFlags.HasFlag(QuantumMapDataBakeFlags.GenerateAssetDB)) {
        AssetDBGeneration.Generate();
      }
    }

    int IOrderedCallback.callbackOrder => 0;

    void IProcessSceneWithReport.OnProcessScene(Scene scene, BuildReport report) {
      if (report == null)
        return;

      AutoBakeMapData(scene, BuildTrigger.Build);
    }
  }
}
#endregion

#region quantum_unity/Assets/Photon/Quantum/Editor/MenuItems/QuantumMenu.cs
namespace Quantum.Editor {
  using System;
  using UnityEditor;
  using UnityEngine;

  public static class QuantumMenu {
    [MenuItem("Assets/Open Quantum Project")]
    [MenuItem("Quantum/Open Quantum Project", false, 100)]
    private static void OpenQuantumProject() {
      var path = System.IO.Path.GetFullPath(QuantumEditorSettings.Instance.QuantumSolutionPath);

      if (!System.IO.File.Exists(path)) {
        EditorUtility.DisplayDialog("Open Quantum Project", "Solution file '" + path + "' not found. Check QuantumProjectPath in your QuantumEditorSettings.", "Ok");
      }

      var uri = new Uri(path);
      Application.OpenURL(uri.AbsoluteUri);
    }
  }
}
#endregion

#region quantum_unity/Assets/Photon/Quantum/Editor/MenuItems/QuantumObjectFactory.cs
namespace Quantum.Editor {
  using System.Linq;
  using System.Runtime.CompilerServices;
  using Photon.Deterministic;
  using UnityEditor;
  using UnityEngine;

  static class QuantumObjectFactory {

    private static Mesh _circleMesh;
    private static Mesh CircleMesh => LoadMesh("Circle", ref _circleMesh);

    private static Mesh _quadMesh;
    private static Mesh QuadMesh => LoadMesh("Quad", ref _quadMesh);


    [MenuItem("GameObject/Quantum/Empty Entity", false, 10)]
    private static void CreateEntity(MenuCommand mc) => new GameObject()
      .ThenAdd<global::EntityPrototype>(x => x.TransformMode = EntityPrototypeTransformMode.None)
      .ThenAdd<global::EntityView>()
      .Finish(mc);

    [MenuItem("GameObject/Quantum/2D/Sprite Entity", false, 10)]
#if QUANTUM_XY
    private static void CreateSpriteEntity(MenuCommand mc) => new GameObject()
      .ThenAdd<SpriteRenderer>()
      .ThenAdd<global::EntityPrototype>(x => x.TransformMode = EntityPrototypeTransformMode.Transform2D)
      .ThenAdd<global::EntityView>()
      .Finish(mc);
#else
    private static void CreateSpriteEntity(MenuCommand mc) => new GameObject()
      .ThenAlter<Transform>(x => {
        var child = new GameObject("Sprite");
        child.AddComponent<SpriteRenderer>();
        child.transform.rotation = Quaternion.AngleAxis(90.0f, Vector3.right);
        child.transform.SetParent(x, false);
      })
      .ThenAdd<global::EntityPrototype>(x => x.TransformMode = EntityPrototypeTransformMode.Transform2D)
      .ThenAdd<global::EntityView>()
      .Finish(mc, select: "Sprite");
#endif

    [MenuItem("GameObject/Quantum/2D/Quad Entity", false, 10)]
    private static void CreateQuadEntity(MenuCommand mc) => ObjectFactory.CreatePrimitive(PrimitiveType.Cube)
      .ThenRemove<Collider>()
      .ThenAlter<MeshFilter>(x => x.sharedMesh = QuadMesh)
      .ThenAdd<global::EntityPrototype>(x => {
        x.TransformMode = EntityPrototypeTransformMode.Transform2D;
        x.PhysicsCollider.IsEnabled = true;
        x.PhysicsCollider.Shape2D = new Shape2DConfig() {
          BoxExtents = FP._0_50 * FPVector2.One,
          ShapeType = Shape2DType.Box,
        };
      })
      .ThenAdd<global::EntityView>()
      .Finish(mc);


    [MenuItem("GameObject/Quantum/2D/Circle Entity", false, 10)]
    private static void CreateCircleEntity2D(MenuCommand mc) => ObjectFactory.CreatePrimitive(PrimitiveType.Sphere)
      .ThenRemove<Collider>()
      .ThenAlter<MeshFilter>(x => x.sharedMesh = CircleMesh)
      .ThenAdd<global::EntityPrototype>(x => {
        x.TransformMode = EntityPrototypeTransformMode.Transform2D;
        x.PhysicsCollider.IsEnabled = true;
        x.PhysicsCollider.Shape2D = new Shape2DConfig() {
          CircleRadius = FP._0_50,
          ShapeType = Shape2DType.Circle,
        };
      })
      .ThenAdd<global::EntityView>()
      .Finish(mc);

#if !QUANTUM_DISABLE_PHYSICS2D

    [MenuItem("GameObject/Quantum/2D/Static Quad Collider", false, 10)]
    private static void CreateQuadStaticCollider(MenuCommand mc) => ObjectFactory.CreatePrimitive(PrimitiveType.Cube)
      .ThenRemove<Collider>()
      .ThenAlter<MeshFilter>(x => x.sharedMesh = QuadMesh)
      .ThenAdd<QuantumStaticBoxCollider2D>(x => x.Size = FPVector2.One)
      .Finish(mc);

    [MenuItem("GameObject/Quantum/2D/Static Circle Collider", false, 10)]
    private static void CreateCircleStaticCollider(MenuCommand mc) => ObjectFactory.CreatePrimitive(PrimitiveType.Sphere)
      .ThenRemove<Collider>()
      .ThenAlter<MeshFilter>(x => x.sharedMesh = CircleMesh)
      .ThenAdd<QuantumStaticCircleCollider2D>(x => x.Radius = FP._0_50)
      .Finish(mc);

#endif

    [MenuItem("GameObject/Quantum/3D/Box Entity", false, 10)]
    private static void CreateBoxEntity(MenuCommand mc) => ObjectFactory.CreatePrimitive(PrimitiveType.Cube)
      .ThenRemove<Collider>()
      .ThenAdd<global::EntityPrototype>(x => {
        x.TransformMode = EntityPrototypeTransformMode.Transform3D;
        x.PhysicsCollider.IsEnabled = true;
        x.PhysicsCollider.Shape3D = new Shape3DConfig() {
          BoxExtents = FP._0_50 * FPVector3.One,
          ShapeType = Shape3DType.Box,
        };
      })
      .ThenAdd<global::EntityView>()
      .Finish(mc);

    [MenuItem("GameObject/Quantum/3D/Sphere Entity", false, 10)]
    private static void CreateSphereEntity(MenuCommand mc) => ObjectFactory.CreatePrimitive(PrimitiveType.Sphere)
      .ThenRemove<Collider>()
      .ThenAdd<global::EntityPrototype>(x => {
        x.TransformMode = EntityPrototypeTransformMode.Transform3D;
        x.PhysicsCollider.IsEnabled = true;
        x.PhysicsCollider.Shape3D = new Shape3DConfig() {
          SphereRadius = FP._0_50,
          ShapeType = Shape3DType.Sphere,
        };
      })
      .ThenAdd<global::EntityView>()
      .Finish(mc);

#if !QUANTUM_DISABLE_PHYSICS3D

    [MenuItem("GameObject/Quantum/3D/Static Box Collider", false, 10)]
    private static void CreateBoxStaticCollider(MenuCommand mc) => ObjectFactory.CreatePrimitive(PrimitiveType.Cube)
      .ThenRemove<Collider>()
      .ThenAdd<QuantumStaticBoxCollider3D>(x => x.Size = FPVector3.One)
      .Finish(mc);


    [MenuItem("GameObject/Quantum/3D/Static Sphere Collider", false, 10)]
    private static void CreateSphereStaticCollider(MenuCommand mc) => ObjectFactory.CreatePrimitive(PrimitiveType.Sphere)
      .ThenRemove<Collider>()
      .ThenAdd<QuantumStaticSphereCollider3D>(x => x.Radius = FP._0_50)
      .Finish(mc);
    
    [MenuItem("GameObject/Quantum/3D/Static Mesh Collider", false, 10)]
    private static void CreateMeshStaticCollider(MenuCommand mc) => ObjectFactory.CreatePrimitive(PrimitiveType.Cube)
      .ThenRemove<Collider>()
      .ThenAdd<QuantumStaticMeshCollider3D>()
      .Finish(mc);

#endif

    private static GameObject ThenRemove<T>(this GameObject go) where T : Component {
      UnityEngine.Object.DestroyImmediate(go.GetComponent<T>());
      return go;
    }

    private static GameObject ThenAdd<T>(this GameObject go, System.Action<T> callback = null) where T : Component {
      var component = go.AddComponent<T>();
      callback?.Invoke(component);
      return go;
    }

    private static GameObject ThenAlter<T>(this GameObject go, System.Action<T> callback) where T : Component {
      var component = go.GetComponent<T>();
      callback(component);
      return go;
    }

    private static void Finish(this GameObject go, MenuCommand mc, string select = null, [CallerMemberName] string callerName = null) {
      Debug.Assert(callerName.StartsWith("Create"));
      go.name = callerName.Substring("Create".Length);
      GameObjectUtility.SetParentAndAlign(go, mc.context as GameObject);
      Undo.RegisterCreatedObjectUndo(go, "Create " + go.name);
      if (!string.IsNullOrEmpty(select)) {
        Selection.activeObject = go.transform.Find(select)?.gameObject;
      } else {
        Selection.activeObject = go;
      }
    }

    private static Mesh LoadMesh(string name, ref Mesh field) {
      if (field != null)
        return field;
      var fullName = name;
#if QUANTUM_XY
      fullName += "XY";
#else
      fullName += "XZ";
#endif
      const string resourcePath = "QuantumShapes2D";

      field = UnityEngine.Resources.LoadAll<Mesh>(resourcePath).FirstOrDefault(x => x.name.Equals(fullName, System.StringComparison.OrdinalIgnoreCase));
      if (field == null) {
        Debug.LogError($"Mesh not found: {fullName} in resource {resourcePath}.");
      }
      return field;
    }

  }
}
#endregion

#region quantum_unity/Assets/Photon/Quantum/Editor/MenuItems/QuantumSettingsProvider.cs
namespace Quantum.Editor {
  using System;
  using System.Collections.Generic;
  using System.Linq;
  using UnityEditor;
  using UnityEngine;
#if UNITY_2019_1_OR_NEWER
using UnityEngine.UIElements;
#else
  using UnityEngine.Experimental.UIElements;
#endif

  public class QuantumSettingsProvider {

    [SettingsProvider]
    public static SettingsProvider CreateEditorSettings() {
      return CreateAssetSettingsProvider<QuantumEditorSettings>("Quantum/Editor Settings");
    }

    [SettingsProvider]
    public static SettingsProvider CreateDeterministicConfig() {
      return CreateAssetSettingsProvider<DeterministicSessionConfigAsset>("Quantum/Deterministic Config");
    }

    private static readonly MultiAssetSettingsProvider.PopupData ServerSettingsPopupData = new MultiAssetSettingsProvider.PopupData(typeof(PhotonServerSettings));
    private static readonly MultiAssetSettingsProvider.PopupData SimulationConfigPopupData = new MultiAssetSettingsProvider.PopupData(typeof(SimulationConfigAsset));
    private static readonly MultiAssetSettingsProvider.PopupData PhysicsMaterialPopupData = new MultiAssetSettingsProvider.PopupData(typeof(PhysicsMaterialAsset));
    private static readonly MultiAssetSettingsProvider.PopupData NavMeshAgentConfigPopupData = new MultiAssetSettingsProvider.PopupData(typeof(NavMeshAgentConfigAsset));
    private static readonly MultiAssetSettingsProvider.PopupData CharacterController2DPopupData = new MultiAssetSettingsProvider.PopupData(typeof(CharacterController2DConfigAsset));
    private static readonly MultiAssetSettingsProvider.PopupData CharacterController3DPopupData = new MultiAssetSettingsProvider.PopupData(typeof(CharacterController3DConfigAsset));

    [SettingsProvider]
    public static SettingsProvider CreatePhotonServerSettings() {
      return CreateMultiAssetSettingsProvider<PhotonServerSettings>("Quantum/Photon Server Settings", ServerSettingsPopupData);
    }

    [SettingsProvider]
    public static SettingsProvider CreateSimulationConfig() {
      return CreateMultiAssetSettingsProvider<SimulationConfigAsset>("Quantum/Simulation Config", SimulationConfigPopupData);
    }

    [SettingsProvider]
    public static SettingsProvider CreatePhysicsMaterials() {
      return CreateMultiAssetSettingsProvider<PhysicsMaterialAsset>("Quantum/Physics Materials", PhysicsMaterialPopupData);
    }

    [SettingsProvider]
    public static SettingsProvider CreateNavMeshAgentConfigs() {
      return CreateMultiAssetSettingsProvider<NavMeshAgentConfigAsset>("Quantum/Nav Mesh Agents", NavMeshAgentConfigPopupData);
    }

    [SettingsProvider]
    public static SettingsProvider CreateCharacterController2D() {
      return CreateMultiAssetSettingsProvider<CharacterController2DConfigAsset>("Quantum/Character Controller 2D", CharacterController2DPopupData);
    }

    [SettingsProvider]
    public static SettingsProvider CreateCharacterController3D() {
      return CreateMultiAssetSettingsProvider<CharacterController3DConfigAsset>("Quantum/Character Controller 3D", CharacterController3DPopupData);
    }

    private static SettingsProvider CreateAssetSettingsProvider<T>(string settingsWindowPath) where T : ScriptableObject {
      var assets = SearchAndLoadAsset<T>();
      if (assets.Count > 0) {
        var asset = SearchAndLoadAsset<T>()[0];
        var provider = AssetSettingsProvider.CreateProviderFromObject(settingsWindowPath, asset);
        provider.keywords = SettingsProvider.GetSearchKeywordsFromSerializedObject(new SerializedObject(asset));
        return provider;
      }

      return null;
    }

    private static SettingsProvider CreateMultiAssetSettingsProvider<T>(string settingsWindowPath, MultiAssetSettingsProvider.PopupData popupData) where T : ScriptableObject {
      var assets = SearchAndLoadAsset<T>();
      if (assets.Count > 0) {
        return new MultiAssetSettingsProvider(
          settingsWindowPath,
          () => MultiAssetSettingsProvider.CreateEditor(popupData),
          popupData,
          SettingsProvider.GetSearchKeywordsFromSerializedObject(new SerializedObject(assets[0])));
      }

      return null;
    }

    public static List<UnityEngine.Object> SearchAndLoadAsset<T>() where T : ScriptableObject {
      return SearchAndLoadAsset(typeof(T));
    }

    public static List<UnityEngine.Object> SearchAndLoadAsset(Type t) {
      string[] guids = AssetDatabase.FindAssets("t:" + t.Name, null);

      var selectedObjects = new List<UnityEngine.Object>();
      for (int i = 0; i < guids.Length; i++) {
        selectedObjects.Add(AssetDatabase.LoadAssetAtPath(AssetDatabase.GUIDToAssetPath(guids[i]), t));
      }

      return selectedObjects;
    }
  }

  public class MultiAssetSettingsProvider : AssetSettingsProvider {
    public class PopupData {
      public string[] OptionsDisplay;
      public string[] OptionsPath;
      public int CurrentIndex;
      public Type AssetType;

      public PopupData(Type assetType) {
        AssetType = assetType;
      }
    }

    public static UnityEditor.Editor CreateEditor(PopupData popupData) {
      if (popupData.OptionsPath.Length == 0) {
        return null;
      }

      return CreateEditorFromAssetPath(popupData.OptionsPath[popupData.CurrentIndex]);
    }

    private static UnityEditor.Editor CreateEditorFromAssetPath(string assetPath) {
      UnityEngine.Object[] targetObjects = AssetDatabase.LoadAllAssetsAtPath(assetPath);
      if (targetObjects != null)
        return UnityEditor.Editor.CreateEditor(targetObjects);
      return null;
    }

    private readonly PopupData _popupData;

    public MultiAssetSettingsProvider(string settingsWindowPath, Func<UnityEditor.Editor> editorCreator, PopupData popupData, IEnumerable<string> keywords) :
      base(settingsWindowPath, editorCreator, keywords) {
      _popupData = popupData;
    }

    public override void OnActivate(string searchContext, VisualElement rootElement) {
      var assets = QuantumSettingsProvider.SearchAndLoadAsset(_popupData.AssetType);
      _popupData.OptionsPath = assets.Select(a => AssetDatabase.GetAssetPath(a)).ToArray();
      _popupData.OptionsDisplay = _popupData.OptionsPath.Select(o => o.Replace("/", " \u2215 ")).ToArray();
      _popupData.CurrentIndex = Mathf.Min(_popupData.CurrentIndex, Mathf.Max(0, _popupData.OptionsPath.Length - 1));

      base.OnActivate(searchContext, rootElement);
    }

    public override void OnTitleBarGUI() {
      try {
        // Only because there are some nasty exceptions when an assets is deleted while being selected.
        base.OnTitleBarGUI();
      } catch (Exception e) {
        Debug.Log(e);
        Reload();
      }
    }

    public override void OnGUI(string searchContext) {
      if (string.IsNullOrEmpty(searchContext) && _popupData.OptionsDisplay.Length > 1) {
        using (new EditorGUI.IndentLevelScope()) {
          var newIndex = EditorGUILayout.Popup("Chose Asset:", _popupData.CurrentIndex, _popupData.OptionsDisplay);
          if (newIndex != _popupData.CurrentIndex) {
            _popupData.CurrentIndex = newIndex;
            Reload(searchContext);
          }
        }
      }

      base.OnGUI(searchContext);
    }

    private void Reload(string searchContext = null) {
      // Give me access to settingsEditor or creatorFunc and I won't have to do this:
      OnDeactivate();
      OnActivate(searchContext, null);
    }
  }
}
#endregion

#region quantum_unity/Assets/Photon/Quantum/Editor/MenuItems/QuantumShortcuts.cs
namespace Quantum.Editor {

  using Quantum;
  using System;
  using System.Collections.Generic;
  using UnityEditor;
  using UnityEngine;

  public class QuantumShortcuts : EditorWindow {
    public static float ButtonWidth = 200.0f;

    [MenuItem("Window/Quantum/Shortcuts")]
    [MenuItem("Quantum/Show Shortcuts", false, 43)]
    public static void ShowWindow() {
      GetWindow(typeof(QuantumShortcuts), false, "Quantum Shortcuts");
    }

    public class GridScope : IDisposable {
      private bool _endHorizontal;

      public GridScope(int columnCount, ref int currentColumn) {
        if (currentColumn % columnCount == 0) {
          GUILayout.BeginHorizontal();
        }

        _endHorizontal = ++currentColumn % columnCount == 0;
      }

      public void Dispose() {
        if (_endHorizontal) { 
          GUILayout.EndHorizontal();
        }
      }
    }

    public virtual void OnGUI() {
      var columnCount = (int)Mathf.Max(EditorGUIUtility.currentViewWidth / ButtonWidth, 1);
      var buttonWidth = EditorGUIUtility.currentViewWidth / columnCount;
      var currentColumn = 0;

      using (new GridScope(columnCount, ref currentColumn)) {
        if (GUI.Button(DrawIcon("Settings"), "Simulation Configs", EditorStyles.miniButton)) SearchAndSelect<SimulationConfigAsset>((long)DefaultAssetGuids.SimulationConfig);
      }
      using (new GridScope(columnCount, ref currentColumn)) {
        if (GUI.Button(DrawIcon("Grid Icon"), "Deterministic Configs", EditorStyles.miniButton)) SearchAndSelect<DeterministicSessionConfigAsset>();
      }
      using (new GridScope(columnCount, ref currentColumn)) {
        if (GUI.Button(DrawIcon("NetworkView Icon"), "Photon Server Settings", EditorStyles.miniButton)) SearchAndSelect<PhotonServerSettings>();
      }
      using (new GridScope(columnCount, ref currentColumn)) {
        if (GUI.Button(DrawIcon("BuildSettings.Editor.Small"), "Editor Settings", EditorStyles.miniButton)) SearchAndSelect<QuantumEditorSettings>("QuantumEditorSettings");
      }
      using (new GridScope(columnCount, ref currentColumn)) {
        if (GUI.Button(DrawIcon("PhysicMaterial Icon"), "Physics Materials", EditorStyles.miniButton)) SearchAndSelect<PhysicsMaterialAsset>();
      }
      using (new GridScope(columnCount, ref currentColumn)) {
        if (GUI.Button(DrawIcon("NavMeshData Icon"), "NavMesh Agent Configs", EditorStyles.miniButton)) SearchAndSelect<NavMeshAgentConfigAsset>();
      }
      using (new GridScope(columnCount, ref currentColumn)) {
        if (GUI.Button(DrawIcon("CapsuleCollider2D Icon"), "Character Controller 2D", EditorStyles.miniButton)) SearchAndSelect<CharacterController2DConfigAsset>();
      }
      using (new GridScope(columnCount, ref currentColumn)) {
        if (GUI.Button(DrawIcon("CapsuleCollider Icon"), "Character Controller 3D", EditorStyles.miniButton)) SearchAndSelect<CharacterController3DConfigAsset>();
      }
      using (new GridScope(columnCount, ref currentColumn)) {
        if (GUI.Button(DrawIcon("DefaultAsset Icon"), "Asset Database Window", EditorStyles.miniButton)) {
          var windows = (AssetDBInspector[])UnityEngine.Resources.FindObjectsOfTypeAll(typeof(AssetDBInspector));
          if (windows.Length > 0) {
            windows[0].Close();
          } else {
            AssetDBInspector window = (AssetDBInspector)GetWindow(typeof(AssetDBInspector), false, "Quantum Asset DB");
            window.Show();
          }
        }
      }
    }

    [Obsolete("Use DrawIcon() without width parameter")]
    public static Rect DrawIcon(string iconName, float width) {
      return DrawIcon(iconName);
    }

    public static Rect DrawIcon(string iconName) {
      var rect = EditorGUILayout.GetControlRect();
      var width = rect.width;
      rect.width = 20;
      EditorGUI.LabelField(rect, EditorGUIUtility.IconContent(iconName));
      rect.xMin  += rect.width;
      rect.width = width - rect.width;
      return rect;
    }

    public static T SearchAndSelect<T>() where T : UnityEngine.Object {
      var t = typeof(T);
      var guids = AssetDatabase.FindAssets("t:" + t.Name, null);
      if (guids.Length == 0) {
        Debug.LogFormat("No UnityEngine.Objects of type '{0}' found.", t.Name);
        return null;
      }

      var selectedObjects = new List<UnityEngine.Object>();
      for (int i = 0; i < guids.Length; i++) {
        selectedObjects.Add(AssetDatabase.LoadAssetAtPath(AssetDatabase.GUIDToAssetPath(guids[i]), t));
      }

      Selection.objects = selectedObjects.ToArray();
      return (T)selectedObjects[0];
    }


    public static T SearchAndSelect<T>(AssetGuid assetGuid) where T : UnityEngine.Object {
      var t = typeof(T);
      var guids = AssetDatabase.FindAssets("t:" + t.Name, null);
      if (guids.Length == 0) {
        Debug.LogFormat("No UnityEngine.Objects of type '{0}' found.", t.Name);
        return null;
      }

      if (guids.Length < 2) {
        return SearchAndSelect<T>();
      }

      T specificAsset = null;
      for (int i = 0; i < guids.Length; i++) {
        var asset = AssetDatabase.LoadAssetAtPath(AssetDatabase.GUIDToAssetPath(guids[i]), t);
        if (typeof(AssetBase).IsAssignableFrom(typeof(T)) &&
          ((AssetBase)asset).AssetObject.Identifier.Guid == assetGuid) {
          specificAsset = (T)asset;
          break;
        }
      }

      if (specificAsset == null || Selection.objects.Length == 1 && Selection.objects[0] == specificAsset) {
        return SearchAndSelect<T>();
      }

      Selection.objects = new UnityEngine.Object[1] { specificAsset };
      return specificAsset;
    }

    public static T SearchAndSelect<T>(string name) where T : UnityEngine.Object {
      var t = typeof(T);
      var guids = AssetDatabase.FindAssets("t:" + t.Name, null);
      if (guids.Length == 0) {
        Debug.LogFormat("No UnityEngine.Objects of type '{0}' found.", t.Name);
        return null;
      }

      if (guids.Length < 2) {
        return SearchAndSelect<T>();
      }

      T specificAsset = null;
      for (int i = 0; i < guids.Length; i++) {
        var asset = AssetDatabase.LoadAssetAtPath(AssetDatabase.GUIDToAssetPath(guids[i]), t);
        if (asset.name == name) { 
          specificAsset = (T)asset;
          break;
        }
      }

      if (specificAsset == null || Selection.objects.Length == 1 && Selection.objects[0] == specificAsset) {
        return SearchAndSelect<T>();
      }

      Selection.objects = new UnityEngine.Object[1] { specificAsset };
      return specificAsset;
    }
  }
}

#endregion

#region quantum_unity/Assets/Photon/Quantum/Editor/MenuItems/QuantumToolbarUtilities.cs
namespace Quantum.Editor {
  using System;
  using System.Collections.Generic;
  using System.IO;
  using System.Reflection;
  using UnityEngine;
  using UnityEditor;
  using UnityEditor.SceneManagement;
#if UNITY_2019_1_OR_NEWER
using UnityEngine.UIElements;
#else
  using UnityEngine.Experimental.UIElements;
#endif

  [InitializeOnLoad]
  public static class QuantumToolbarUtilities {
    private static ScriptableObject _toolbar;
    private static string[]         _scenePaths;
    private static string[]         _sceneNames;

    static QuantumToolbarUtilities() {
      EditorApplication.delayCall += () => {
        EditorApplication.update -= Update;
        EditorApplication.update += Update;
      };
    }

    private static void Update() {
      if (QuantumEditorSettings.InstanceFailSilently?.UseQuantumToolbarUtilities != true) {
        return;
      }

      if (_toolbar == null) {
        Assembly editorAssembly = typeof(UnityEditor.Editor).Assembly;

        UnityEngine.Object[] toolbars = UnityEngine.Resources.FindObjectsOfTypeAll(editorAssembly.GetType("UnityEditor.Toolbar"));
        _toolbar = toolbars.Length > 0 ? (ScriptableObject)toolbars[0] : null;
        if (_toolbar != null) {
#if UNITY_2021_1_OR_NEWER
        var root    = _toolbar.GetType().GetField("m_Root", BindingFlags.NonPublic | BindingFlags.Instance);
        var rawRoot = root.GetValue(_toolbar);
        var mRoot   = rawRoot as VisualElement;
        RegisterCallback(QuantumEditorSettings.Instance.QuantumToolbarZone.ToString(), OnGUI);

        void RegisterCallback(string root, Action cb) {
          var toolbarZone = mRoot.Q(root);
          if (toolbarZone != null) {
            var parent = new VisualElement() {
              style = {
                flexGrow = 1,
                flexDirection = FlexDirection.Row,
              }
            };
            var container = new IMGUIContainer();
            container.onGUIHandler += () => {
              cb?.Invoke();
            };
            parent.Add(container);
          toolbarZone.Add(parent);
          }
        }
#else
#if UNITY_2020_1_OR_NEWER
          var windowBackendPropertyInfo = editorAssembly.GetType("UnityEditor.GUIView").GetProperty("windowBackend", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
          var windowBackend = windowBackendPropertyInfo.GetValue(_toolbar);
          var visualTreePropertyInfo = windowBackend.GetType().GetProperty("visualTree", BindingFlags.Public| BindingFlags.Instance); 
          var visualTree = (VisualElement)visualTreePropertyInfo.GetValue(windowBackend); 
#else
          PropertyInfo visualTreePropertyInfo = editorAssembly.GetType("UnityEditor.GUIView").GetProperty("visualTree", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
          VisualElement visualTree             = (VisualElement)visualTreePropertyInfo.GetValue(_toolbar, null);
#endif

          IMGUIContainer container = (IMGUIContainer)visualTree[0];

          FieldInfo onGUIHandlerFieldInfo = typeof(IMGUIContainer).GetField("m_OnGUIHandler", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
          Action    handler               = (Action)onGUIHandlerFieldInfo.GetValue(container);
          handler -= OnGUI;
          handler += OnGUI;
          onGUIHandlerFieldInfo.SetValue(container, handler);
#endif
        }
      }

      if (_scenePaths == null || _scenePaths.Length != EditorBuildSettings.scenes.Length) {
        List<string> scenePaths = new List<string>();
        List<string> sceneNames = new List<string>();

        foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes) {
          if (scene.path == null || scene.path.StartsWith("Assets") == false)
            continue;

          string scenePath = Application.dataPath + scene.path.Substring(6);

          scenePaths.Add(scenePath);
          sceneNames.Add(Path.GetFileNameWithoutExtension(scenePath));
        }

        _scenePaths = scenePaths.ToArray();
        _sceneNames = sceneNames.ToArray();
      }
    }

    private static void OnGUI() {
      if (!QuantumEditorSettings.Instance.UseQuantumToolbarUtilities) {
        return;
      }

      using (new EditorGUI.DisabledScope(Application.isPlaying)) {

#if UNITY_2021_1_OR_NEWER == false
        Rect rect = new Rect(0, 0, Screen.width, Screen.height);
        switch (QuantumEditorSettings.Instance.QuantumToolbarAnchor) {
          case QuantumToolbarAnchor.Legacy:
            rect.xMin = EditorGUIUtility.currentViewWidth * 0.5f + 100.0f;
            rect.xMax = EditorGUIUtility.currentViewWidth - 350.0f;
            break;
          case QuantumToolbarAnchor.Center:
            rect.xMin = EditorGUIUtility.currentViewWidth * 0.5f - QuantumEditorSettings.Instance.QuantumToolbarSize  * 0.5f + QuantumEditorSettings.Instance.QuantumToolbarOffset;
            rect.xMax = rect.xMin + QuantumEditorSettings.Instance.QuantumToolbarSize;
            break;
          case QuantumToolbarAnchor.Left:
            rect.xMin = QuantumEditorSettings.Instance.QuantumToolbarOffset;
            rect.xMax = rect.xMin + QuantumEditorSettings.Instance.QuantumToolbarSize;
            break;
          case QuantumToolbarAnchor.Right:
            rect.xMin = EditorGUIUtility.currentViewWidth - QuantumEditorSettings.Instance.QuantumToolbarSize - QuantumEditorSettings.Instance.QuantumToolbarOffset;
            rect.xMax = rect.xMin + QuantumEditorSettings.Instance.QuantumToolbarSize;
            break;
        }

        rect.y = 8.0f;

        using (new GUILayout.AreaScope(rect))
#endif
        {
          string sceneName  = EditorSceneManager.GetActiveScene().name;
          int    sceneIndex = -1;

          for (int i = 0; i < _sceneNames.Length; ++i) {
            if (sceneName == _sceneNames[i]) {
              sceneIndex = i;
              break;
            }
          }

          int newSceneIndex = EditorGUILayout.Popup(sceneIndex, _sceneNames, GUILayout.Width(200.0f));
          if (newSceneIndex != sceneIndex) {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) {
              EditorSceneManager.OpenScene(_scenePaths[newSceneIndex], OpenSceneMode.Single);
            }
          }
        }
      }
    }
  }
}
#endregion