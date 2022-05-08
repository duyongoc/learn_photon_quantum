using Quantum;
using System;

[Serializable]
public class QuantumStaticColliderSettings {
  public PhysicsCommon.StaticColliderMutableMode MutableMode;
  public AssetRefPhysicsMaterial                 PhysicsMaterial;
  public AssetRef                                Asset;
  public Boolean                                 Trigger;
}
