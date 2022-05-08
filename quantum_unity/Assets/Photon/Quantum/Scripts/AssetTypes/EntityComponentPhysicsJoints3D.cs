using UnityEngine;
using Photon.Deterministic;
using Quantum;
#if UNITY_EDITOR
using UnityEditor;

#endif

[RequireComponent(typeof(EntityPrototype))]
public partial class EntityComponentPhysicsJoints3D {
  private void OnValidate() => Refresh();

  public override void Refresh() {
    AutoConfigureDistance();
  }

  private void AutoConfigureDistance() {
    if (Prototype.JointConfigs == null) {
      return;
    }

    FPMathUtils.LoadLookupTables();

    foreach (var config in Prototype.JointConfigs) {
      if (config.AutoConfigureDistance && config.JointType != Quantum.Physics3D.JointType3D.None) {
        var anchorPos    = transform.position.ToFPVector3() + transform.rotation.ToFPQuaternion() * config.Anchor;
        var connectedPos = config.ConnectedAnchor;

        if (config.ConnectedEntity != null) {
          var connectedTransform = config.ConnectedEntity.transform;
          connectedPos =  connectedTransform.rotation.ToFPQuaternion() * connectedPos;
          connectedPos += connectedTransform.position.ToFPVector3();
        }

        config.Distance    = FPVector3.Distance(anchorPos, connectedPos);
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
    if (Application.isPlaying == false && (QuantumEditorSettings.Instance.DrawJointGizmos & QuantumEditorSettings.GizmosMode.OnDraw) == QuantumEditorSettings.GizmosMode.OnDraw) {
      DrawGizmos(selected: false);
    }
  }

  private void OnDrawGizmosSelected() {
    if (Application.isPlaying == false && (QuantumEditorSettings.Instance.DrawJointGizmos & QuantumEditorSettings.GizmosMode.OnSelected) == QuantumEditorSettings.GizmosMode.OnSelected) {
      DrawGizmos(selected: true);
    }
  }

  private void DrawGizmos(bool selected) {
    var entity = GetComponent<EntityPrototype>();

    if (entity == null || Prototype.JointConfigs == null) {
      return;
    }

    FPMathUtils.LoadLookupTables();
    
    var editorSettings = QuantumEditorSettings.Instance;
    foreach (var config in Prototype.JointConfigs) {
      GizmoUtils.DrawGizmosJoint3D(config, transform, config.ConnectedEntity == null ? null : config.ConnectedEntity.transform, selected, editorSettings);
    }
  }
#endif
}