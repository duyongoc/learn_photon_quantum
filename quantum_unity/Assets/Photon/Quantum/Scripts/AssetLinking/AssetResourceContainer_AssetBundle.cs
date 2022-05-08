using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Quantum {
  public partial class AssetResourceContainer {
    public AssetResourceInfoGroup_AssetBundle AssetBundlesGroup;

    [Serializable]
    public class AssetResourceInfo_AssetBundle : AssetResourceInfo {
      public string AssetBundle;
      public string AssetName;
    }

    [Serializable]
    public class AssetResourceInfoGroup_AssetBundle : AssetResourceInfoGroup<AssetResourceInfo_AssetBundle> {

      public override int SortOrder => 3000;

      public override UnityResourceLoader.ILoader CreateLoader() {
        return new Loader_AssetBundles();
      }
    }

    class Loader_AssetBundles : UnityResourceLoader.LoaderBase<AssetResourceInfo_AssetBundle, AssetBundleRequest> {

      protected override AssetBase GetAssetFromAsyncState(AssetResourceInfo_AssetBundle resourceInfo, AssetBundleRequest asyncState) {
        if (resourceInfo.IsNestedAsset) {
          return FindAsset(asyncState.allAssets, resourceInfo.Guid);
        } else {
          return (AssetBase)asyncState.asset;
        }
      }

      protected override bool IsDone(AssetBundleRequest asyncState) {
        return asyncState.isDone;
      }

      protected override AssetBundleRequest LoadAsync(AssetResourceInfo_AssetBundle info) {
        var bundle = GetAssetBundleOrThrow(info);
        if (info.IsNestedAsset) {
          return bundle.LoadAssetWithSubAssetsAsync<AssetBase>(info.AssetName);
        } else {
          return bundle.LoadAssetAsync<AssetBase>(info.AssetName);
        }
      }

      protected override AssetBase LoadSync(AssetResourceInfo_AssetBundle info) {
        var bundle = GetAssetBundleOrThrow(info);
        if (info.IsNestedAsset) {
          return FindAsset(bundle.LoadAssetWithSubAssets<AssetBase>(info.AssetName), info.Guid);
        } else {
          return bundle.LoadAsset<AssetBase>(info.AssetName);
        }
      }

      protected override void Unload(AssetResourceInfo_AssetBundle info, AssetBase asset) {
        // outside of the scope
      }

      private AssetBundle GetAssetBundleOrThrow(AssetResourceInfo_AssetBundle resource) {
        var assetBundle = AssetBundle.GetAllLoadedAssetBundles().FirstOrDefault(ab => ab.name == resource.AssetBundle);
        if (assetBundle == null) {
          assetBundle = AssetBundle.LoadFromFile(System.IO.Path.Combine(Application.streamingAssetsPath, resource.AssetBundle));
        }

        if (assetBundle == null) {
          throw new InvalidOperationException("Unable to load asset bundle");
        }

        return assetBundle;
      }
    }
  }
}