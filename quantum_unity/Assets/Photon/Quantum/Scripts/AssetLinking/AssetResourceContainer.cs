using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Quantum {

  public partial class AssetResourceContainer : ScriptableObject {

    private List<AssetResourceInfoGroup> _groups;

    public int AssetCount => Groups.Sum(x => x.Resources.Count);

    public UnityResourceLoader CreateLoader() {
      return new UnityResourceLoader(Groups.Select(x => x.CreateLoader()).ToArray());
    }

    public List<AssetResource> CreateResourceWrappers() {
      var result = new List<AssetResource>(AssetCount);
      for (int i = 0; i < Groups.Count; ++i) {
        foreach (AssetResourceInfo info in Groups[i].Resources) {
          result.Add(UnityResourceLoader.CreateAssetResource(info, i));
        }
      }
      return result;
    }

    public IReadOnlyList<AssetResourceInfoGroup> Groups {
      get {
        if (_groups == null) {
          InitGroups();
          Debug.Assert(_groups != null);
        }
        return _groups;
      }
    }

    public AssetResourceInfo FindResourceInfo(AssetGuid guid) {
      foreach (var group in Groups) {
        var info = group.FindResourceInfo(guid);
        if (info != null) {
          return info;
        }
      }
      return null;
    }

    private void InitGroups() {
      _groups = GetType()
        .GetFields()
        .Where(x => x.FieldType.IsSubclassOf(typeof(AssetResourceInfoGroup)))
        .Select(x => {
          var result = (AssetResourceInfoGroup)x.GetValue(this);
          if (result == null) {
            result = (AssetResourceInfoGroup)Activator.CreateInstance(x.FieldType);
            x.SetValue(this, result);
          }
          return result;
        })
        .OrderBy(x => x.SortOrder)
        .ToList();
    }
  }
}