using System;
using Photon.Deterministic;
using Quantum;
using UnityEngine;
using UnityEngine.Serialization;
using EnumFlagsAttribute = Quantum.Inspector.EnumFlagsAttribute;

[CreateAssetMenu(menuName = "Quantum/Configurations/QuantumEditorSettings", fileName = "QuantumEditorSettings", order = EditorDefines.AssetMenuPriorityConfigurations)]
public class QuantumEditorSettings : ScriptableObject {

  private static QuantumEditorSettings _instance;

  public static QuantumEditorSettings Instance {
    get {
      if (_instance == null) {
        _instance = InstanceFailSilently;
      }
      if (_instance == null) {
        Debug.LogError("Can't find QuantumEditorSettings scriptable object in the Resource folders. Create a new instance via the create Asset menu.");
      }
      return _instance;
    }
  }

  public static QuantumEditorSettings InstanceFailSilently {
    get {
      if (_instance == null) {
        _instance = UnityEngine.Resources.Load<QuantumEditorSettings>("QuantumEditorSettings");
      }

      return _instance;
    }
  }

  [Flags, Serializable]
  public enum GizmosMode {
    None                 = 0,
    OnDraw               = 1 << 0,
    OnSelected           = 1 << 1,
    OnApplicationPlaying = 1 << 2,
  }

  [Obsolete("Use DefaultAssetSearchPath instead")]
  public string DatabasePath => DefaultAssetSearchPath;
  public string DefaultAssetSearchPath => AssetSearchPaths[0];

  [Tooltip("Path to asset resource db file.")]
  public string AssetResourcesPath = "Assets/Resources/AssetResources.asset";
  [Tooltip("These folders are scanned when creating the AssetResource collection. The first on is the default path for exported assets like map and navmesh.")]
  public string[] AssetSearchPaths = new string[] { "Assets/Resources/DB" };
  [Tooltip("Quantum scripts occasionally search for types in the Unity assemblies (e.g. MapDataBakerCallback). When using assembly definitions add them here.")]
  public string[] SearchAssemblies = new string[] { "Assembly-CSharp", "Assembly-CSharp-firstpass" };
  [Tooltip("See SearchAssemblies")]
  public string[] SearchEditorAssemblies = new string[] { "Assembly-CSharp-Editor", "Assembly-CSharp-Editor-firstpass" };

  [Header("Map")]
  public Color PhysicsGridColor = new Color(0.0f, 0.7f, 1f);
  public Color NavMeshGridColor = new Color(0.4f, 1.0f, 0.7f);

  [Header("Gizmos")]
  public FP GizmoIconScale = FP._1;

  [Header("Collider Gizmos")]
  public Boolean DrawStaticMeshTriangles = true;
  public Boolean DrawStaticMeshNormals = true;
  
  public Boolean DrawSceneMeshCells = true;
  public Boolean DrawSceneMeshTriangles = true;
  
  public Color StaticColliderColor = Quantum.ColorRGBA.ColliderGreen.ToColor();
  public Color DynamicColliderColor = Quantum.ColorRGBA.ColliderBlue.ToColor();
  public Color KinematicColliderColor = Quantum.ColorRGBA.White.ToColor();
  public Color CharacterControllerColor = Quantum.ColorRGBA.Yellow.ToColor();
  public Color AsleepColliderColor = Quantum.ColorRGBA.Gray.ToColor();
  public Color DisabledColliderColor = Quantum.ColorRGBA.Red.ToColor();

  [Header("Joint Gizmos")]
  [EnumFlags]
  public GizmosMode DrawJointGizmos = GizmosMode.OnSelected | GizmosMode.OnApplicationPlaying;
  public Color JointGizmosPrimaryColor   = new Color(0, 1, 0, 1.0f);
  public Color JointGizmosSecondaryColor = new Color(0, 1, 1, 1.0f);
  public Color JointGizmosWarningColor   = new Color(1, 0, 0, 1.0f);

  [Header("Prediction Culling Gizmos")]
  public Boolean DrawPredictionArea = true;
  public Color PredictionAreaColor = new Color(1, 0, 0, 0.25f);

  [Header("Pathfinder Gizmos")]
  public Boolean DrawPathfinderRawPath = false;
  public Boolean DrawPathfinderRawTrianglePath = false;
  public Boolean DrawPathfinderFunnel = false;

  [Header("NavMesh Agent Gizmos")]
  public Boolean DrawNavMeshAgents = false;
  public Color NavMeshAgentColor = Color.green;
  public Color NavMeshAvoidanceColor = Color.green * 0.5f;

  [Header("NavMesh Gizmos")]
  public Boolean DrawNavMesh = false;
  public Boolean DrawNavMeshBorders = false;
  public Boolean DrawNavMeshTriangleIds = false;
  public Boolean DrawNavMeshRegionIds = false;
  public Boolean DrawNavMeshVertexNormals = false;
  public Boolean DrawNavMeshLinks = false;
  public Color NavMeshDefaultColor = new Color(0.0f, 0.75f, 1.0f, 0.5f);
  public Color NavMeshRegionColor = new Color(1.0f, 0.0f, 0.5f, 0.5f);

  [Header("NavMesh Editor")]
  public Boolean DrawNavMeshDefinitionAlways = false;
  public Boolean DrawNavMeshDefinitionMesh = false;
  public Boolean DrawNavMeshDefinitionOptimized = false;


  [Header("Editor Features")]
  [Tooltip("Toggle the Quantum asset inspector. Completely disable it by defining DISABLE_QUANTUM_ASSET_EDITOR.")]
  public bool UseQuantumAssetInspector = true;
  [Tooltip("The post processor enables duplicating Quantum assets and prefabs and make sure a new guid and correct path are set. This can make especially batched processes slow and can be toggled off here.")]
  public bool UseAssetBasePostprocessor = true;
  [Tooltip("If enabled a scene loading dropdown is displayed next to the play button.")]
  public bool UseQuantumToolbarUtilities = true;
#if UNITY_2021_1_OR_NEWER
  [Tooltip("Toolbar Zone. Requires a domain reload after change.")]
  public QuantumToolbarZone QuantumToolbarZone = QuantumToolbarZone.ToolbarZoneRightAlign;
#else
  [Tooltip("Toolbar anchor.\nLegacy mode has a static position and ignores size and offset.")]
  public QuantumToolbarAnchor QuantumToolbarAnchor = QuantumToolbarAnchor.Legacy;
  [Range(0, 200)]
  [Tooltip("Toolbar size")]
  public float QuantumToolbarSize = 200;
  [Range(-2000, 2000)]
  [Tooltip("Toolbar offset")]
  public float QuantumToolbarOffset = 200;
#endif
  [Tooltip("If enabled a local PhotonPrivateAppVersion scriptable object is created to support the demo menu scene.")]
  public bool UsePhotonAppVersionsPostprocessor = true;
  [Tooltip("If enabled entity components are displayed inside of EntityPrototype inspector")]
  [FormerlySerializedAs("UseInlineEntityComponents")]
  public QuantumEntityComponentInspectorMode EntityComponentInspectorMode = QuantumEntityComponentInspectorMode.InlineInEntityPrototypeAndHideMonoBehaviours;

  [Range(2, 7)]
  [Tooltip("How many decimal places to round to when displaying FPs.")]
  public int FPDisplayPrecision = 5;

  [EnumFlags, Tooltip("Automatically trigger bake on saving a scene.")]
  public QuantumMapDataBakeFlags AutoBuildOnSceneSave = QuantumMapDataBakeFlags.BakeMapData;
  [EnumFlags, Tooltip("If set MapData will be automatically baked on entering play mode, on saving a scene and on building a player.")]
  public QuantumMapDataBakeFlags AutoBuildOnPlaymodeChanged = QuantumMapDataBakeFlags.BakeMapData;
  [EnumFlags, Tooltip("If set MapData will be automatically baked on entering play mode, on saving a scene and on building a player.")]
  public QuantumMapDataBakeFlags AutoBuildOnBuild = QuantumMapDataBakeFlags.BakeMapData;

  [Header("Quantum Solution Integration (changes require Unity restart)")]
  [Tooltip("Overwrite to use a different path from the Unity project to the quantum code solution.")]
  public string QuantumSolutionPath = "../quantum_code/quantum_code.sln";
  [Tooltip("If enabled projects from quantum_code.sln will get imported into Unity's generated Visual Studio Solution.")]
  public bool MergeWithVisualStudioSolution = false;
  [Tooltip("If enabled rebuilding Quantum will trigger Unity to import resulting DLLs immediately, not waiting for gaining focus.")]
  public bool ImportQuantumLibrariesImmediately = false;
  [Tooltip("If enabled any changes in .qtn files in quantum.code will run the codegen immediately.")]
  public bool AutoRunQtnCodeGen = false;


  [System.NonSerialized]
  private string databasePathInResources;

  [Obsolete("Use DatabasePathInResources")]
  public string ResourceDatabasePath => DatabasePathInResources;

  public string DatabasePathInResources {
    get {
      if (string.IsNullOrEmpty(databasePathInResources)) {
        // Make path relative to the Resource folder.
        if (!PathUtils.MakeRelativeToFolder(DefaultAssetSearchPath, "Resources", out databasePathInResources)) {
          Debug.LogError("Failed to make folder relative to Resources/ " + DefaultAssetSearchPath);
        }
      }
      return databasePathInResources;
    }
  }

  [System.NonSerialized]
  private string assetResourcesPathInResources;

  public string AssetResourcesPathInResources {
    get {
      if (string.IsNullOrEmpty(assetResourcesPathInResources)) {
        // Make path relative to the Resource folder.
        assetResourcesPathInResources = PathUtils.GetPathWithoutExtension(AssetResourcesPath);
        if (!PathUtils.MakeRelativeToFolder(assetResourcesPathInResources, "Resources", out assetResourcesPathInResources)) {
          Debug.LogError("Failed to make folder relative to Resources/ " + AssetResourcesPath);
        }
      }
      return assetResourcesPathInResources;
    }
  }

  public Color GetNavMeshColor(NavMeshRegionMask regionMask) {
    if (regionMask.IsMainArea) {
      return NavMeshDefaultColor;
    }
    return NavMeshRegionColor;
  }
}

[Serializable]
public enum QuantumToolbarAnchor {
  Legacy,
  Center,
  Left,
  Right
}

[Serializable]
public enum QuantumToolbarZone {
  ToolbarZoneRightAlign,
  ToolbarZoneLeftAlign
}

[Flags, Serializable]
public enum QuantumMapDataBakeFlags {
  None,
  [Obsolete("Use BakeMapData instead")]
  Obsolete_BakeMapData  = 1 << 0,
  BakeMapData           = BakeMapPrototypes | BakeMapColliders,
  BakeMapPrototypes     = 1 << 5,
  BakeMapColliders      = 1 << 6,

  BakeUnityNavMesh      = 1 << 3,
  ImportUnityNavMesh    = 1 << 2,
  BakeNavMesh           = 1 << 1,
  ClearUnityNavMesh     = 1 << 8,

  GenerateAssetDB       = 1 << 4,
  SaveUnityAssets       = 1 << 7,
}

public enum QuantumEntityComponentInspectorMode {
  ShowMonoBehaviours,
  InlineInEntityPrototypeAndShowMonoBehavioursStubs,
  InlineInEntityPrototypeAndHideMonoBehaviours,
}