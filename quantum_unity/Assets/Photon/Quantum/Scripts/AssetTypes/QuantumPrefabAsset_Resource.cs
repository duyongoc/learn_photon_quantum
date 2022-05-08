using System;
using UnityEngine;

public class QuantumPrefabAsset_Resource : QuantumPrefabAsset {
  public string ResourcePath;
  private object _state;

  protected override void Load(in LoadContext context) {
    Debug.Assert(_state == null);

    if (context.PreferAsync) {
      var asyncOp = UnityEngine.Resources.LoadAsync<GameObject>(ResourcePath);
      if (asyncOp.isDone) {
        var asset = (GameObject)asyncOp.asset;
        _state = asset;
        context.Loaded(asset);
      } else {
        _state = asyncOp;
        var cc = context;
        asyncOp.completed += (op) => {
          var asset = (GameObject)((ResourceRequest)op).asset;
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
      }
    } else {
      var asset = UnityEngine.Resources.Load<GameObject>(ResourcePath);
      _state = asset;
      context.Loaded(asset);
    }
  }

  protected override void Unload() {
    if (_state == null) {
      return;
    }
    if (_state is ResourceRequest asyncOp) {
      // the handler checks _state, so we should be fine here
    } else if (_state is GameObject prefab) {
      UnloadPrefab(prefab);
    }
    _state = null;
  }

  private void UnloadPrefab(GameObject asset) {
    UnityEngine.Resources.UnloadAsset(asset);
  }
}
