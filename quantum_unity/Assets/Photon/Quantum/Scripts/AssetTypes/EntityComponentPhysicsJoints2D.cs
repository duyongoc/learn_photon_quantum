using System;
using UnityEngine;
using Photon.Deterministic;
using Quantum;
#if UNITY_EDITOR
using UnityEditor;
#endif

[RequireComponent(typeof(EntityPrototype))]
public partial class EntityComponentPhysicsJoints2D {
  private void OnValidate() => AutoConfigureDistance();

  public override void Refresh() => AutoConfigureDistance();

  private void AutoConfigureDistance() {
    if (Prototype.JointConfigs == null) {
      return;
    }

    FPMathUtils.LoadLookupTables();

    foreach (var config in Prototype.JointConfigs) {
      if (config.AutoConfigureDistance && config.JointType != Quantum.Physics2D.JointType.None) {
        var anchorPos    = transform.position.ToFPVector2() + FPVector2.Rotate(config.Anchor, transform.rotation.ToFPRotation2D());
        var connectedPos = config.ConnectedAnchor;

        if (config.ConnectedEntity != null) {
          var connectedTransform = config.ConnectedEntity.transform;
          connectedPos =  FPVector2.Rotate(connectedPos, connectedTransform.rotation.ToFPRotation2D());
          connectedPos += connectedTransform.position.ToFPVector2();
        }

        config.Distance    = FPVector2.Distance(anchorPos, connectedPos);
        config.MinDistance = config.Distance;
        config.MaxDistance = config.Distance;
      }

      if (config.MinDistance > config.MaxDistance) {
        config.MinDistance = config.MaxDistance;
      }
    }
  }

#if UNITY_EDITOR
  private void OnDrawGizmos() {
    if (Application.isPlaying && (QuantumEditorSettings.Instance.DrawJointGizmos & QuantumEditorSettings.GizmosMode.OnApplicationPlaying) == default) {
      return;
    }

    if ((QuantumEditorSettings.Instance.DrawJointGizmos & QuantumEditorSettings.GizmosMode.OnDraw) == QuantumEditorSettings.GizmosMode.OnDraw) {
      DrawGizmos(selected: false);
    }
  }

  private void OnDrawGizmosSelected() {
    if (Application.isPlaying && (QuantumEditorSettings.Instance.DrawJointGizmos & QuantumEditorSettings.GizmosMode.OnApplicationPlaying) == default) {
      return;
    }

    if ((QuantumEditorSettings.Instance.DrawJointGizmos & QuantumEditorSettings.GizmosMode.OnSelected) == QuantumEditorSettings.GizmosMode.OnSelected) {
      DrawGizmos(selected: true);
    }
  }

  private void DrawGizmos(bool selected) {
    var entity = GetComponent<EntityPrototype>();

    if (entity == null || Prototype.JointConfigs == null) {
      return;
    }

    FPMathUtils.LoadLookupTables();

    const float anchorRadiusFactor           = 0.1f;
    const float barHalfLengthFactor          = 0.1f;
    const float hingeRefAngleBarLengthFactor = 0.5f;
    const float unselectedColorAlphaRatio    = 0.6f;
    const float solidDiscAlphaRatio          = 0.15f;

    var editorSettings = QuantumEditorSettings.Instance;
    var gizmosScale    = editorSettings.GizmoIconScale.AsFloat;

    var primColor    = editorSettings.JointGizmosPrimaryColor;
    var secColor     = editorSettings.JointGizmosSecondaryColor;
    var warningColor = editorSettings.JointGizmosWarningColor;

    if (selected == false) {
      primColor    = primColor.Alpha(primColor.a       * unselectedColorAlphaRatio);
      secColor     = secColor.Alpha(secColor.a         * unselectedColorAlphaRatio);
      warningColor = warningColor.Alpha(warningColor.a * unselectedColorAlphaRatio);
    }

    foreach (var config in Prototype.JointConfigs) {
      if (config.JointType == Quantum.Physics2D.JointType.None) {
        continue;
      }

      Debug.Assert(entity != null);

      var anchorPos    = transform.position.ToFPVector2() + FPVector2.Rotate(config.Anchor, transform.rotation.ToFPRotation2D());
      var connectedPos = config.ConnectedAnchor;

      Transform connectedTransform = null;
      if (config.ConnectedEntity != null) {
        connectedTransform =  config.ConnectedEntity.transform;
        connectedPos       =  FPVector2.Rotate(connectedPos, connectedTransform.rotation.ToFPRotation2D());
        connectedPos       += connectedTransform.position.ToFPVector2();
      }

      var anchorPosVec3          = anchorPos.ToUnityVector3();
      var connectedAnchorPosVec3 = connectedPos.ToUnityVector3();

      if (Application.isPlaying == false) {
        if (config.AutoConfigureDistance) {
          config.Distance    = FPVector2.Distance(anchorPos, connectedPos);
          config.MinDistance = config.Distance;
          config.MaxDistance = config.Distance;
        }

        if (config.MinDistance > config.MaxDistance) {
          config.MinDistance = config.MaxDistance;
        }
      }

      Gizmos.color = secColor;
      Gizmos.DrawSphere(anchorPosVec3, gizmosScale          * anchorRadiusFactor);
      Gizmos.DrawSphere(connectedAnchorPosVec3, gizmosScale * anchorRadiusFactor);
      Gizmos.DrawLine(anchorPosVec3, connectedAnchorPosVec3);

      switch (config.JointType) {
        case Quantum.Physics2D.JointType.DistanceJoint: {
          var connectedToAnchorDir = (anchorPos    - connectedPos).Normalized;
          var minDistanceMark      = (connectedPos + connectedToAnchorDir * config.MinDistance).ToUnityVector3();
          var maxDistanceMark      = (connectedPos + connectedToAnchorDir * config.MaxDistance).ToUnityVector3();

          FPVector2 orthogonal;
          orthogonal.X = -connectedToAnchorDir.Y;
          orthogonal.Y = connectedToAnchorDir.X;

          var orthogonalVec3 = orthogonal.ToUnityVector3() * gizmosScale * barHalfLengthFactor;

          Gizmos.color = primColor;
          Gizmos.DrawLine(minDistanceMark, maxDistanceMark);
          Gizmos.DrawLine(minDistanceMark - orthogonalVec3, minDistanceMark + orthogonalVec3);
          Gizmos.DrawLine(maxDistanceMark - orthogonalVec3, maxDistanceMark + orthogonalVec3);
          break;
        }

        case Quantum.Physics2D.JointType.SpringJoint: {
          var connectedToAnchorDir = (anchorPos    - connectedPos).Normalized;
          var distanceMark         = (connectedPos + connectedToAnchorDir * config.Distance).ToUnityVector3();

          FPVector2 orthogonal;
          orthogonal.X = -connectedToAnchorDir.Y;
          orthogonal.Y = connectedToAnchorDir.X;

          var orthogonalVec3 = orthogonal.ToUnityVector3() * gizmosScale * barHalfLengthFactor;

          Gizmos.color = primColor;
          Gizmos.DrawLine(connectedAnchorPosVec3, distanceMark);
          Gizmos.DrawLine(distanceMark - orthogonalVec3, distanceMark + orthogonalVec3);
          break;
        }

        case Quantum.Physics2D.JointType.HingeJoint: {
          var hingeRefAngleBarLength = hingeRefAngleBarLengthFactor * editorSettings.GizmoIconScale.AsFloat;
          var connectedAnchorRight = connectedTransform == null ? Vector3.right : connectedTransform.right;
          var anchorRight = transform.right;
          
          Gizmos.color  = secColor;
          Gizmos.DrawRay(anchorPosVec3, anchorRight * hingeRefAngleBarLength);

          Gizmos.color  = primColor;
          Handles.color = primColor;
          Gizmos.DrawRay(connectedAnchorPosVec3, connectedAnchorRight * hingeRefAngleBarLength);

#if QUANTUM_XY
          var planeNormal = -Vector3.forward;
#else
          var planeNormal = Vector3.up;
#endif

          if (config.UseAngleLimits) {
            var fromDir    = Quaternion.AngleAxis(config.LowerAngle.AsFloat, planeNormal) * connectedAnchorRight;
            var angleRange = (config.UpperAngle - config.LowerAngle).AsFloat;

            if (angleRange < 0.0f) {
              Handles.color = warningColor;
            }

            Handles.DrawWireArc(connectedAnchorPosVec3, planeNormal, fromDir, angleRange, hingeRefAngleBarLength);

            Handles.color = Handles.color.Alpha(Handles.color.a * solidDiscAlphaRatio);
            Handles.DrawSolidArc(connectedAnchorPosVec3, planeNormal, fromDir, angleRange, hingeRefAngleBarLength);
          } else {
            // Draw full disc
            Handles.DrawWireDisc(connectedAnchorPosVec3, planeNormal, hingeRefAngleBarLength);

            Handles.color = Handles.color.Alpha(Handles.color.a * solidDiscAlphaRatio);
            Handles.DrawSolidDisc(connectedAnchorPosVec3, planeNormal, hingeRefAngleBarLength);
          }

          break;
        }
      }
    }
  }
#endif
}