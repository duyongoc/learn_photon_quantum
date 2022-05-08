using System.Diagnostics;
using Photon.Deterministic;
using UnityEngine;
using UI = UnityEngine.UI;

namespace Quantum.Demo {
  public class UIFooter : MonoBehaviour {
    private void Awake() {
      var versionText = GetComponentInChildren<UI.Text>();
      try {
        var fileVersionInfo = FileVersionInfo.GetVersionInfo(typeof(FP).Assembly.Location);
        versionText.text = $"Quantum Version: {fileVersionInfo.ProductVersion}";
      }
      catch {
        try {
          versionText.text = $"Quantum Version: {typeof(FP).Assembly.GetName().Version.ToString()}";
        }
        catch {
          versionText.text = "Quantum Version: Unknown";
        }
      }
    }
  }
}