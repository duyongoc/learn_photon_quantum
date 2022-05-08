using System;
using Quantum;
using Quantum.Prototypes;
using UnityEngine;

[RequireComponent(typeof(EntityComponentPhysicsCollider2D))]
public partial class EntityComponentPhysicsBody2D {

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
    parent.PhysicsBody.Version2D = Prototype.Version;
    parent.PhysicsBody.Config2D = Prototype.Config;
    parent.PhysicsBody.AngularDrag = Prototype.AngularDrag;
    parent.PhysicsBody.Drag = Prototype.Drag;
    parent.PhysicsBody.Mass = Prototype.Mass;
    parent.PhysicsBody.CenterOfMass2D = Prototype.CenterOfMass;
    parent.PhysicsBody.GravityScale = Prototype.GravityScale;
    UnityEditor.Undo.DestroyObjectImmediate(this);
  }

#endif
}