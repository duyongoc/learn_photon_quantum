using Quantum;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using Quantum.Editor;
#endif

public partial class EntityComponentPhysicsCollider2D {
#if !QUANTUM_DISABLE_PHYSICS2D
  [MultiTypeReference(typeof(BoxCollider2D), typeof(CircleCollider2D)
#if !QUANTUM_DISABLE_PHYSICS3D
    , typeof(BoxCollider), typeof(SphereCollider)
#endif
    )]
  public Component SourceCollider;

  private void OnValidate() {
    if (EntityPrototypeUtils.TrySetShapeConfigFromSourceCollider2D(Prototype.ShapeConfig, transform, SourceCollider)) {
      Prototype.IsTrigger = SourceCollider.IsColliderTrigger();
      Prototype.Layer     = SourceCollider.gameObject.layer;
    }
  }

  public override void Refresh() {
    if (EntityPrototypeUtils.TrySetShapeConfigFromSourceCollider2D(Prototype.ShapeConfig, transform, SourceCollider)) {
      Prototype.IsTrigger = SourceCollider.IsColliderTrigger();
      Prototype.Layer     = SourceCollider.gameObject.layer;
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
    parent.PhysicsCollider.Shape2D = Prototype.ShapeConfig;
    parent.PhysicsCollider.SourceCollider = SourceCollider;
    UnityEditor.Undo.DestroyObjectImmediate(this);
  }

  public override void OnInspectorGUI(SerializedObject so, IQuantumEditorGUI QuantumEditorGUI) {
    var sourceCollider = so.FindPropertyOrThrow(nameof(EntityComponentPhysicsCollider2D.SourceCollider));

    EditorGUILayout.PropertyField(sourceCollider);

    bool enterChildren = true;
    for (var p = so.FindPropertyOrThrow("Prototype"); p.Next(enterChildren) && p.depth >= 1; enterChildren = false) {
      using (new EditorGUI.DisabledScope(sourceCollider.objectReferenceValue != null && 
                                         (p.name == nameof(Quantum.Prototypes.PhysicsCollider2D_Prototype.Layer) || 
                                         p.name == nameof(Quantum.Prototypes.PhysicsCollider2D_Prototype.IsTrigger)))) {
        QuantumEditorGUI.PropertyField(p);
      }
    }

    try {
      // sync with Unity collider, if set
      ((EntityComponentPhysicsCollider2D)so.targetObject).Refresh();
    } catch (System.Exception ex) {
      EditorGUILayout.HelpBox(ex.Message, MessageType.Error);
    }
  }

#endif
#endif
}