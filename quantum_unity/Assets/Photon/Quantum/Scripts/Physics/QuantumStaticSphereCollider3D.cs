using Photon.Deterministic;
using Quantum;
using System;
using Quantum.Inspector;
using UnityEngine;

public class QuantumStaticSphereCollider3D : MonoBehaviour {
#if !QUANTUM_DISABLE_PHYSICS3D
  public SphereCollider SourceCollider;

  [DrawIf("SourceCollider", 0)]
  public FP Radius;

  [DrawIf("SourceCollider", 0)]
  public FPVector3 PositionOffset;

  public QuantumStaticColliderSettings Settings;

  public void UpdateFromSourceCollider() {
    if (SourceCollider == null) {
      return;
    }

    Radius           = SourceCollider.radius.ToFP();
    PositionOffset   = SourceCollider.center.ToFPVector3();
    Settings.Trigger = SourceCollider.isTrigger;
  }

  public virtual void BeforeBake() {
    UpdateFromSourceCollider();
  }

  void OnDrawGizmos() {
    if (Application.isPlaying == false) {
      UpdateFromSourceCollider();
    }

    DrawGizmo(false);
  }

  void OnDrawGizmosSelected() {
    if (Application.isPlaying == false) {
      UpdateFromSourceCollider();
    }

    DrawGizmo(true);
  }

  void DrawGizmo(Boolean selected) {
    // the radius with which the sphere with be baked into the map
    var radius = Radius.AsFloat * transform.localScale.x;
    
    GizmoUtils.DrawGizmosSphere(transform.TransformPoint(PositionOffset.ToUnityVector3()), radius, selected, QuantumEditorSettings.Instance.StaticColliderColor);
  }
#endif
}
