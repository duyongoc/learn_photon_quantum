using Photon.Realtime;
using System;
using System.Collections.Generic;
using UnityEngine;
using UI = UnityEngine.UI;

namespace Quantum.Demo {
  [CreateAssetMenu(menuName = "Quantum/Demo/PhotonAppVersions", order = Quantum.EditorDefines.AssetMenuPriorityDemo)]
  public class PhotonAppVersions : ScriptableObject {
    [Serializable]
    public enum Type {
      UsePrivateAppVersion,
      UsePhotonAppVersion,
      Custom
    }

    public List<string> CustomVersions = new List<string>();

    /// <summary>
    /// Retrieve a string that is unique for one project workspace. The PhotonPrivateAppVersion scriptable object is not added to version control, so every checkout has it's own.
    /// </summary>
    public static string Private {
      get {
        var resources = UnityEngine.Resources.LoadAll<PhotonPrivateAppVersion>("");
        if (resources.Length > 0) {
          return resources[0].Value;
        }

        return null;
      }
    }

    public static List<UI.Dropdown.OptionData> CreateDefaultDropdownOptions(AppSettings appSettings, PhotonAppVersions selectableAppVersion) {
      var options = new List<UI.Dropdown.OptionData>();
      options.Add(new UI.Dropdown.OptionData("Use Private AppVersion (recommended)"));
      options.Add(new UI.Dropdown.OptionData($"Use Photon AppVersion: '{appSettings.AppVersion}'"));
      if (selectableAppVersion) {
        foreach (var customVersion in selectableAppVersion.CustomVersions) {
          options.Add(new UI.Dropdown.OptionData($"'{customVersion}'"));
        }
      }

      return options;
    }

    public static string AppendAppVersion(Type t, PhotonAppVersions selectableAppVersion) {
      switch (t) {
        case Type.UsePrivateAppVersion:
          // Use the guid created only for this build
          if (selectableAppVersion) {
            var privateValue = PhotonAppVersions.Private;
            if (!string.IsNullOrEmpty(privateValue)) {
              return $" {privateValue}";
            }
          }
          break;

        case Type.UsePhotonAppVersion:
          // Keep the original version
          break;

        default:
          // Set a pre-defined app version to find play groups.
          var appVersionIndex = t - Type.Custom;
          if (selectableAppVersion && appVersionIndex < selectableAppVersion.CustomVersions.Count) {
            return selectableAppVersion.CustomVersions[appVersionIndex];
          } else {
            return $" Custom {appVersionIndex:00}";
          }
      }

      return string.Empty;
    }
  }
}
