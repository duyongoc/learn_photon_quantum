using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Quantum {

  public abstract class AssetResourceInfoGroup {
    public abstract IReadOnlyList<AssetResourceInfo> Resources { get; }

    public abstract int SortOrder { get; }

    public abstract UnityResourceLoader.ILoader CreateLoader();

    public abstract void Clear();
    public abstract void Add(AssetResourceInfo info);

    public AssetResourceInfo FindResourceInfo(AssetGuid guid) {
      var index = BinarySearch(Resources, guid);
      if (index < 0) {
        return null;
      } else {
        return (AssetResourceInfo)Resources[index];
      }
    }

    private static int BinarySearch(IReadOnlyList<AssetResourceInfo> list, AssetGuid guid) {
      int min = 0;
      int max = list.Count - 1;
      while (min <= max) {
        int mid = min + (max - min >> 1);

        var info = list[mid];
        int comparision = info.Guid.CompareTo(guid);
        if (comparision == 0) {
          return mid;
        }

        if (comparision < 0) {
          min = mid + 1;
        } else {
          max = mid - 1;
        }
      }

      return ~min;
    }
  }

  public abstract class AssetResourceInfoGroup<T> : AssetResourceInfoGroup where T : AssetResourceInfo, new() {

    [SerializeField]
    private List<T> _resources = new List<T>();

    public override IReadOnlyList<AssetResourceInfo> Resources => _resources;

    public IReadOnlyList<T> ResourcesT => _resources;

    public override void Clear() {
      _resources.Clear();
    }

    public override void Add(AssetResourceInfo info) {
      _resources.Add((T)info);
    }
  }
}