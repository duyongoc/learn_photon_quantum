using Quantum;
using Quantum.Prototypes;
using UnityEngine;

[RequireComponent(typeof(EntityComponentPhysicsCollider3D))]
public partial class EntityComponentPhysicsBody3D {

#if UNITY_EDITOR
  
  private void OnValidate() {
    Prototype.EnsureVersionUpdated();
  }

  [ContextMenu("Migrate To EntityPrototype")]
  public void Migrate() {
    var parent = GetComponent<EntityPrototype>();
    UnityEditor.Undo.RecordObject(parent, "Migrate");
    Prototype.EnsureVersionUpdated();
    parent.PhysicsBody.IsEnabled = true;
    parent.PhysicsBody.Version3D = Prototype.Version;
    parent.PhysicsBody.AngularDrag = Prototype.AngularDrag;
    parent.PhysicsBody.Drag = Prototype.Drag;
    parent.PhysicsBody.Mass = Prototype.Mass;
    parent.PhysicsBody.RotationFreeze = Prototype.RotationFreeze;
    parent.PhysicsBody.CenterOfMass3D = Prototype.CenterOfMass;
    parent.PhysicsBody.GravityScale = Prototype.GravityScale;
    UnityEditor.Undo.DestroyObjectImmediate(this);
  }

#endif
}