using UnityEditor;

namespace Quantum.Demo {
  [CustomEditor(typeof(PhotonPrivateAppVersion), false)]
  public class PhotonPrivateAppVersionEditor : UnityEditor.Editor {
    public override void OnInspectorGUI() {
      base.OnInspectorGUI();
      EditorGUILayout.HelpBox("This object is created automatically by an AssetPostprocessor and is used in the Quantum demo menus.\nCan be disabled in QuantumEditorSettings.UsePhotonAppVersionsPostprocessor.", MessageType.Warning);
    }
  }
}
