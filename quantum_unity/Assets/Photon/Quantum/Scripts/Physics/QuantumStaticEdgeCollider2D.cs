using Photon.Deterministic;
using UnityEngine;
using System;
using Quantum;
using Quantum.Inspector;

public class QuantumStaticEdgeCollider2D : MonoBehaviour {
#if !QUANTUM_DISABLE_PHYSICS2D
  public EdgeCollider2D SourceCollider;

  [DrawIf("SourceCollider", 0)]
  public FPVector2 VertexA = new FPVector2(2, 2);

  [DrawIf("SourceCollider", 0)]
  public FPVector2 VertexB = new FPVector2(-2, -2);

  [DrawIf("SourceCollider", 0)]
  public FPVector2 PositionOffset;

  public FP RotationOffset;
  public FP Height;
  public QuantumStaticColliderSettings Settings;

  public void UpdateFromSourceCollider() {
    if (SourceCollider == null) {
      return;
    }

    Settings.Trigger = SourceCollider.isTrigger;
    PositionOffset   = SourceCollider.offset.ToFPVector2();

    VertexA = SourceCollider.points[0].ToFPVector2();
    VertexB = SourceCollider.points[1].ToFPVector2();
  }

  public virtual void BeforeBake() {
    UpdateFromSourceCollider();
  }

  void OnDrawGizmos() {
    if (Application.isPlaying == false) {
      UpdateFromSourceCollider();
    }

    DrawGizmos(false);
  }


  void OnDrawGizmosSelected() {
    if (Application.isPlaying == false) {
      UpdateFromSourceCollider();
    }

    DrawGizmos(true);
  }

  void DrawGizmos(Boolean selected) {
    var t     = transform;
    var pos   = t.TransformPoint(PositionOffset.ToUnityVector3());
    var rot   = transform.rotation * RotationOffset.FlipRotation().ToUnityQuaternionDegrees();
    var scale = t.localScale;

    var start = pos + rot * Vector3.Scale(scale, VertexA.ToUnityVector3());
    var end = pos + rot * Vector3.Scale(scale, VertexB.ToUnityVector3());

    var height = Height.AsFloat;
    
#if QUANTUM_XY
      height *= scale.z;
#else
      height *= scale.y;
#endif
    
    GizmoUtils.DrawGizmosEdge(start, end, height, selected, QuantumEditorSettings.Instance.StaticColliderColor);
  }
#endif
}
