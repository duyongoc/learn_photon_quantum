using System;
using UnityEngine;
using Quantum;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using Photon.Deterministic;

#if UNITY_EDITOR
using UnityEditor;
#endif

public static partial class UnityDB {
  private static Context _context;

  static UnityDB() {
#if UNITY_EDITOR
    // The resource manager needs to dispose asset that would otherwise leak between domain reloads
    AssemblyReloadEvents.beforeAssemblyReload += BeforeAssemblyReload;
    // Asset bundles loaded need to be reset when leaving the play mode
    EditorApplication.playModeStateChanged += PlayModeStateChanged;
#endif
  }

  // implement this if you load AssetResourceContainer from a different sourca than Resources, e.g. AssetBundles or Addressables
  static partial void LoadAssetResourceContainerUser(ref AssetResourceContainer container);

  public static IEnumerable<AssetResource> AssetResources => GetOrCreateContext().AssetResources;

  [Obsolete("Use DefaultResourceManager instead")]
  public static ResourceManagerDynamic ResourceManager => DefaultResourceManager;

  public static ResourceManagerDynamic DefaultResourceManager => GetOrCreateContext().ResourceManager;


#if QUANTUM_ADDRESSABLES
  public static List<QTuple<AssetRef, string>> CollectAddressableAssets() {
    var result = new List<QTuple<AssetRef, string>>();
    CollectAddressableAssets(result);
    return result;
  }

  public static void CollectAddressableAssets(List<QTuple<AssetRef, string>> entries) {
    foreach (var info in GetOrCreateContext().ResourceContainer.AddressablesGroup.ResourcesT) {
      entries.Add(QTuple.Create(info.AssetRef, info.Address));
    }
  }
#endif

  public static void Dispose() {
    if (_context == null)
      return;

    try {
      Debug.Log("Disposing UnityDB");
      _context.Dispose();
    } finally {
      _context = null;
    }
  }

  public static T FindAsset<T>(AssetObject asset) where T : AssetBase {
    return asset == null ? default : FindAsset<T>(asset.Guid);
  }

  public static T FindAsset<T>(String path) where T : AssetBase {
    return FindAsset(path) as T;
  }

  public static T FindAsset<T>(AssetGuid guid) where T : AssetBase {
    return FindAsset(guid) as T;
  }

  public static AssetBase FindAsset(string path) {
    var context = GetOrCreateContext();

    if (context.ResourceManager.TryGetAssetResource(path, out var resource)) {
      context.ResourceManager.LoadResource(resource, mainThread: true);
      return UnityResourceLoader.GetWrapperFromResource(resource);
    }

    // no such asset
    return null;
  }

  public static AssetBase FindAsset(AssetGuid guid) {
    var context = GetOrCreateContext();

    if (context.ResourceManager.TryGetAssetResource(guid, out var resource)) {
      context.ResourceManager.LoadResource(resource, mainThread: true);
      return UnityResourceLoader.GetWrapperFromResource(resource);
    }

    // no such asset
    return null;
  }

  public static AssetGuid GetAssetGuid(String path) => DefaultResourceManager.GetAssetGuid(path);

  public static void Update() {
    _context?.ResourceLoader.Update();
  }

  private static Context GetOrCreateContext() {
    if (_context == null) {
      _context = new Context();
    }
    return _context;
  }

  private static AssetResourceContainer LoadContainer() {
    AssetResourceContainer container = null;
    LoadAssetResourceContainerUser(ref container);
    if (container != null) {
      return container;
    }

    container = UnityEngine.Resources.Load<AssetResourceContainer>(QuantumEditorSettings.Instance.AssetResourcesPathInResources);
    if (container != null) {
      return container;
    }

    throw new System.InvalidOperationException("Unable to find AssetResourceContainer.");
  }

  private sealed class Context : IDisposable {
    public QuantumUnityNativeAllocator Allocator = new QuantumUnityNativeAllocator();
    public List<AssetResource> AssetResources;
    public AssetResourceContainer ResourceContainer = LoadContainer();
    public UnityResourceLoader ResourceLoader;
    public ResourceManagerDynamic ResourceManager;

    public Context() {

      QuantumRunner.Init();
      ResourceLoader = ResourceContainer.CreateLoader();
      AssetResources = ResourceContainer.CreateResourceWrappers();
      ResourceManager = CreateResourceManager();
    }

    public ResourceManagerDynamic CreateResourceManager() {
      var manager = new ResourceManagerDynamic();
      manager.Init(AssetResources, ResourceLoader, Allocator);
      return manager;
    }

    public void Dispose() {
      try {
        try {
          ResourceManager?.Dispose();
        } finally {
          ResourceLoader?.Dispose();
        }
      } finally {
        ResourceManager = null;
        ResourceLoader = null;
        ResourceContainer = null;
        AssetResources = null;
        Allocator.Dispose();
      }
    }
  }

#if UNITY_EDITOR
  public static AssetBase FindAssetForInspector(AssetGuid assetGuid) {
    var container = LoadContainer();

    // need to go through the resouce container to make sure they'll be available
    var path = container.FindResourceInfo(assetGuid)?.Path;
    if (string.IsNullOrEmpty(path)) {
      // not mapped in the resource container
      return null;
    }

    // get rid of the nested path
    AssetBase.GetMainAssetPath(path, out path);

    // prepend with Assets and will be good to go with the loading
    if (!path.StartsWith("Packages/")) {
      path = "Assets/" + path;
    }

    // assets may be kept either in scriptable object or prefab
    return new[] { ".asset", ".prefab" }
      .SelectMany(x => AssetDatabase.LoadAllAssetsAtPath(path + x))
      .OfType<AssetBase>()
      .Where(x => x != null) // missing scripts fix
      .SingleOrDefault(x => x.AssetObject?.Guid == assetGuid);
  }

  private static void BeforeAssemblyReload() {
    RecreateResourceManager();
  }

  private static void PlayModeStateChanged(PlayModeStateChange state) {
    if (state == PlayModeStateChange.EnteredEditMode) {
      RecreateResourceManager();
    }
  }

  private static void RecreateResourceManager() {
    if (_context == null)
      return;

    _context.ResourceManager.Dispose();
    _context.ResourceManager = _context.CreateResourceManager();
  }
#endif
}