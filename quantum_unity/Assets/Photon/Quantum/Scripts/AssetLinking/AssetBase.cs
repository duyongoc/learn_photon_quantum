using UnityEngine;

public abstract partial class AssetBase : ScriptableObject {

  public const string DefaultAssetObjectPropertyPath = "Settings";
  public const char NestedPathSeparator = '|';

  public abstract Quantum.AssetObject AssetObject {
    get;
  }

  public bool IsTransient => this is IQuantumPrefabBakedAsset;

  public virtual string AssetObjectPropertyPath {
    get { return DefaultAssetObjectPropertyPath; }
  }

  public virtual void Loaded() {
    PrepareAsset();
  }

  public virtual void PrepareAsset() {
  }

  public virtual void Disposed() {
  }

  public virtual void Reset() {
  }

  public virtual void Awake() {
  }

  private string GetNameForNesting() {
    return (this is IQuantumPrefabNestedAsset) ? AssetObject.GetType().Name : name;
  }

  public static bool GetMainAssetPath(string path, out string mainAssetPath) {
    var sep = path.LastIndexOf(AssetBase.NestedPathSeparator);
    if (sep >= 0) {
      mainAssetPath = path.Substring(0, sep);
      return true;
    } else {
      mainAssetPath = path;
      return false;
    }
  }

#if UNITY_EDITOR
  public virtual void OnInspectorGUIBefore(UnityEditor.SerializedObject serializedObject) {
        
  }
  
  public virtual void OnInspectorGUIAfter(UnityEditor.SerializedObject serializedObject) {
        
  }
  
  public string GenerateDefaultPath(string path) {
    path = Quantum.PathUtils.GetPathWithoutExtension(path);
    if (!path.StartsWith("Packages/") && Quantum.PathUtils.MakeRelativeToFolder(path, "Assets", out var relativePath)) {
      path = relativePath;
    }
    path = Quantum.PathUtils.MakeSane(path);

    if (UnityEditor.AssetDatabase.IsMainAsset(this)) {
      return path;
    } else {
      return path + AssetBase.NestedPathSeparator + GetNameForNesting();
    }
  }

  public string GetAssetPropertyPath(string subPropertyPath = null) {
    if (string.IsNullOrEmpty(subPropertyPath)) {
      return AssetObjectPropertyPath;
    } else {
      return $"{AssetObjectPropertyPath}.{subPropertyPath}";
    }
  }
#endif
}