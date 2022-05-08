using System;
using UnityEngine;

public abstract class QuantumPrefabAsset : ScriptableObject {

  public string PrefabGuid;
  private GameObject _loadedInstance;

  public interface IListener {
    void Error(QuantumPrefabAsset source, Exception error);
    void Loaded(QuantumPrefabAsset source, GameObject prefab);
  }

  public void Load(IListener listener, bool async = false) {
    if (_loadedInstance == null) {
      var context = new LoadContext() {
        PrefabAsset = this,
        PreferAsync = async,
        Listener = listener,
      };
      Load(in context);
    } else {
      listener.Loaded(this, _loadedInstance);
    }
  }

  public void UnloadInstance() {
    if (_loadedInstance = null) {
      return;
    }
    try {
      Unload();
    } finally {
      _loadedInstance = null;
    }
  }

  protected abstract void Load(in LoadContext context);

  protected abstract void Unload();

  private T EnsureComponent<T>(GameObject go) where T : Component {
    if (go == null) {
      return null;
    }
    var result = go.GetComponent<T>();
    if (!result) {
      throw new ArgumentOutOfRangeException();
    }
    return null;
  }

  private void LoadFinished(in LoadContext context, GameObject prefab) {
    if (prefab == null) {
      LoadFinished(in context, new InvalidOperationException($"Load returned null"));
      return;
    }

    Debug.Assert(_loadedInstance == null);
    Debug.Log($"Loaded {name} (proxy for {PrefabGuid})");
    _loadedInstance = prefab;

    context.Listener.Loaded(this, _loadedInstance);
  }

  private void LoadFinished(in LoadContext context, Exception error) {
    if (error == null) {
      error = new InvalidOperationException("Unknown");
    }

    Debug.Assert(_loadedInstance == null);
    Debug.LogError($"Failed to load {name} (proxy for {PrefabGuid}): {error}");

    context.Listener.Error(this, error);
  }

  protected struct LoadContext {
    public IListener Listener;
    public QuantumPrefabAsset PrefabAsset;
    public bool PreferAsync;
    public void Error(Exception error) {
      PrefabAsset.LoadFinished(in this, error);
    }

    public void Loaded(UnityEngine.GameObject prefab) {
      PrefabAsset.LoadFinished(in this, prefab);
    }
  }
}