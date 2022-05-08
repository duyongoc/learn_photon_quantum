using Photon.Realtime;
using System;
using System.Collections.Generic;
using UnityEngine;
using UI = UnityEngine.UI;

namespace Quantum.Demo {
  [CreateAssetMenu(menuName = "Quantum/Demo/PhotonRegions", order = EditorDefines.AssetMenuPriorityDemo)]
  public class PhotonRegions : ScriptableObject {
    [Serializable]
    public struct RegionInfo {
      public string Name;
      public string Token;
    }

    public List<RegionInfo> Regions = new List<RegionInfo>();

    public static List<UI.Dropdown.OptionData> CreateDefaultDropdownOptions(out int selectedOption, string lastSelectedRegionToken, AppSettings appSettings, PhotonRegions selectableRegions) {
      // Find best initial value for the region select
      selectedOption = 0;
      if (string.IsNullOrEmpty(lastSelectedRegionToken)) {
        lastSelectedRegionToken = appSettings.FixedRegion;
      }

      // Create region options
      var options = new List<UI.Dropdown.OptionData>();

      // first one is always best region
      options.Add(new UI.Dropdown.OptionData("Best Region"));
      if (selectableRegions) {
        foreach (var photonRegion in selectableRegions.Regions) {
          options.Add(new UI.Dropdown.OptionData(photonRegion.Name));
          if (photonRegion.Token == lastSelectedRegionToken) {
            selectedOption = options.Count - 1;
          }
        }
      } else {
        options.Add(new UI.Dropdown.OptionData(appSettings.FixedRegion));
        if (lastSelectedRegionToken == appSettings.FixedRegion) {
          selectedOption = options.Count - 1;
        }
      }

      return options;
    }
  }
}
