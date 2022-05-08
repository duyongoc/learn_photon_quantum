using System;
using Quantum;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using Quantum.Editor;
#endif

public partial class EntityComponentTransform2D {
  public bool AutoSetPosition = true;
  public bool AutoSetRotation = true;

  private void OnValidate() {
    Refresh();
  }

  public override void Refresh() {
    if (AutoSetPosition) {
      Prototype.Position = transform.position.ToFPVector2();
    }
    if (AutoSetRotation) {
      Prototype.Rotation = transform.rotation.ToFPRotation2DDegrees();
    }
  }

#if UNITY_EDITOR

  [ContextMenu("Migrate To EntityPrototype")]
  public void Migrate() {
    var parent = GetComponent<EntityPrototype>();
    UnityEditor.Undo.RecordObject(parent, "Migrate");
    parent.TransformMode = EntityPrototypeTransformMode.Transform2D;
    UnityEditor.Undo.DestroyObjectImmediate(this);
  }

  public override void OnInspectorGUI(SerializedObject so, IQuantumEditorGUI QuantumEditorGUI) {
    var autoSetPosition = so.FindPropertyOrThrow(nameof(EntityComponentTransform2D.AutoSetPosition));
    var autoSetRotation = so.FindPropertyOrThrow(nameof(EntityComponentTransform2D.AutoSetRotation));

    EditorGUILayout.PropertyField(autoSetPosition);
    EditorGUILayout.PropertyField(autoSetRotation);

    using (new EditorGUI.DisabledScope(autoSetPosition.boolValue)) {
      QuantumEditorGUI.PropertyField(so.FindPropertyOrThrow("Prototype.Position"));
    }
    using (new EditorGUI.DisabledScope(autoSetRotation.boolValue)) {
      QuantumEditorGUI.PropertyField(so.FindPropertyOrThrow("Prototype.Rotation"));
    }
  }
#endif
}