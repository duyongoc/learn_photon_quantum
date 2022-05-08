using System;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Quantum {

  public partial class AssetResourceContainer {
    public AssetResourceInfoGroup_Resources ResourcesGroup;

    [Serializable]
    public class AssetResourceInfo_Resources : AssetResourceInfo {
      public string ResourcePath;
    }

    [Serializable]
    public class AssetResourceInfoGroup_Resources : AssetResourceInfoGroup<AssetResourceInfo_Resources> {
      public override int SortOrder => 2000;

      public override UnityResourceLoader.ILoader CreateLoader() {
        return new Loader_Resources();
      }
    }

    class Loader_Resources : UnityResourceLoader.LoaderBase<AssetResourceInfo_Resources, ResourceRequest> {

      protected override AssetBase GetAssetFromAsyncState(AssetResourceInfo_Resources info, ResourceRequest asyncState) {
        if (info.IsNestedAsset) {
          return FindAsset(UnityEngine.Resources.LoadAll<AssetBase>(info.ResourcePath), info.Guid);
        } else {
          return (AssetBase)asyncState.asset;
        }
      }

      protected override bool IsDone(ResourceRequest asyncState) {
        return asyncState.isDone;
      }

      protected override ResourceRequest LoadAsync(AssetResourceInfo_Resources info) {
        return UnityEngine.Resources.LoadAsync<AssetBase>(info.ResourcePath);
      }

      protected override AssetBase LoadSync(AssetResourceInfo_Resources info) {
        return info.IsNestedAsset
          ? FindAsset(UnityEngine.Resources.LoadAll<AssetBase>(info.ResourcePath), info.Guid)
          : UnityEngine.Resources.Load<AssetBase>(info.ResourcePath);
      }

      protected override void Unload(AssetResourceInfo_Resources info, AssetBase asset) {
        if ( asset is BinaryDataAsset ) {
          UnityEngine.Resources.UnloadAsset(asset);
        }
      }
    }
  }
}