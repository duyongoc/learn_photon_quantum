using UnityEngine;
using Quantum;
using Quantum.Editor;
#if UNITY_EDITOR
using UnityEditor;
#endif


public partial class EntityComponentTransform2DVertical {

  [Tooltip("If not set lossyScale.y of the transform will be used")]
  public bool AutoSetHeight = true;

  public bool AutoSetPosition = true;

  private void OnValidate() {
    Refresh();
  }

  public override void Refresh() {
    if (AutoSetPosition) {
      // based this on MapDataBaker for colliders
#if QUANTUM_XY
      Prototype.Position = -transform.position.z.ToFP();
#else
      Prototype.Position = transform.position.y.ToFP();
#endif
    }

    if (AutoSetHeight) {
      Prototype.Height = transform.lossyScale.y.ToFP();
    }
  }

#if UNITY_EDITOR

  [ContextMenu("Migrate To EntityPrototype")]
  public void Migrate() {
    var parent = GetComponent<EntityPrototype>();
    UnityEditor.Undo.RecordObject(parent, "Migrate");
    parent.Transform2DVertical.IsEnabled = true;
    parent.Transform2DVertical.Height = Prototype.Height;
    parent.Transform2DVertical.PositionOffset = Prototype.Position - transform.position.ToFPVerticalPosition();
    parent.TransformMode = EntityPrototypeTransformMode.Transform2D;
    UnityEditor.Undo.DestroyObjectImmediate(this);
  }

  public override void OnInspectorGUI(SerializedObject so, IQuantumEditorGUI QuantumEditorGUI) {
    var autoSetPosition = so.FindPropertyOrThrow(nameof(EntityComponentTransform2DVertical.AutoSetPosition));
    var autoSetHeight = so.FindPropertyOrThrow(nameof(EntityComponentTransform2DVertical.AutoSetHeight));

    EditorGUILayout.PropertyField(autoSetPosition);
    EditorGUILayout.PropertyField(autoSetHeight);

    using (new EditorGUI.DisabledScope(autoSetPosition.boolValue)) {
      QuantumEditorGUI.PropertyField(so.FindPropertyOrThrow("Prototype.Position"));
    }
    using (new EditorGUI.DisabledScope(autoSetHeight.boolValue)) {
      QuantumEditorGUI.PropertyField(so.FindPropertyOrThrow("Prototype.Height"));
    }
  }
#endif
}