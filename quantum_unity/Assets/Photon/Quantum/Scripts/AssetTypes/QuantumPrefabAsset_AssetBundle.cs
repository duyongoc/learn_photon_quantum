using System;
using System.Linq;
using UnityEngine;

public class QuantumPrefabAsset_AssetBundle : QuantumPrefabAsset {
  public string AssetBundle;
  public string AssetName;

  private object _state;
  private AssetBundle _bundle;

  protected override void Load(in LoadContext context) {
    _bundle = UnityEngine.AssetBundle.GetAllLoadedAssetBundles().FirstOrDefault(ab => ab.name == AssetBundle);
    if (_bundle == null) {
      _bundle = UnityEngine.AssetBundle.LoadFromFile(System.IO.Path.Combine(Application.streamingAssetsPath, AssetBundle));
    }

    if (_bundle == null) {
      context.Error(new InvalidOperationException("Unable to load asset bundle"));
    } else {
      if (context.PreferAsync) {
        var asyncOp = _bundle.LoadAssetAsync<GameObject>(AssetName);
        _state = asyncOp;
        var cc = context;
        asyncOp.completed += (op) => {
          var asset = (GameObject)((AssetBundleRequest)op).asset;
          if (_state != op) {
            Debug.Assert(_state == null);
            if (asset != null) {
              UnloadPrefab(asset);
            }
          } else {
            _state = asset;
            cc.Loaded(asset);
          }
        };


      } else {
        var gameObject = _bundle.LoadAsset<GameObject>(AssetName);
        context.Loaded(gameObject);
      }
    }
  }

  protected override void Unload() {
    if (_state == null) {
      return;
    }
    Debug.Assert(_bundle != null);
    if (_state is AssetBundleRequest asyncOp) {
      // the handler checks _state, so we should be fine here
    } else if (_state is GameObject prefab) {
      UnloadPrefab(prefab);
    }
    _bundle = null;
    _state = null;
  }

  private void UnloadPrefab(GameObject prefab) {
    Destroy(prefab);
  }
}
