using Quantum;
using Quantum.Prototypes;
using UnityEngine;

[RequireComponent(typeof(EntityComponentPhysicsCollider2D))]
public partial class EntityComponentPhysicsCallbacks2D {

#if UNITY_EDITOR

  [ContextMenu("Migrate To EntityPrototype")]
  public void Migrate() {
    var parent = GetComponent<EntityPrototype>();
    UnityEditor.Undo.RecordObject(parent, "Migrate");
    parent.PhysicsCollider.CallbackFlags = Prototype.CallbackFlags;
    UnityEditor.Undo.DestroyObjectImmediate(this);
  }

#endif
}