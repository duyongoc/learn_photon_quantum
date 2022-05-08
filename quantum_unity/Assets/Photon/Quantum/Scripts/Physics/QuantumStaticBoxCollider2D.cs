using Photon.Deterministic;
using Quantum;
using System;
using Quantum.Inspector;
using UnityEngine;

public class QuantumStaticBoxCollider2D : MonoBehaviour {
#if !QUANTUM_DISABLE_PHYSICS2D
  public Component SourceCollider;

  [DrawIf("SourceCollider", 0)]
  public FPVector2 Size;

  [DrawIf("SourceCollider", 0)]
  public FPVector2 PositionOffset;

  public FP RotationOffset;
  public FP Height;
  public QuantumStaticColliderSettings Settings;

  public void UpdateFromSourceCollider() {
    if (SourceCollider == null) {
      return;
    }
    
    switch (SourceCollider) {
#if !QUANTUM_DISABLE_PHYSICS3D
      case BoxCollider box:
        Size             = box.size.ToFPVector2();
        PositionOffset   = box.center.ToFPVector2();
        Settings.Trigger = box.isTrigger;
        break;
#endif

      case BoxCollider2D box:
        Size             = box.size.ToFPVector2();
        PositionOffset   = box.offset.ToFPVector2();
        Settings.Trigger = box.isTrigger;
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

    var size = Size.ToUnityVector3();
    var offset = Vector3.zero;

#if QUANTUM_XY
    size.z = -Height.AsFloat;
    offset.z = size.z / 2.0f;
#else
    size.y = Height.AsFloat;
    offset.y = size.y / 2.0f;
#endif

    var t = transform;
    var matrix = Matrix4x4.TRS(
      t.TransformPoint(PositionOffset.ToUnityVector3()),
      t.rotation * RotationOffset.FlipRotation().ToUnityQuaternionDegrees(),
      t.localScale) * Matrix4x4.Translate(offset);
    GizmoUtils.DrawGizmosBox(matrix, size, selected, QuantumEditorSettings.Instance.StaticColliderColor);
  }
#endif
}
