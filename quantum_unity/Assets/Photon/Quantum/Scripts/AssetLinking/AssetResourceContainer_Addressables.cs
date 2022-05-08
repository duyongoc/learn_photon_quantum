#if QUANTUM_ADDRESSABLES

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;

using AsyncOpHandle = UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationHandle<AssetBase>;

namespace Quantum {
  public partial class AssetResourceContainer {

    public AssetResourceInfoGroup_Addressables AddressablesGroup;

    [Serializable]
    public class AssetResourceInfo_Addressables : AssetResourceInfo {
      public string Address;
    }

    [Serializable]
    public class AssetResourceInfoGroup_Addressables : AssetResourceInfoGroup<AssetResourceInfo_Addressables> {
      public override int SortOrder => 1000;

      public override UnityResourceLoader.ILoader CreateLoader() {
        return new Loader_Addressables();
      }
    }

    class Loader_Addressables : UnityResourceLoader.LoaderBase<AssetResourceInfo_Addressables, AsyncOpHandle> {

      private Dictionary<AssetGuid, AsyncOpHandle> _handles = new Dictionary<AssetGuid, AsyncOpHandle>();

      protected override AssetBase GetAssetFromAsyncState(AssetResourceInfo_Addressables resourceInfo, AsyncOpHandle asyncState) {
        Debug.Assert(asyncState.IsDone);

        if (!asyncState.IsValid()) {
          throw new InvalidOperationException("Failed", asyncState.OperationException);
        }

        return asyncState.Result;
      }

      protected override bool IsDone(AsyncOpHandle asyncState) {
        return asyncState.IsDone;
      }

      protected override AsyncOpHandle LoadAsync(AssetResourceInfo_Addressables info) {
        var op = Addressables.LoadAssetAsync<AssetBase>(info.Address);
        _handles.Add(info.Guid, op);
        return op;
      }

      protected override AssetBase LoadSync(AssetResourceInfo_Addressables info) {
        if (!Addressables.InitializeAsync().IsDone) {
          throw new InvalidOperationException("Addressables are not initialized yet. This is an async process" +
            " and needs to finish before attempting to load a resource in a sync way.");
        }

        bool usingExistingHandle = false;

        if (_handles.TryGetValue(info.Guid, out AsyncOpHandle handle)) {
          Debug.Assert(handle.IsValid());
          usingExistingHandle = true;
        } else {
          handle = Addressables.LoadAssetAsync<AssetBase>(info.Address);
        }

#if QUANTUM_ADDRESSABLES_USE_WAIT_FOR_COMPLETION
        if (!handle.IsDone) {
          handle.WaitForCompletion();
        }
#endif

        if (!handle.IsDone) {
          if (usingExistingHandle) {
            _handles.Remove(info.Guid);
          }
          Addressables.Release(handle);
          throw new InvalidOperationException($"Addressable {info.Address} failed to load in a sync mode. " +
            $"Out of the box, Addressables don't support it. Preload your assets or go to https://github.com/Unity-Technologies/Addressables-Sample " +
            $"for an example how to implement synchronous loading.");
        }

        if (!usingExistingHandle) {
          _handles.Add(info.Guid, handle);
        }

        return GetAssetFromAsyncState(info, handle);
      }

      protected override void Unload(AssetResourceInfo_Addressables info, AssetBase asset) {
        base.Unload(info, asset);
        if (_handles.TryGetValue(info.Guid, out var handle)) {
          Addressables.Release(handle);
          _handles.Remove(info.Guid);
        }
      }
    }
  }
}

#endif