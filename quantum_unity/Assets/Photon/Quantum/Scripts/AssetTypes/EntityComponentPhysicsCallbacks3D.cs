using Quantum;
using Quantum.Prototypes;
using UnityEngine;

[RequireComponent(typeof(EntityComponentPhysicsCollider3D))]
public partial class EntityComponentPhysicsCallbacks3D {

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