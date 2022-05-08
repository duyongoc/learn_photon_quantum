using Quantum;
using Quantum.Inspector;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public partial class EntityComponentNavMeshPathfinder {

  [LocalReference]
  [DrawIf("Prototype.InitialTargetNavMesh.Id.Value", 0)]
  public MapNavMeshDefinition InitialTargetNavMeshReference;
  public override void Refresh() {
    if (InitialTargetNavMeshReference != null) {
      Prototype.InitialTargetNavMeshName = InitialTargetNavMeshReference.name;
    }
  }

#if UNITY_EDITOR

  [ContextMenu("Migrate To EntityPrototype")]
  public void Migrate() {
    var parent = GetComponent<EntityPrototype>();
    UnityEditor.Undo.RecordObject(parent, "Migrate");
    parent.NavMeshPathfinder.IsEnabled = true;
    parent.NavMeshPathfinder.InitialTarget.IsEnabled = this.Prototype.InitialTarget.HasValue;
    if (Prototype.InitialTarget.HasValue) {
      parent.NavMeshPathfinder.InitialTarget.Position = Prototype.InitialTarget.Value;
      parent.NavMeshPathfinder.InitialTarget.NavMesh.Asset = Prototype.InitialTargetNavMesh;
      parent.NavMeshPathfinder.InitialTarget.NavMesh.Name = Prototype.InitialTargetNavMeshName;
    }
    UnityEditor.Undo.DestroyObjectImmediate(this);
  }
#endif
}