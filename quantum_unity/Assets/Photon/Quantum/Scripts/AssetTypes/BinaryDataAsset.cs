using System;
using Photon.Deterministic;
using UnityEngine;

[CreateAssetMenu(menuName = "Quantum/Binary Data", order = Quantum.EditorDefines.AssetMenuPriorityStart + 15 * 26 + 19)]
public partial class BinaryDataAsset : AssetBase, ISerializationCallbackReceiver {
  public Quantum.BinaryData Settings;
  public TextAsset SourceTextAsset;

  public override Quantum.AssetObject AssetObject => Settings;

  public override void PrepareAsset() {
    base.PrepareAsset();

    if (SourceTextAsset != null) {
      Settings.Data = SourceTextAsset.bytes;
      Settings.IsCompressed = false;
    }
  }

  public override void Reset() {
    if (Settings == null) {
      Settings = new Quantum.BinaryData();
    }

    base.Reset();
  }

  public void SetData(byte[] data, bool compressed) {
    SourceTextAsset = null;
    Settings.IsCompressed = compressed;
    if (compressed) {
      Settings.Data = ByteUtils.GZipCompressBytes(data);
    } else {
      Settings.Data = data;
    }
  }

  public void Store(System.IO.Stream stream) {
    var bytes = Settings.Data;
    if (Settings.IsCompressed) {
      bytes = ByteUtils.GZipDecompressBytes(bytes);
    }
    stream.Write(bytes, 0, bytes.Length);
  }

  void ISerializationCallbackReceiver.OnBeforeSerialize() {
    if (SourceTextAsset != null) {
      Settings.Data = Array.Empty<byte>();
      Settings.IsCompressed = false;
    }
  }

  void ISerializationCallbackReceiver.OnAfterDeserialize() {
    
  }
}

public static partial class RawAssetAssetExt {
  public static BinaryDataAsset GetUnityAsset(this Quantum.BinaryData data) {
    return data == null ? null : UnityDB.FindAsset<BinaryDataAsset>(data);
  }
}
