using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Quantum;
using UnityEditor;
using UnityEngine;


namespace Quantum {

  [Serializable]
  public partial class AssetResourceInfo {

    public string Path;
    public AssetRef AssetRef;

    public AssetGuid Guid {
      get => AssetRef.Id;
      set => AssetRef.Id = value;
    }

    public bool IsNestedAsset => Path.Contains(AssetBase.NestedPathSeparator);
  }
}