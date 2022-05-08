using Photon.Deterministic;
using Quantum;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

public unsafe class EntityViewUpdater : MonoBehaviour {
  [Tooltip("Optionally provide a transform that all entity views will be parented under.")]
  public Transform ViewParentTransform = null;
  [Tooltip("Disable you don't intend to use MapData component.")]
  public bool AutoFindMapData = true;

  // current map
  [NonSerialized]
  MapData _mapData = null;

  // current set of entities that should be removed
  HashSet<EntityRef> _removeEntities = new HashSet<EntityRef>();

  // current set of active entities
  HashSet<EntityRef> _activeEntities = new HashSet<EntityRef>();

  // current set of active prefabs
  Dictionary<EntityRef, EntityView> _activeViews = new Dictionary<EntityRef, EntityView>(256);

  // teleport state variable
  Boolean _teleport;

  QuantumGame _observedGame = null;

  // Provide access for derived EntityViewUpdater classes
  protected MapData MapData => _mapData;
  protected HashSet<EntityRef> ActiveEntities => _activeEntities;
  protected HashSet<EntityRef> RemoveEntities => _removeEntities;
  protected Dictionary<EntityRef, EntityView> ActiveViews => _activeViews;
  protected Boolean Teleport => _teleport;
  
  public QuantumGame ObservedGame => _observedGame;

  [Obsolete("Use GetView instead")]
  public EntityView GetPrefab(EntityRef entityRef) => GetView(entityRef);

  public EntityView GetView(EntityRef entityRef) {
    _activeViews.TryGetValue(entityRef, out EntityView root);
    return root;
  }

  [Obsolete("Use TeleportAllEntities() instead")]
  public void SetTeleportOnce() {
    TeleportAllEntities();
  }

  public void TeleportAllEntities() {
    _teleport = true;
  }

  public void SetCurrentGame(QuantumGame game) {
    _observedGame = game;
  }

  public void Awake() {
    QuantumCallback.Subscribe(this, (CallbackGameStarted c) => OnGameStarted(c.Game));
    QuantumCallback.Subscribe(this, (CallbackUnitySceneLoadDone c) => OnGameStarted(c.Game));

    QuantumCallback.Subscribe(this, (CallbackUpdateView c) => OnObservedGameUpdated(c.Game), game => game == _observedGame);
    QuantumCallback.Subscribe(this, (CallbackGameDestroyed c) => OnObservedGameDestroyed(c.Game, true), game => game == _observedGame);
    QuantumCallback.Subscribe(this, (CallbackUnitySceneLoadBegin c) => OnObservedGameDestroyed(c.Game, false), game => game == _observedGame);
  }

  private void OnGameStarted(QuantumGame game) {
    if ( _observedGame == null ) {
      // attach to the first game found
      SetCurrentGame(game);
    }
  }

  private void OnObservedGameDestroyed(QuantumGame game, bool destroyed) {

    Debug.Assert(_observedGame == game);

    if (destroyed) {
      // Game and session are shutdown instantly -> delete the game objects right away and don't wait for a cleanup between the ticks (OnUpdateView).
      // If objects are not destroyed here scripts on them that access QuantumRunner.Default will throw.
      foreach (var view in _activeViews) {
        if (!view.Value)
          continue;

        DestroyEntityView(game, view.Value);
      }
      _activeViews.Clear();
    }

    _observedGame = null;
  }

  private void OnObservedGameUpdated(QuantumGame game) {

    Debug.Assert(_observedGame == game);

    if (game.Frames.Predicted != null) {

      bool checkPossiblyOrphanedMapEntityViews = false;

      if (_mapData == null && AutoFindMapData) {
        _mapData = FindObjectOfType<MapData>();
        if (_mapData) {
          checkPossiblyOrphanedMapEntityViews = true;
        }
      }

      _activeEntities.Clear();

      // Always use clock aliasing interpolation except during forced teleports.
      var useClockAliasingInterpolation = !_teleport;

      // Use error based interpolation only during multiplayer mode and when not forced teleporting.
      // For local games we want don't want error based interpolation as well as on forced teleports.
      var useErrorCorrection = game.Session.GameMode == DeterministicGameMode.Multiplayer && _teleport == false;

      // Go through all verified entities and create new view instances for new entities.
      // Checks information (CreateBehaviour) on each EntityView if it should be created/destroyed during Verified or Prediceted frames.
      SyncViews(game, game.Frames.Verified, EntityViewBindBehaviour.Verified);

      // Go through all entities in the current predicted frame (predicted == verified only during lockstep).
      SyncViews(game, game.Frames.Predicted, EntityViewBindBehaviour.NonVerified);

      // Sync the active view instances with the active entity list. Find outdated instances.
      _removeEntities.Clear();
      foreach (var key in _activeViews) {
        if (_activeEntities.Contains(key.Key) == false) {
          _removeEntities.Add(key.Key);
        }
      }

      // Destroy outdated view instances.
      foreach (var key in _removeEntities) {
        DestroyEntityView(game, key);
      }

      if (checkPossiblyOrphanedMapEntityViews) {
        Debug.Assert(_mapData);
        foreach (var view in _mapData.MapEntityReferences) {
          if (!view || !view.isActiveAndEnabled)
            continue;
          if (view.EntityRef == EntityRef.None) {
            DisableMapEntityInstance(view);
          }
        }
      }

      // Run over all view instances and update components using only entities from current frame.
      foreach (var kvp in _activeViews) {
        // grab instance
        var instance = kvp.Value;

        // make sure we do not try to update for an instance which doesn't exist.
        if (!instance) {
          continue;
        }

        // TODO we could just call update (and internally this would be resolved to either 2D or 3D transform 

        if (game.Frames.Predicted.Has<Transform2D>(kvp.Key)) {
          // update 2d transform
          instance.UpdateFromTransform2D(game, useClockAliasingInterpolation, useErrorCorrection);
        } else {
          // update 3d transform
          if (game.Frames.Predicted.Has<Transform3D>(kvp.Key)) {
            instance.UpdateFromTransform3D(game, useClockAliasingInterpolation, useErrorCorrection);
          }
        }
      }
    }

    // reset teleport to false always
    _teleport = false;
  }

  private void SyncViews(QuantumGame game, Frame frame, EntityViewBindBehaviour createBehaviour) {

    // update prefabs
    foreach(var (entity, view) in frame.GetComponentIterator<Quantum.View>()) {
      CreateViewIfNeeded(game, game.Frames.Predicted, entity, view, createBehaviour);
    }

    // update map entities
    if (_mapData) {
      var currentMap = _mapData.Asset.Settings.Guid;
      if (currentMap != frame.Global->Map.Id) {
        // can't update map entities because of map mismatch
      } else {
        foreach (var (entity, mapEntityLink) in frame.GetComponentIterator<Quantum.MapEntityLink>()) {
          BindMapEntityIfNeeded(game, game.Frames.Predicted, entity, mapEntityLink, createBehaviour);
        }
      }
    }
  }

  void CreateViewIfNeeded(QuantumGame game, Frame f, EntityRef handle, Quantum.View view, EntityViewBindBehaviour createBehaviour) {
    var instance = default(EntityView);
    var entityView = f.FindAsset<Quantum.EntityView>(view.Current.Id);
    
    if (_activeViews.TryGetValue(handle, out instance)) {
      if (instance.BindBehaviour == createBehaviour) {
        if (entityView == null) {
          // Quantum.View has been revoked for this entity
          DestroyEntityView(game, handle);
        } else {
          var currentGuid = entityView.Guid;
          if (instance.AssetGuid == currentGuid) {
            _activeEntities.Add(handle);
          } else {
            // The Guid changed, recreate the view instance for this entity.
            DestroyEntityView(game, handle);
            if (CreateView(game, f, handle, entityView, createBehaviour) != null) {
              _activeEntities.Add(handle);
            }
          }
        }
      }
    } else if (entityView != null) {
      // Create a new view instance for this entity.
      if (CreateView(game, f, handle, entityView, createBehaviour) != null) {
        _activeEntities.Add(handle);
      }
    }
  }

  private void BindMapEntityIfNeeded(QuantumGame game, Frame f, EntityRef handle, MapEntityLink mapEntity, EntityViewBindBehaviour createBehaviour) {
    var instance = default(EntityView);
    if (_activeViews.TryGetValue(handle, out instance)) {
      if (instance.AssetGuid.IsValid) {
        // this can happen if a scene prototype has the View property set to an asset
      } else {
        if (instance.BindBehaviour == createBehaviour) {
          _activeEntities.Add(handle);
        }
      }
    } else {
      if (BindMapEntity(game, f, handle, mapEntity, createBehaviour) != null) {
        _activeEntities.Add(handle);
      }
    }
  }

  EntityView CreateView(QuantumGame game, Frame f, EntityRef handle, Quantum.EntityView view, EntityViewBindBehaviour createBehaviour) {
    if (view == null) {
      return null;
    }

    var asset = UnityDB.FindAsset<EntityViewAsset>(view);
    if (asset == null) {
      return null;
    }

    if (asset.View == null) {
      LoadMissingPrefab(asset);
      if (asset.View == null) {
        return null;
      }
    }

    if (asset.View.BindBehaviour != createBehaviour)
      return null;

    EntityView instance;
    if (TryGetTransform(f, handle, out Vector3 position, out Quaternion rotation)) {
      instance = CreateEntityViewInstance(asset, position, rotation);
    } else {
      instance = CreateEntityViewInstance(asset);
    }
    
    if (ViewParentTransform != null) {
      instance.transform.SetParent(ViewParentTransform);
    }
    
    instance.AssetGuid = view.Guid;
    OnEntityViewInstantiated(game, f, instance, handle);

    // return instance
    return instance;
  }

  EntityView BindMapEntity(QuantumGame game, Frame f, EntityRef handle, MapEntityLink mapEntity, EntityViewBindBehaviour createBehaviour) {

    Debug.Assert(_mapData);

    if (_mapData.MapEntityReferences.Count <= mapEntity.Index) {
      Debug.LogErrorFormat(this,
        "MapData on \"{0}\" does not have a map entity slot with an index {1} (entity: {2}). EntityView will not be assigned. " +
        "Make sure all baked data is up to date.", _mapData.gameObject.scene.path, mapEntity.Index, handle);
      return null;
    }

    var instance = _mapData.MapEntityReferences[mapEntity.Index];

    if (instance?.BindBehaviour != createBehaviour) {
      return null;
    }

    if (instance.EntityRef.IsValid && instance.EntityRef != handle) {
      // possible when a map is restarted
      DestroyEntityView(game, instance.EntityRef);
    }

    if (TryGetTransform(f, handle, out Vector3 position, out Quaternion rotation)) {
      ActivateMapEntityInstance(instance, position, rotation);
    } else {
      ActivateMapEntityInstance(instance);
    }

    instance.AssetGuid = new AssetGuid();
    OnEntityViewInstantiated(game, f, instance, handle);
    return instance;
  }

  private void OnEntityViewInstantiated(QuantumGame game, Frame f, EntityView instance, EntityRef handle) {
    if (instance.GameObjectNameIsEntityRef) {
      instance.gameObject.name = handle.ToString();
    }
    
    instance.EntityRef = handle;
    instance.OnInstantiated();

    if (f.Has<Transform2D>(handle)) {
      instance.UpdateFromTransform2D(game, false, false);
    } else if (f.Has<Transform3D>(handle)) {
      instance.UpdateFromTransform3D(game, false, false);
    }

    // add to lookup
    _activeViews.Add(handle, instance);

    instance.OnEntityInstantiated.Invoke(game);
  }

  void DestroyEntityView(QuantumGame game, EntityRef entityRef) {
    EntityView view;

    if (_activeViews.TryGetValue(entityRef, out view)) {
      DestroyEntityView(game, view);
    }

    _activeViews.Remove(entityRef);
  }

  protected virtual void DestroyEntityView(QuantumGame game, EntityView view) {
    Debug.Assert(view != null);
    view.OnEntityDestroyed.Invoke(game);

    if (!view.ManualDisposal) {
      if (view.AssetGuid.IsValid) {
        DestroyEntityViewInstance(view);
      } else {
        DisableMapEntityInstance(view);
      }
    }

  }

  void OnDestroy() {
    foreach (var kvp in _activeViews) {
      if (kvp.Value && kvp.Value.gameObject) {
        Destroy(kvp.Value.gameObject);
      }
    }
  }

  protected virtual EntityView CreateEntityViewInstance(EntityViewAsset asset, Vector3? position = null, Quaternion? rotation = null) {
    Debug.Assert(asset.View != null);

    var view = position.HasValue && rotation.HasValue ? 
        GameObject.Instantiate(asset.View, position.Value, rotation.Value) :
        GameObject.Instantiate(asset.View);

    return view;
  }

  protected virtual void DestroyEntityViewInstance(EntityView instance) {
    GameObject.Destroy(instance.gameObject);
  }

  protected virtual void ActivateMapEntityInstance(EntityView instance, Vector3? position = null, Quaternion? rotation = null) {

    if (position.HasValue)
      instance.transform.position = position.Value;
    if (rotation.HasValue)
      instance.transform.rotation = rotation.Value;
    if (!instance.gameObject.activeSelf) {
      instance.gameObject.SetActive(true);
    }
  }

  protected virtual void DisableMapEntityInstance(EntityView instance) {
    instance.gameObject.SetActive(false);
  }

  protected virtual void LoadMissingPrefab(EntityViewAsset viewAsset) {
    if (viewAsset.ViewStatus == EntityViewAssetStatus.NotLoaded) {
      viewAsset.LoadViewPrefab(async: false);
    }
  }

  private static bool TryGetTransform(Frame f, EntityRef handle, out Vector3 position, out Quaternion rotation) {
    if (f.Has<Transform2D>(handle)) {
      var transform2D = f.Unsafe.GetPointer<Transform2D>(handle);
      position = transform2D->Position.ToUnityVector3();
      rotation = transform2D->Rotation.ToUnityQuaternion();
      return true;
    } else if (f.Has<Transform3D>(handle)) {
      var transform3D = f.Unsafe.GetPointer<Transform3D>(handle);
      position = transform3D->Position.ToUnityVector3();
      rotation = transform3D->Rotation.ToUnityQuaternion();
      return true;
    } else {
      position = default;
      rotation = default;
      return false;
    }
  }
}
