

#region quantum_unity/Assets/Photon/Quantum/Editor/AssetPipeline/AssetBasePostprocessor.cs
namespace Quantum.Editor {
  using System;
  using System.Linq;
  using UnityEditor;
  using UnityEngine;
#if UNITY_2021_2_OR_NEWER
  using UnityEditor.SceneManagement;
#else
  using UnityEditor.Experimental.SceneManagement;
#endif

  public class AssetBasePostprocessor : AssetPostprocessor {

    private static int? _removeMonoBehaviourUndoGroup;
    private static int _reentryCount = 0;
    private const int MaxReentryCount = 3;

    [Flags]
    private enum ValidationResult {
      Ok,
      Dirty = 1,
      Invalidated = 2,
    }
    

    [InitializeOnLoadMethod]
    static void SetupVariantPrefabWorkarounds() {
      PrefabStage.prefabSaving += OnPrefabStageSaving;
      PrefabStage.prefabStageClosing += OnPrefabStageClosing;
    }

    static void OnPrefabStageClosing(PrefabStage stage) {

      var assetPath = stage.GetAssetPath();
      var prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
      if (PrefabUtility.GetPrefabAssetType(prefabAsset) != PrefabAssetType.Variant)
        return;

      // restore references
      ValidateQuantumAsset(assetPath, ignoreVariantPrefabWorkaround: true);
    }

    static void OnPrefabStageSaving(GameObject obj) {
      var stage = PrefabStageUtility.GetCurrentPrefabStage();
      if (stage == null)
        return;

      var assetPath = stage.GetAssetPath();
      var prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
      if (PrefabUtility.GetPrefabAssetType(prefabAsset) != PrefabAssetType.Variant)
        return;

      // nested assets of variant prefabs holding component references raise internal Unity error;
      // these references need to be cleared before first save
      var nestedAssets = AssetDatabase.LoadAllAssetsAtPath(assetPath).OfType<AssetBase>();
      foreach (var nestedAsset in nestedAssets) {
        if (nestedAsset is IQuantumPrefabNestedAsset == false)
          continue;
        NestedAssetBaseEditor.ClearParent(nestedAsset);
      }
    }

    static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths) {
      try {

        if (++_reentryCount > MaxReentryCount) {
          Debug.LogError("Exceeded max reentry count, possibly a bug");
          return;
        }

        if (QuantumEditorSettings.InstanceFailSilently?.UseAssetBasePostprocessor == true) {

#if UNITY_2020_1_OR_NEWER
          // this is a workaround for EditorUtility.SetDirty and AssetDatabase.SaveAssets not working nicely
          // with postprocessors; a dummy FindAssets call prior to SaveAssets seems to flush internal state and fix the issue
          AssetDatabase.FindAssets($"t:AssetThatDoesNotExist", QuantumEditorSettings.Instance.AssetSearchPaths);
#endif

          var result = ValidationResult.Ok;

          foreach (var importedAsset in importedAssets) {
            result |= ValidateQuantumAsset(importedAsset);
          }

          foreach (var movedAsset in movedAssets) {
            result |= ValidateQuantumAsset(movedAsset);
          }

          for (int i = 0; result == ValidationResult.Ok && i < deletedAssets.Length; i++) {
            if (QuantumEditorSettings.Instance.AssetSearchPaths.Any(p => deletedAssets[i].StartsWith(p))) {
              result |= ValidationResult.Invalidated;
            }
          }

#if !UNITY_2020_1_OR_NEWER
          if (result.HasFlag(ValidationResult.Dirty)) {
            AssetDatabase.SaveAssets();
          }
#endif

          if (result.HasFlag(ValidationResult.Invalidated) || result.HasFlag(ValidationResult.Dirty)) {
            AssetDBGeneration.OnAssetDBInvalidated?.Invoke();
          }

#if UNITY_2020_1_OR_NEWER
          if (result.HasFlag(ValidationResult.Dirty)) {
            AssetDatabase.SaveAssets();
          }
#endif
        }
      } finally {
        --_reentryCount;
      }
    }

    private static ValidationResult ValidateQuantumAsset(string path, bool ignoreVariantPrefabWorkaround = false) {

      var result = ValidationResult.Ok;

      for (int i = 0; i < QuantumEditorSettings.Instance.AssetSearchPaths.Length; i++) {
        if (path.StartsWith(QuantumEditorSettings.Instance.AssetSearchPaths[i])) {
          var mainAsset = AssetDatabase.LoadMainAssetAtPath(path);

          if (mainAsset is AssetBase asset) {
            result |= ValidateAsset(asset, path);
          } else if (mainAsset is GameObject prefab) {

            if (!ignoreVariantPrefabWorkaround) {
              // there is some weirdness in how Unity handles variant prefabs; basically you can't reference any components
              // externally in that stage, or you'll get an internal error
              if (PrefabUtility.GetPrefabAssetType(prefab) == PrefabAssetType.Variant && PrefabStageUtility.GetCurrentPrefabStage()?.GetAssetPath() == path) {
                break;
              }
            }

            result |= ValidatePrefab(prefab, path);
          }
          else if (mainAsset is SceneAsset) {
            continue;
          }

          var nestedAssets = AssetDatabase.LoadAllAssetsAtPath(path)
            .OfType<AssetBase>()
            .Where(x => x != mainAsset);

          foreach (var nestedAsset in nestedAssets) {
            result |= ValidateAsset(nestedAsset, path);
          }
        }
      }

      return result;
    }

    private static ValidationResult ValidateAsset(AssetBase asset, string assetPath) {
      Debug.Assert(!string.IsNullOrEmpty(assetPath));

      if (asset.IsTransient) {
        // fully transient
        return ValidationResult.Ok;
      }

      var correctPath = asset.GenerateDefaultPath(assetPath);

      ValidationResult result = ValidationResult.Ok;

      if (string.IsNullOrEmpty(asset.AssetObject.Path)) {
        asset.AssetObject.Path = correctPath;
        result |= ValidationResult.Dirty;
      } else {
        if (!asset.AssetObject.Path.Equals(correctPath)) {
          // possible duplication
          var sourceAssetPath = asset.AssetObject.Path;

          // ditch everything after the separator 
          var separatorIndex = sourceAssetPath.LastIndexOf(AssetBase.NestedPathSeparator);
          if (separatorIndex >= 0) {
            sourceAssetPath = sourceAssetPath.Substring(0, separatorIndex);
          }

          var wasCloned = AssetDatabase.LoadAllAssetsAtPath($"Assets/{sourceAssetPath}.asset")
            .Concat(AssetDatabase.LoadAllAssetsAtPath($"Assets/{sourceAssetPath}.prefab"))
            .OfType<AssetBase>()
            .Any(x => x.AssetObject?.Guid == asset.AssetObject.Guid);

          if (wasCloned) {
            var newGuid = AssetGuid.NewGuid();
            Debug.LogFormat(asset, "Asset {0} ({3}) appears to have been cloned, assigning new id: {1} (old id: {2})", assetPath, newGuid, asset.AssetObject.Guid, asset.GetType());
            asset.AssetObject.Guid = newGuid;
            result |= ValidationResult.Invalidated;
          }

          asset.AssetObject.Path = correctPath;
          result |= ValidationResult.Dirty;
        }
      }

      if (!asset.AssetObject.Guid.IsValid) {
        asset.AssetObject.Guid = AssetGuid.NewGuid();
        result |= ValidationResult.Dirty;
        result |= ValidationResult.Invalidated;
      }

      if (result.HasFlag(ValidationResult.Dirty)) {
        EditorUtility.SetDirty(asset);
      }

      return result;
    }

    private static ValidationResult ValidatePrefab(GameObject prefab, string prefabPath) {
      Debug.Assert(!string.IsNullOrEmpty(prefabPath));
      var result = ValidationResult.Ok;

      var existingNestedAssets = AssetDatabase.LoadAllAssetsAtPath(prefabPath).OfType<IQuantumPrefabNestedAsset>().ToList();

      foreach (var component in prefab.GetComponents<MonoBehaviour>()) {
        if (component == null) {
          Debug.LogWarning($"Asset {prefab} has a missing component", prefab);
          continue;
        }

        if ( component is IQuantumPrefabNestedAssetHost host ) {
          var nestedAssetType = host.NestedAssetType;

          if (nestedAssetType == null || nestedAssetType.IsAbstract) {
            Debug.LogError($"{component.GetType().FullName} component's NestedAssetType property is either null or abstract, unable to create.", prefab);
            continue;
          }

          if (NestedAssetBaseEditor.EnsureExists(component, nestedAssetType, out var nested, save: false)) {
            // saving will trigger the postprocessor again
            result |= ValidationResult.Dirty;
          }

          existingNestedAssets.Remove(nested);
        }
      }

      foreach (var orphaned in existingNestedAssets) {
        var obj = (AssetBase)orphaned;
        Debug.LogFormat("Deleting orphaned nested asset: {0} (in {1})", obj, prefabPath);
        if (Undo.GetCurrentGroupName() == "Remove MonoBehaviour" || _removeMonoBehaviourUndoGroup == Undo.GetCurrentGroup()) {
          // special case: when component gets removed with context menu, we want to be able to restore
          // asset with the original guid
          _removeMonoBehaviourUndoGroup = Undo.GetCurrentGroup();
          Undo.DestroyObjectImmediate(obj);
        } else {
          _removeMonoBehaviourUndoGroup = null;
          UnityEngine.Object.DestroyImmediate(obj, true);
        }
        result |= ValidationResult.Dirty;
      }

      if (result.HasFlag(ValidationResult.Dirty)) {
        EditorUtility.SetDirty(prefab);
      }

      return result;
    }
  }

  static class PrefabStageExtensions {
    public static string GetAssetPath(this PrefabStage stage) {
#if UNITY_2020_1_OR_NEWER
      return stage.assetPath;
#else
      return stage.prefabAssetPath;
#endif
    }
  }
}

#endregion

#region quantum_unity/Assets/Photon/Quantum/Editor/AssetPipeline/AssetDBGeneration.cs
namespace Quantum.Editor {
  using System;
  using UnityEngine;
  using UnityEditor;
  using System.Collections.Generic;
  using System.IO;
  using System.Linq;
  using System.Reflection;

  public static partial class AssetDBGeneration {
    // Reset this when other behavior than an immediate AssetDB is desired after creating new or changing Quantum asset guids.
    public static Action OnAssetDBInvalidated = Generate;

    [MenuItem("Quantum/Generate Asset Resources", true, 21)]
    public static bool GenerateValidation() {
      return !Application.isPlaying;
    }

    [MenuItem("Quantum/Generate Asset Resources", false, 21)]
    public static void Generate() {

      if (Application.isPlaying) {
        return;
      }

      // This part will ensure that every prefab has a nested Quantum asset
      {
        var dirtyAssets = false;
        var prefabGuids = AssetDatabase.FindAssets("t:Prefab", QuantumEditorSettings.Instance.AssetSearchPaths);
        foreach (var prefabGuid in prefabGuids) {
          var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(prefabGuid));
          Debug.Assert(prefab != null);
          // TODO: ensure for each component
        }

        if (dirtyAssets) {
          AssetDatabase.SaveAssets();
        }
      }

      var allAssets = GatherQuantumAssets();

      // Overwrite the resource container and add found assets
      {
        var container = AssetDatabase.LoadAssetAtPath<AssetResourceContainer>(QuantumEditorSettings.Instance.AssetResourcesPath);
        if (container == null) {
          container = ScriptableObject.CreateInstance<AssetResourceContainer>();
          AssetDatabase.CreateAsset(container, QuantumEditorSettings.Instance.AssetResourcesPath);
        }

        var guidMap = new Dictionary<AssetGuid, AssetBase>();
        var pathMap = new Dictionary<string, AssetBase>();

        var createResourceInfoDelegates = new List<Delegate>();

        foreach (var group in container.Groups) {
          group.Clear();

          var generatorType = typeof(Func<,,>).MakeGenericType(group.GetType(), typeof(AssetContext), typeof(AssetResourceInfo));
          createResourceInfoDelegates.Add(typeof(AssetDBGeneration).CreateMethodDelegate(nameof(TryCreateResourceInfo), BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, generatorType));
        }

        var context = new AssetContext();
        foreach (var asset in allAssets) {
          if (asset != null) {

            // Check for duplicates
            {
              if (guidMap.TryGetValue(asset.AssetObject.Guid, out var conflicting)) {
                Debug.LogError($"Duplicated guid {asset.AssetObject.Guid} found and skipping asset at path '{asset.AssetObject.Path}'. Conflicting asset: {AssetDatabase.GetAssetPath(conflicting)}", asset);
                continue;
              }

              if (pathMap.TryGetValue(asset.AssetObject.Path, out conflicting)) {
                Debug.LogError($"Duplicated path '{asset.AssetObject.Path}' found and skipping asset with guid {asset.AssetObject.Guid}. Conflicting asset: {AssetDatabase.GetAssetPath(conflicting)}", asset);
                continue;
              }
            }

            guidMap.Add(asset.AssetObject.Guid, asset);
            pathMap.Add(asset.AssetObject.Path, asset);
            context.Asset = asset;

            bool found = false;
            for ( int i = 0; i < container.Groups.Count; ++i) {
              var group = container.Groups[i];
              var info = (AssetResourceInfo)createResourceInfoDelegates[i].DynamicInvoke(group, context);

              if (info != null) {
                info.Guid = asset.AssetObject.Guid;
                info.Path = asset.AssetObject.Path;
                group.Add(info);
                found = true;
                break;
              }
            }

            if (!found) {
              Debug.LogError($"Failed to find a resource group for {asset.AssetObject.Identifier}. " +
                $"Make sure this asset is either in Resources, has an AssetBundle assigned, is an Addressable (if QUANTUM_ADDRESSABLES is defined) " +
                $"or add your own custom group to handle it.", asset);
              continue;
            }
          }
        }

        EditorUtility.SetDirty(container);
      }

      UnityDB.Dispose();

      Debug.Log("Rebuild Quantum Asset DB");
    }

    public static List<AssetBase> GatherQuantumAssets() {
      var allAssets = new List<AssetBase>();
      {
        var assetGuids = AssetDatabase.FindAssets($"t:{nameof(AssetBase)}", QuantumEditorSettings.Instance.AssetSearchPaths);
        foreach (var assetGuid in assetGuids.Distinct()) {
          foreach (var assetBase in AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GUIDToAssetPath(assetGuid)).OfType<AssetBase>()) {
            if (assetBase is IQuantumPrefabNestedAsset nested) {
              // check if this asset is split
              if (!assetBase.IsTransient && NestedAssetBaseEditor.HasPrefabAsset(nested)) {
                continue;
              }
            }
            allAssets.Add(assetBase);
          }
        }

        foreach (var assetBase in allAssets) {
          if (assetBase?.IsTransient == false) {

            var assetPath = AssetDatabase.GetAssetPath(assetBase);

            // Fix invalid guid ids.
            if (!assetBase.AssetObject.Guid.IsValid) {
              var assetFileName = Path.GetFileName(assetPath);

              // Recover default config settings
              switch (assetFileName) {
                case "DefaultCharacterController2D":
                  assetBase.AssetObject.Guid = (long)DefaultAssetGuids.CharacterController2DConfig;
                  break;
                case "DefaultCharacterController3D":
                  assetBase.AssetObject.Guid = (long)DefaultAssetGuids.CharacterController3DConfig;
                  break;
                case "DefaultNavMeshAgentConfig":
                  assetBase.AssetObject.Guid = (long)DefaultAssetGuids.NavMeshAgentConfig;
                  break;
                case "DefaultPhysicsMaterial":
                  assetBase.AssetObject.Guid = (long)DefaultAssetGuids.PhysicsMaterial;
                  break;
                case "SimulationConfig":
                  assetBase.AssetObject.Guid = (long)DefaultAssetGuids.SimulationConfig;
                  break;
                default:
                  assetBase.AssetObject.Guid = AssetGuid.NewGuid();
                  break;
              }

              Debug.LogWarning($"Generated a new guid {assetBase.AssetObject.Guid} for asset at path '{assetPath}'");
              EditorUtility.SetDirty(assetBase);
            }

            // Fix invalid paths
            var correctPath = assetBase.GenerateDefaultPath(assetPath);
            if (string.IsNullOrEmpty(assetBase.AssetObject.Path) || assetBase.AssetObject.Path != correctPath) {
              assetBase.AssetObject.Path = correctPath;

              Debug.LogWarning($"Generated a new path '{assetBase.AssetObject.Path}' for asset {assetBase.AssetObject.Guid}");

              if (string.IsNullOrEmpty(assetBase.AssetObject.Path)) {
                Debug.LogError($"Asset '{assetBase.AssetObject.Guid}' is not added to the Asset DB because it does not have a valid path");
                continue;
              } else {
                EditorUtility.SetDirty(assetBase);
              }
            }
          }
        }
      }

      allAssets.Sort((a, b) => a.AssetObject.Guid.CompareTo(b.AssetObject.Guid));

      return allAssets;
    }

    static AssetResourceInfo TryCreateResourceInfo(AssetResourceContainer.AssetResourceInfoGroup_Resources group, AssetContext context) {
      if (PathUtils.MakeRelativeToFolder(context.UnityAssetPath, "Resources", out var resourcePath)) {
        // drop the extension
        return new AssetResourceContainer.AssetResourceInfo_Resources() {
          ResourcePath = PathUtils.GetPathWithoutExtension(resourcePath)
        };
      }
      return null;
    }

    static AssetResourceInfo TryCreateResourceInfo(AssetResourceContainer.AssetResourceInfoGroup_AssetBundle group, AssetContext context) {
      var assetBundleName = AssetDatabase.GetImplicitAssetBundleName(context.UnityAssetPath);
      if (!string.IsNullOrEmpty(assetBundleName)) {
        return new AssetResourceContainer.AssetResourceInfo_AssetBundle() {
          AssetBundle = assetBundleName,
          AssetName = Path.GetFileName(context.UnityAssetPath),
        };
      }
      return null;
    }

    partial class AssetContext {
      public AssetBase Asset { get; set; }
      public bool IsMainAsset      => AssetDatabase.IsMainAsset(Asset);
      public string UnityAssetPath => AssetDatabase.GetAssetPath(Asset);
      public string UnityAssetGuid => AssetDatabase.AssetPathToGUID(UnityAssetPath);
      public string QuantumPath    => Asset.AssetObject.Path;
      public AssetGuid QuantumGuid => Asset.AssetObject.Guid;
    }
  }
}

#endregion

#region quantum_unity/Assets/Photon/Quantum/Editor/AssetPipeline/AssetDBGeneration_Addressables.cs
#if QUANTUM_ADDRESSABLES

namespace Quantum.Editor {

  using System.Collections.Generic;
  using System.Linq;
  using UnityEditor.AddressableAssets;
  using UnityEditor.AddressableAssets.Settings;

  public static partial class AssetDBGeneration {

    partial class AssetContext {
      public ILookup<string, AddressableAssetEntry> GuidToParentAddressable = CreateAddressablesLookup();
    }

    private static AssetResourceInfo TryCreateResourceInfo(AssetResourceContainer.AssetResourceInfoGroup_Addressables group, AssetContext context) {
      var addressableEntry = context.GuidToParentAddressable[context.UnityAssetGuid].SingleOrDefault();
      if (addressableEntry != null) {
        string address = addressableEntry.address;
        if (!context.IsMainAsset) {
          address += $"[{context.Asset.name}]";
        }
        return new AssetResourceContainer.AssetResourceInfo_Addressables() {
          Address = address
        };
      }

      return null;
    }

    public static ILookup<string, AddressableAssetEntry> CreateAddressablesLookup() {
      var assetList = new List<AddressableAssetEntry>();
      var assetsSettings = AddressableAssetSettingsDefaultObject.Settings;

      if (assetsSettings == null) {
        throw new System.InvalidOperationException("Unable to load Addressables settings. This may be due to an outdated Addressables version.");
      }

      foreach (var settingsGroup in assetsSettings.groups) {
        if (settingsGroup.ReadOnly)
          continue;
        settingsGroup.GatherAllAssets(assetList, true, true, true);
      }

      return assetList.Where(x => !string.IsNullOrEmpty(x.guid)).ToLookup(x => x.guid);
    }
  }
}

#endif
#endregion

#region quantum_unity/Assets/Photon/Quantum/Editor/AssetPipeline/AssetDBGeneration_Export.cs
namespace Quantum.Editor {

  using System.IO;
  using System.Linq;
  using UnityEditor;
  using UnityEngine;

  public static partial class AssetDBGeneration {
    private static string AssetDBLocation {
      get => EditorPrefs.GetString("Quantum_Export_LastDBLocation");
      set => EditorPrefs.SetString("Quantum_Export_LastDBLocation", value);
    }

    /// <summary>
    /// Discovers Quantum assets in the project, identically to the Asset DB Resource file, and export the data into JSON.
    /// </summary>
    [MenuItem("Quantum/Export/Asset DB %#t", false, 3)]
    public static void Export() {
      var lastLocation = AssetDBLocation;
      var directory = string.IsNullOrEmpty(lastLocation) ? Application.dataPath : Path.GetDirectoryName(lastLocation);
      var defaultName = string.IsNullOrEmpty(lastLocation) ? "db" : Path.GetFileName(lastLocation);

      var filePath = EditorUtility.SaveFilePanel("Save db to..", directory, defaultName, "json");
      if (!string.IsNullOrEmpty(filePath)) {
        Export(filePath);
        AssetDBLocation = filePath;
      }
    }

    public static void Export(string filePath) {
      var allAssets = GatherQuantumAssets();

      var allAssetObjects = allAssets.Select(a => {
        a.PrepareAsset();
        return a.AssetObject;
      });

      var serializer = new QuantumUnityJsonSerializer() { IsPrettyPrintEnabled = false };
      File.WriteAllBytes(filePath, serializer.SerializeAssets(allAssetObjects));
    }

    [MenuItem("Quantum/Export/Asset DB (Through UnityDB)", true, 3)]
    public static bool ExportThroughUnityDB_Validate() {
      return Application.isPlaying;
    }

    /// <summary>
    /// Use this when the asset loading has been customized by code that requires to start the game.
    /// </summary>
    [MenuItem("Quantum/Export/Asset DB (Through UnityDB)", false, 3)]
    public static void ExportThroughUnityDB() {
      var lastLocation = AssetDBLocation;
      var directory = string.IsNullOrEmpty(lastLocation) ? Application.dataPath : Path.GetDirectoryName(lastLocation);
      var defaultName = string.IsNullOrEmpty(lastLocation) ? "db" : Path.GetFileName(lastLocation);

      var filePath = EditorUtility.SaveFilePanel("Save db to..", directory, defaultName, "json");
      if (!string.IsNullOrEmpty(filePath)) {
        ExportThroughUnityDB(filePath);
        AssetDBLocation = filePath;
      }
    }

    public static void ExportThroughUnityDB(string filePath) {
      var serializer = new QuantumUnityJsonSerializer() { IsPrettyPrintEnabled = true };
      var assetObjectUnityDB = UnityDB.DefaultResourceManager.LoadAllAssets(true).ToList();
      assetObjectUnityDB.Sort((a, b) => a.Guid.CompareTo(b.Guid));
      File.WriteAllBytes(filePath, serializer.SerializeAssets(assetObjectUnityDB));
    }
  }
}

#endregion

#region quantum_unity/Assets/Photon/Quantum/Editor/AssetPipeline/QuantumPrefabAssetImporter.cs
namespace Quantum.Editor {
  using System;
  using System.IO;
  using System.Linq;
  using UnityEditor;
#if UNITY_2020_2_OR_NEWER
  using UnityEditor.AssetImporters;
#else
  using UnityEditor.Experimental.AssetImporters;
#endif
  using UnityEngine;

#if QUANTUM_ADDRESSABLES
  using UnityEngine.AddressableAssets;
#endif


  [ScriptedImporter(2, Extension, 100000)]
  public partial class QuantumPrefabAssetImporter : ScriptedImporter {
    public const string Extension = "qprefab";
    public const string ExtensionWithDot = ".qprefab";
    public const string Suffix = "_data";

    public static string GetPath(string prefabPath) {
      var directory = Path.GetDirectoryName(prefabPath);
      var name = Path.GetFileNameWithoutExtension(prefabPath);
      return PathUtils.MakeSane(Path.Combine(directory, name + Suffix + ExtensionWithDot));
    }

    partial void CreateRootAssetUser(ref QuantumPrefabAsset root);

    public override void OnImportAsset(AssetImportContext ctx) {
      var path = ctx.assetPath;

      var prefabGuid = File.ReadAllText(path);
      var prefabPath = AssetDatabase.GUIDToAssetPath(prefabGuid);
      var prefab = string.IsNullOrEmpty(prefabPath) ? null : AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
      if (prefab == null) {
        ctx.LogImportError($"Unable to load prefab: {prefabGuid}");
        return;
      }
      ctx.DependsOnSourceAsset(prefabPath);

      // sync paths
      var desiredPath = GetPath(prefabPath);
      if (PathUtils.MakeSane(path) != desiredPath) {
        EditorApplication.delayCall += () => {
          AssetDatabase.MoveAsset(path, desiredPath);
        };
      }

      // create root object
      QuantumPrefabAsset root = null;
      CreateRootAssetUser(ref root);
      if (root == null) {
#if QUANTUM_ADDRESSABLES
        var lookup = AssetDBGeneration.CreateAddressablesLookup();
        var addressableEntry = lookup[prefabGuid].SingleOrDefault();
        if (addressableEntry != null) {
          var entry = ScriptableObject.CreateInstance<QuantumPrefabAsset_Addressable>();
          entry.Address = new AssetReferenceGameObject(prefabGuid);
          root = entry;
        } else
#endif
      {
          var prefabBundle = AssetDatabase.GetImplicitAssetBundleName(prefabPath);
          if (!string.IsNullOrEmpty(prefabBundle)) {
            var entry = ScriptableObject.CreateInstance<QuantumPrefabAsset_AssetBundle>();
            entry.AssetBundle = prefabBundle;
            entry.AssetName = Path.GetFileName(prefabPath);
            root = entry;
          } else if (PathUtils.MakeRelativeToFolder(prefabPath, "Resources", out var resourcePath)) {
            var entry = ScriptableObject.CreateInstance<QuantumPrefabAsset_Resource>();
            entry.ResourcePath = PathUtils.GetPathWithoutExtension(resourcePath);
            root = entry;
          } else {
            ctx.LogImportError($"Unable to determine how the source prefab can be loaded. Assign Address, set Asset Bundle, move to Resources or implement " +
              $" QuantumPrefabAssetImporter.CreateRootAssetUser");
            return;
          }
        }
      }

      root.PrefabGuid = prefabGuid;
      root.name = prefab.name;
      ctx.AddObjectToAsset("root", root);

      // discover nested assets
      var components = prefab.GetComponents<MonoBehaviour>()
        .OfType<IQuantumPrefabNestedAssetHost>()
        .ToList();

      if (!components.Any()) {
        ctx.LogImportWarning($"Prefab {prefabPath} does not have any {nameof(IQuantumPrefabNestedAssetHost)} components, this qprefab is pointless");
      } else {
        foreach (var component in components) {
          var nestedAsset = NestedAssetBaseEditor.GetNested((Component)component, component.NestedAssetType);
          if (nestedAsset == null) {
            ctx.LogImportError($"Not found {component.NestedAssetType}");
            continue;
          } 

          var instance = (AssetBase)ScriptableObject.CreateInstance(component.SplitAssetType);
          instance.name = NestedAssetBaseEditor.GetName(instance, root) + Suffix;

          var bakedAsset = (IQuantumPrefabBakedAsset)instance;
          bakedAsset.Import(root, nestedAsset);

          // ideally we would like to hide these assets, but Resources/Bundles/Addressables stop working :(
          // instance.hideFlags = HideFlags.HideInHierarchy;

          ctx.AddObjectToAsset(component.GetType().Name, instance);
        }
      }
    }
  }
}
#endregion