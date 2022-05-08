using System.IO;
using UnityEditor;
using UnityEngine;

namespace Quantum.Demo {
  public class PhotonAppVersionsPostprocessor : AssetPostprocessor {
    static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths) {
      if (QuantumEditorSettings.InstanceFailSilently?.UsePhotonAppVersionsPostprocessor == true) {
        foreach (var importedAsset in importedAssets) {
          if (importedAsset.EndsWith("PhotonAppVersions.asset")) {
            var assets = AssetDatabase.FindAssets("t:PhotonPrivateAppVersion");
            if (assets.Length == 0) {
              var    so               = ScriptableObject.CreateInstance<PhotonPrivateAppVersion>();
              var    path             = importedAsset.Replace(Path.GetFileName(importedAsset), "");
              string assetPathAndName = AssetDatabase.GenerateUniqueAssetPath(path + "PhotonPrivateAppVersion.asset");
              AssetDatabase.CreateAsset(so, assetPathAndName);
              AssetDatabase.SaveAssets();
              AssetDatabase.Refresh();
              Debug.Log($"Creating a local version of PhotonPrivateAppVersion with guid '{so.Value}' to '{assetPathAndName}'");
            }
          }
        }
      }
    }
  }
}