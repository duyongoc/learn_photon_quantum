using Quantum;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using Quantum.Editor;
#endif

public partial class EntityComponentPhysicsCollider3D {
#if !QUANTUM_DISABLE_PHYSICS3D
  [MultiTypeReference(typeof(BoxCollider), typeof(SphereCollider))]
  public Collider SourceCollider3D;

  private void OnValidate() {
    if (EntityPrototypeUtils.TrySetShapeConfigFromSourceCollider3D(Prototype.ShapeConfig, transform, SourceCollider3D)) {
      Prototype.IsTrigger = SourceCollider3D.isTrigger;
      Prototype.Layer     = SourceCollider3D.gameObject.layer;
    }
  }

  public override void Refresh() {
    if (EntityPrototypeUtils.TrySetShapeConfigFromSourceCollider3D(Prototype.ShapeConfig, transform, SourceCollider3D)) {
      Prototype.IsTrigger = SourceCollider3D.isTrigger;
      Prototype.Layer     = SourceCollider3D.gameObject.layer;
    }
  }

#if UNITY_EDITOR

  [ContextMenu("Migrate To EntityPrototype")]
  public void Migrate() {
    var parent = GetComponent<EntityPrototype>();
    UnityEditor.Undo.RecordObject(parent, "Migrate");
    parent.PhysicsCollider.IsEnabled = true;
    parent.PhysicsCollider.IsTrigger = Prototype.IsTrigger;
    parent.PhysicsCollider.Layer = Prototype.Layer;
    parent.PhysicsCollider.Material = Prototype.PhysicsMaterial;
    parent.PhysicsCollider.Shape3D = Prototype.ShapeConfig;
    parent.PhysicsCollider.SourceCollider = SourceCollider3D;
    UnityEditor.Undo.DestroyObjectImmediate(this);
  }

  public override void OnInspectorGUI(SerializedObject so, IQuantumEditorGUI QuantumEditorGUI) {
    var sourceCollider = so.FindPropertyOrThrow(nameof(EntityComponentPhysicsCollider3D.SourceCollider3D));

    EditorGUILayout.PropertyField(sourceCollider);

    bool enterChildren = true;
    for (var p = so.FindPropertyOrThrow("Prototype"); p.Next(enterChildren) && p.depth >= 1; enterChildren = false) {
      using (new EditorGUI.DisabledScope(sourceCollider.objectReferenceValue != null && 
                                         (p.name == nameof(Quantum.Prototypes.PhysicsCollider3D_Prototype.Layer) || 
                                          p.name == nameof(Quantum.Prototypes.PhysicsCollider3D_Prototype.IsTrigger)))) {
        QuantumEditorGUI.PropertyField(p);
      }
    }

    try {
      // sync with Unity collider, if set
      ((EntityComponentPhysicsCollider3D)so.targetObject).Refresh();
    } catch (System.Exception ex) {
      EditorGUILayout.HelpBox(ex.Message, MessageType.Error);
    }
  }

#endif
#endif
}