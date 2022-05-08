using System;
using System.Collections.Generic;
using UnityEngine;

namespace Quantum {

  public sealed class UnityResourceLoader : IResourceLoader, IDisposable {

    private sealed class UnityAssetResource : AssetResource {

      public UnityAssetResource(AssetResourceInfo resourceInfo, int groupIndex) {
        ResourceInfo = resourceInfo;
        base.Guid = resourceInfo.Guid;
        base.Path = resourceInfo.Path;
        LoaderIndex = groupIndex;
      }

      public AssetBase AssetWrapper;
      public AssetResourceInfo ResourceInfo;
      public bool IsBeingLoadedAsync;
      public int LoaderIndex;
    }

    private readonly ILoader[] _loaders;

    private Dictionary<AssetGuid, UnityAssetResource> _inProgress = new Dictionary<AssetGuid, UnityAssetResource>();

    private Action<AssetResourceInfo, AssetBase> _loadedAsync;

    public static AssetResource CreateAssetResource(AssetResourceInfo resouceInfo, int loaderIndex) {
      return new UnityAssetResource(resouceInfo, loaderIndex);
    }

    public static AssetBase GetWrapperFromResource(AssetResource resource) {
      return ((UnityAssetResource)resource).AssetWrapper;
    }

    public UnityResourceLoader(ILoader[] loaders) {
      _loaders = loaders;
      _loadedAsync = (resourceInfo, asset) => {
        if (!_inProgress.TryGetValue(resourceInfo.Guid, out var resource)) {
          // sync loading already
          return;
        }

        Assert.Always(resource.IsBeingLoadedAsync);
        resource.IsBeingLoadedAsync = false;
        _inProgress.Remove(resourceInfo.Guid);
        FinishLoading(resource, asset);
      };
    }

    public event ResourceLoaded LoadCompleted;
    public event ResourceLoadFailed LoadFailed;

    public void Dispose() {
      foreach (var loader in _loaders) {
        loader.Dispose();
      }
    }

    void IResourceLoader.DisposeResource(AssetResource resource) {
      var unityResource = (UnityAssetResource)resource;
      var wrapper = unityResource.AssetWrapper;
      unityResource.AssetWrapper = null;
      GetLoaderForResource(unityResource).Unload(unityResource.ResourceInfo, wrapper);
    }

    void IResourceLoader.LoadResource(AssetResource resource) {
      var unityResource = (UnityAssetResource)resource;

      Debug.Assert(unityResource.AssetWrapper == null && !unityResource.IsLoaded);

      if (unityResource.IsBeingLoadedAsync) {
        // stop async loading
        unityResource.IsBeingLoadedAsync = false;
        Assert.Always(_inProgress.Remove(resource.Guid));
      }

      var loader = GetLoaderForResource(unityResource);
      try {
        var asset = loader.LoadSync(unityResource.ResourceInfo);
        FinishLoading(unityResource, asset);
      } catch (Exception ex) {
        FinishLoading(unityResource, ex);
      }
    }

    void IResourceLoader.LoadResourceAsync(AssetResource resource) {
      var unityResource = (UnityAssetResource)resource;

      Debug.Assert(unityResource.AssetWrapper == null && !unityResource.IsLoaded);

      if (unityResource.IsBeingLoadedAsync) {
        // already loading!
        return;
      }

      var loader = GetLoaderForResource(unityResource);

      try {
        loader.LoadAsync(unityResource.ResourceInfo);
      } catch (Exception ex) {
        FinishLoading(unityResource, ex);
        return;
      }

      unityResource.IsBeingLoadedAsync = true;
      _inProgress.Add(resource.Guid, unityResource);
    }

    public void Update() {
      foreach (var loader in _loaders) {
        loader.UpdateLoadAsync(_loadedAsync);
      }
    }

    private void FinishLoading(UnityAssetResource resource, Exception error) {
      LoadFailed?.Invoke(this, resource, error);
    }

    private void FinishLoading(UnityAssetResource resource, AssetBase asset) {
      if (asset == null) {
        FinishLoading(resource, new InvalidOperationException($"Loader {GetLoaderForResource(resource)} returned null for {resource.Guid} ({resource.Path})"));
      } else {
        resource.AssetWrapper = asset;
        LoadCompleted?.Invoke(this, resource, asset.AssetObject);
        resource.AssetWrapper.Loaded();
      }
    }

    private ILoader GetLoaderForResource(UnityAssetResource resource) {
      return _loaders[resource.LoaderIndex];
    }

    public interface ILoader : IDisposable {
      void LoadAsync(AssetResourceInfo resourceInfo);
      AssetBase LoadSync(AssetResourceInfo resourceInfo);
      void Unload(AssetResourceInfo resourceInfo, AssetBase asset);
      void UpdateLoadAsync(Action<AssetResourceInfo, AssetBase> asyncLoadedCallback);
    }

    public abstract class LoaderBase<T, AsyncState> : ILoader where T : AssetResourceInfo {
      private List<(T, AsyncState)> _asyncRequests = new List<(T, AsyncState)>();

      public virtual void Dispose() {

      }

      void ILoader.LoadAsync(AssetResourceInfo resourceInfo) {
        var asyncState = LoadAsync((T)resourceInfo);
        _asyncRequests.Add(((T)resourceInfo, asyncState));
      }

      AssetBase ILoader.LoadSync(AssetResourceInfo resourceInfo) {
        return LoadSync((T)resourceInfo);
      }

      void ILoader.Unload(AssetResourceInfo resourceInfo, AssetBase asset) {
        Unload((T)resourceInfo, asset);
      }

      void ILoader.UpdateLoadAsync(Action<AssetResourceInfo, AssetBase> asyncLoadedCallback) {
        for (int i = 0; i < _asyncRequests.Count; ++i) {
          var (resource, asyncOp) = _asyncRequests[i];

          if (!IsDone(asyncOp)) {
            continue;
          }

          _asyncRequests.RemoveAt(i--);

          AssetBase asset = GetAssetFromAsyncState(resource, asyncOp);
          asyncLoadedCallback(resource, asset);
        }
      }

      protected static AssetBase FindAsset(UnityEngine.Object[] assets, AssetGuid subAssetGuid) {
        for (int i = 0; i < assets.Length; ++i) {
          if (assets[i] is AssetBase asset) {
            if (asset.AssetObject.Identifier.Guid == subAssetGuid)
              return asset;
          }
        }
        return null;
      }

      protected static AssetBase FindAsset(AssetBase[] assets, AssetGuid subAssetGuid) {
        for (int i = 0; i < assets.Length; ++i) {
          var asset = assets[i];
          if (asset.AssetObject.Identifier.Guid == subAssetGuid)
            return asset;
        }
        return null;
      }

      protected abstract AssetBase GetAssetFromAsyncState(T resourceInfo, AsyncState asyncState);

      protected abstract bool IsDone(AsyncState asyncState);

      protected abstract AsyncState LoadAsync(T info);

      protected abstract AssetBase LoadSync(T info);

      protected virtual void Unload(T info, AssetBase asset) {
      }
    }
  }
}