using Photon.Deterministic;
using Quantum;
using System;
using Quantum.Inspector;
using UnityEngine;

public class QuantumStaticCircleCollider2D : MonoBehaviour {
#if !QUANTUM_DISABLE_PHYSICS2D
  public Component SourceCollider;

  [DrawIf("SourceCollider", 0)]
  public FP Radius;

  [DrawIf("SourceCollider", 0)]
  public FPVector2 PositionOffset;

  public FP Height;
  public QuantumStaticColliderSettings Settings;

  public void UpdateFromSourceCollider() {
    if (SourceCollider == null) {
      return;
    }

    switch (SourceCollider) {
#if !QUANTUM_DISABLE_PHYSICS3D
      case SphereCollider sphere:
        Radius           = sphere.radius.ToFP();
        PositionOffset   = sphere.center.ToFPVector2();
        Settings.Trigger = sphere.isTrigger;
        break;
#endif

      case CircleCollider2D circle:
        Radius           = circle.radius.ToFP();
        PositionOffset   = circle.offset.ToFPVector2();
        Settings.Trigger = circle.isTrigger;
        break;

      default:
        SourceCollider = null;
        break;
    }
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

    var height = Height.AsFloat;
#if QUANTUM_XY
    height *= -1.0f;
    height *= transform.localScale.z;
#else
    height *= transform.localScale.y;
#endif

    var t = transform;
    GizmoUtils.DrawGizmosCircle(
      t.TransformPoint(PositionOffset.ToUnityVector3()),
      Radius.AsFloat * Mathf.Max(t.localScale.x, t.localScale.y),
      height,
      selected,
      QuantumEditorSettings.Instance.StaticColliderColor);
  }
#endif
}
