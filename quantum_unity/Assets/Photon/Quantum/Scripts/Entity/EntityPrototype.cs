using System;
using System.Collections.Generic;
using System.Linq;
using Photon.Deterministic;
using Quantum;
using Quantum.Inspector;
using Quantum.Prototypes;
using UnityEngine;
using HideInInspectorAttribute = Quantum.Inspector.HideInInspectorAttribute;
using TooltipAttribute = Quantum.Inspector.TooltipAttribute;
using EnumFlagsAttribute = Quantum.Inspector.EnumFlagsAttribute;

public enum EntityPrototypeTransformMode {
  Transform2D = 0,
  Transform3D = 1,
  None = 2,
}

public class EntityPrototype : MonoBehaviour, IQuantumPrefabNestedAssetHost {

  [Serializable]
  public struct Transform2DVerticalInfo {
    [HideInInspectorAttribute]
    public bool IsEnabled;
    public FP Height;
    public FP PositionOffset;
  }

  [Serializable]
  public struct PhysicsColliderGeneric {
    public bool IsTrigger;
    public AssetRefPhysicsMaterial Material;

    public Component SourceCollider;

    [HideInInspectorAttribute]
    public bool IsEnabled;

    public Shape2DConfig Shape2D;

    public Shape3DConfig Shape3D;

    [DrawIf("SourceCollider", 0)]
    [Layer]
    public int Layer;
    
    public CallbackFlags_Wrapper CallbackFlags;
  }

  [Serializable]
  public struct PhysicsBodyGeneric {

    [HideInInspectorAttribute]
    public bool IsEnabled;
    
    [HideInInspectorAttribute]
    public int Version2D;
    
    [HideInInspectorAttribute]
    public int Version3D;

    [EnumFlags]
    [DisplayName("Config")]
    public PhysicsBody2D.ConfigFlags Config2D;
    
    [EnumFlags]
    [DisplayName("Config")]
    public PhysicsBody3D.ConfigFlags Config3D;
    
    [EnumFlags]
    public RotationFreezeFlags RotationFreeze;
    
    public FP Mass;
    public FP Drag;
    public FP AngularDrag;
    [DisplayName("Center Of Mass")]
    public FPVector2 CenterOfMass2D;
    [DisplayName("Center Of Mass")]
    public FPVector3 CenterOfMass3D;

    public NullableFP GravityScale;

    [Obsolete("Use Version2D or Version3D instead.")]
    public int Version {
      get => Version2D;
      set => Version2D = value;
    }

    public void EnsureVersionUpdated() {
      if (Version2D > PhysicsBody2D_Prototype.BODY_PROTOTYPE_VERSION) {
        Version2D = PhysicsBody2D_Prototype.BODY_PROTOTYPE_VERSION;
      } else {
        while (Version2D < PhysicsBody2D_Prototype.BODY_PROTOTYPE_VERSION) {
          switch (Version2D) {
            case 0:
              Config2D |= PhysicsBody2D.ConfigFlags.IsAwakenedByForces;
              break;
            case 1:
              Config2D &= ~PhysicsBody2D.ConfigFlags.UseContinuousCollisionDetection;
              break;
          }

          ++Version2D;
        }
      }

      Debug.Assert(Version2D == PhysicsBody2D_Prototype.BODY_PROTOTYPE_VERSION);

      if (Version3D > PhysicsBody3D_Prototype.BODY_PROTOTYPE_VERSION) {
        Version3D = PhysicsBody3D_Prototype.BODY_PROTOTYPE_VERSION;
      } else {
        while (Version3D < PhysicsBody3D_Prototype.BODY_PROTOTYPE_VERSION) {
          switch (Version3D) {
            case 0:
              Config3D |= PhysicsBody3D.ConfigFlags.IsAwakenedByForces;
              break;
            case 1:
              Config3D &= ~PhysicsBody3D.ConfigFlags.UseContinuousCollisionDetection;
              break;
          }

          ++Version3D;
        }
      }

      Debug.Assert(Version3D == PhysicsBody3D_Prototype.BODY_PROTOTYPE_VERSION);
    }
  }

  [Serializable]
  public struct NavMeshPathfinderInfo {
    [HideInInspectorAttribute]
    public bool IsEnabled;
    public AssetRefNavMeshAgentConfig NavMeshAgentConfig;
    [Optional("InitialTarget.IsEnabled")]
    public InitialNavMeshTargetInfo InitialTarget;
  }

  [Serializable]
  public struct InitialNavMeshTargetInfo {
    [HideInInspectorAttribute]
    public bool IsEnabled;
    public Transform Target;
    [DrawIf("Target", 0)]
    public FPVector3 Position;
    public NavMeshSpec NavMesh;
  }

  [Serializable]
  public struct NavMeshSpec {
    public MapNavMeshDefinition Reference;
    public AssetRefNavMesh Asset;
    public string Name;
  }

  [Serializable]
  public struct NavMeshSteeringAgentInfo {
    [HideInInspectorAttribute]
    public bool IsEnabled;
    [Optional("MaxSpeed.IsEnabled")]
    public OverrideFP MaxSpeed;
    [Optional("Acceleration.IsEnabled")]
    public OverrideFP Acceleration;
  }

  [Serializable]
  public struct OverrideFP {
    [HideInInspectorAttribute]
    public bool IsEnabled;
    public FP Value;
  }

  [Serializable]
  public struct NavMeshAvoidanceAgentInfo {
    [HideInInspectorAttribute]
    public bool IsEnabled;
  }

  public EntityPrototypeTransformMode TransformMode;

  [Optional("Transform2DVertical.IsEnabled")]
  [DrawIf("TransformMode", 0, DrawIfCompareOperator.Equal, DrawIfHideType.Hide)]
  public Transform2DVerticalInfo Transform2DVertical;

  [Optional("PhysicsCollider.IsEnabled")]
  [DrawIf("TransformMode", 2, DrawIfCompareOperator.NotEqual, DrawIfHideType.Hide)]
  public PhysicsColliderGeneric PhysicsCollider;

  [Optional("PhysicsBody.IsEnabled")]
  [DrawIf("PhysicsCollider.IsTrigger", 0)]
  [DrawIf("PhysicsCollider.IsEnabled", 1)]
  [DrawIf("TransformMode", 2, DrawIfCompareOperator.NotEqual, DrawIfHideType.Hide)]
  [Tooltip("To enable make sure PhysicsCollider is enabled and not a trigger")]
  public PhysicsBodyGeneric PhysicsBody = new PhysicsBodyGeneric() {
    Config2D = PhysicsBody2D.ConfigFlags.Default,
    Config3D = PhysicsBody3D.ConfigFlags.Default,
    Mass = 1,
    Drag = FP._0_50,
    AngularDrag = FP._0_50,
    CenterOfMass2D = FPVector2.Zero,
    CenterOfMass3D = FPVector3.Zero,
    GravityScale = new NullableFP(){_hasValue = 0, _value = FP._1},
  };

  [Optional("NavMeshPathfinder.IsEnabled")]
  [DrawIf("TransformMode", 2, DrawIfCompareOperator.NotEqual, DrawIfHideType.Hide)]
  public NavMeshPathfinderInfo NavMeshPathfinder;

  [Optional("NavMeshSteeringAgent.IsEnabled")]
  [DrawIf("NavMeshPathfinder.IsEnabled", 1)]
  [DrawIf("TransformMode", 2, DrawIfCompareOperator.NotEqual, DrawIfHideType.Hide)]
  public NavMeshSteeringAgentInfo NavMeshSteeringAgent;

  [Optional("NavMeshAvoidanceAgent.IsEnabled")]
  [DrawIf("NavMeshPathfinder.IsEnabled", 1)]
  [DrawIf("NavMeshSteeringAgent.IsEnabled", 1)]
  [DrawIf("TransformMode", 2, DrawIfCompareOperator.NotEqual, DrawIfHideType.Hide)]
  public NavMeshAvoidanceAgentInfo NavMeshAvoidanceAgent;

  public AssetRefEntityView View;

  Type IQuantumPrefabNestedAssetHost.NestedAssetType => typeof(EntityPrototypeAsset);
  Type IQuantumPrefabNestedAssetHost.SplitAssetType => typeof(EntityPrototypeBakedAsset);

  public void PreSerialize() {
    if (TransformMode == EntityPrototypeTransformMode.Transform2D && EntityPrototypeUtils.TrySetShapeConfigFromSourceCollider2D(PhysicsCollider.Shape2D, transform, PhysicsCollider.SourceCollider)) {
      PhysicsCollider.IsTrigger = PhysicsCollider.SourceCollider.IsColliderTrigger();
      PhysicsCollider.Layer     = PhysicsCollider.SourceCollider.gameObject.layer;
    } else if (TransformMode == EntityPrototypeTransformMode.Transform3D && EntityPrototypeUtils.TrySetShapeConfigFromSourceCollider3D(PhysicsCollider.Shape3D, transform, PhysicsCollider.SourceCollider)) {
      PhysicsCollider.IsTrigger = PhysicsCollider.SourceCollider.IsColliderTrigger();
      PhysicsCollider.Layer     = PhysicsCollider.SourceCollider.gameObject.layer;
    }

    {
      if (PhysicsBody.IsEnabled) {
        PhysicsBody.EnsureVersionUpdated();

        if (TransformMode == EntityPrototypeTransformMode.Transform2D) {
          PhysicsBody.RotationFreeze = (PhysicsBody.Config2D & PhysicsBody2D.ConfigFlags.FreezeRotation) == PhysicsBody2D.ConfigFlags.FreezeRotation ? RotationFreezeFlags.FreezeAll : default;
        }
      }
    }

    if (NavMeshPathfinder.IsEnabled) {
      if (NavMeshPathfinder.InitialTarget.Target != null) {
        NavMeshPathfinder.InitialTarget.Position = NavMeshPathfinder.InitialTarget.Target.position.ToFPVector3();
      }

      if (NavMeshPathfinder.InitialTarget.NavMesh.Reference != null) {
        NavMeshPathfinder.InitialTarget.NavMesh.Asset = new AssetRefNavMesh();
        NavMeshPathfinder.InitialTarget.NavMesh.Name = NavMeshPathfinder.InitialTarget.NavMesh.Reference.name;
      }
    }
  }

  public void SerializeImplicitComponents(Quantum.ComponentPrototypeVisitor visitor, out EntityView selfView) {
    if (TransformMode == EntityPrototypeTransformMode.Transform2D) {
      visitor.Visit(new Transform2D_Prototype() {
        Position = transform.position.ToFPVector2(),
        Rotation = transform.rotation.ToFPRotation2DDegrees(),
      });

      if (Transform2DVertical.IsEnabled) {
        visitor.Visit(new Transform2DVertical_Prototype() {
          Position = transform.position.ToFPVerticalPosition() + Transform2DVertical.PositionOffset,
          Height = Transform2DVertical.Height,
        });
      }

      if (PhysicsCollider.IsEnabled) {
        visitor.Visit(new PhysicsCollider2D_Prototype() {
          IsTrigger = PhysicsCollider.IsTrigger,
          Layer = PhysicsCollider.Layer,
          PhysicsMaterial = PhysicsCollider.Material,
          ShapeConfig = PhysicsCollider.Shape2D
        });

        visitor.Visit(new PhysicsCallbacks2D_Prototype() {
          CallbackFlags = PhysicsCollider.CallbackFlags,
        });

        if (!PhysicsCollider.IsTrigger && PhysicsBody.IsEnabled) {
          visitor.Visit(new PhysicsBody2D_Prototype() {
            Config = PhysicsBody.Config2D,
            Version = PhysicsBody.Version2D,
            AngularDrag = PhysicsBody.AngularDrag,
            Drag = PhysicsBody.Drag,
            Mass = PhysicsBody.Mass,
            CenterOfMass = PhysicsBody.CenterOfMass2D,
            GravityScale = PhysicsBody.GravityScale,
          });
        }
      }
    } else if (TransformMode == EntityPrototypeTransformMode.Transform3D) {
      visitor.Visit(new Transform3D_Prototype() {
        Position = transform.position.ToFPVector3(),
        Rotation = transform.rotation.eulerAngles.ToFPVector3(),
      });

      if (PhysicsCollider.IsEnabled) {
        visitor.Visit(new PhysicsCollider3D_Prototype() {
          IsTrigger = PhysicsCollider.IsTrigger,
          Layer = PhysicsCollider.Layer,
          PhysicsMaterial = PhysicsCollider.Material,
          ShapeConfig = PhysicsCollider.Shape3D
        });
        
        visitor.Visit(new PhysicsCallbacks3D_Prototype() {
          CallbackFlags = PhysicsCollider.CallbackFlags,
        });

        if (!PhysicsCollider.IsTrigger && PhysicsBody.IsEnabled) {
          visitor.Visit(new PhysicsBody3D_Prototype() {
            Config = PhysicsBody.Config3D,
            Version = PhysicsBody.Version3D,
            AngularDrag = PhysicsBody.AngularDrag,
            Drag = PhysicsBody.Drag,
            Mass = PhysicsBody.Mass,
            RotationFreeze = PhysicsBody.RotationFreeze,
            CenterOfMass = PhysicsBody.CenterOfMass3D,
            GravityScale = PhysicsBody.GravityScale,
          });
        }
      }
    }

    if (NavMeshPathfinder.IsEnabled) {
      var pathfinder = new NavMeshPathfinder_Prototype() {
        AgentConfig = NavMeshPathfinder.NavMeshAgentConfig
      };

      if (NavMeshPathfinder.InitialTarget.IsEnabled) {
        pathfinder.InitialTarget = NavMeshPathfinder.InitialTarget.Position;
        pathfinder.InitialTargetNavMesh = NavMeshPathfinder.InitialTarget.NavMesh.Asset;
        pathfinder.InitialTargetNavMeshName = NavMeshPathfinder.InitialTarget.NavMesh.Name;
      }

      visitor.Visit(pathfinder);

      if (NavMeshSteeringAgent.IsEnabled) {
        visitor.Visit(new NavMeshSteeringAgent_Prototype() {
          OverrideMaxSpeed = NavMeshSteeringAgent.MaxSpeed.IsEnabled,
          OverrideAcceleration = NavMeshSteeringAgent.Acceleration.IsEnabled,
          MaxSpeed = NavMeshSteeringAgent.MaxSpeed.Value,
          Acceleration = NavMeshSteeringAgent.Acceleration.Value
        });

        if (NavMeshAvoidanceAgent.IsEnabled) {
          visitor.Visit(new NavMeshAvoidanceAgent_Prototype());
        }
      }
    }

    selfView = GetComponent<EntityView>();

    if (selfView) {
      // self, don't emit view
    } else if (View.Id.IsValid) {
      visitor.Visit(new View_Prototype() {
        Current = View,
      });
    }
  }

  [System.Diagnostics.Conditional("UNITY_EDITOR")]
  public void CheckComponentDuplicates(System.Action<string> duplicateCallback) {
    CheckComponentDuplicates((type, sources) => {
      duplicateCallback($"Following components add {type.Name} prototype: {string.Join(", ", sources.Select(x => x.GetType()))}. The last one will be used.");
    });
  }

  [System.Diagnostics.Conditional("UNITY_EDITOR")]
  public void CheckComponentDuplicates(System.Action<System.Type, List<Component>> duplicateCallback) {

    var typeToSource = new Dictionary<Type, List<Component>>();

    var visitor = new CheckComponentDuplicatesVisitor() {
      Source = this,
      TypeToSources = typeToSource
    };

    SerializeImplicitComponents(visitor, out var dummy);

    foreach (var component in GetComponents<EntityComponentBase>()) {
      if (typeToSource.TryGetValue(component.PrototypeType, out var sources)) {
        sources.Add(component);
      } else {
        typeToSource.Add(component.PrototypeType, new List<Component>() { component });
      }
    }

    foreach (var kv in typeToSource) {
      if (kv.Value.Count > 1) {
        duplicateCallback(kv.Key, kv.Value);
      }
    }
  }

#if UNITY_EDITOR
  private void OnValidate() {
    try {
      PreSerialize();
    } catch (Exception ex) {
      Debug.LogError($"EntityPrototype validation error: {ex.Message}", this);
    }

    CheckComponentDuplicates(msg => Debug.LogWarning(msg, gameObject));
  }

  private void OnDrawGizmos() {
    if (Application.isPlaying)
      return;

    FPMathUtils.LoadLookupTables();

    try {
      PreSerialize();
    } catch {
    }

    Shape2DConfig config2D = null;
    Shape3DConfig config3D = null;
    bool isDynamic2D = false;
    bool isDynamic3D = false;
    float height = 0.0f;
    Vector3 position2D = transform.position;
    FP rotation2DDeg = transform.rotation.ToFPRotation2DDegrees();
    Vector3 position3D = transform.position;
    Quaternion rotation3D = transform.rotation;

    if (PhysicsCollider.IsEnabled) {
      if (TransformMode == EntityPrototypeTransformMode.Transform2D) {
        config2D = PhysicsCollider.Shape2D;
        isDynamic2D = PhysicsBody.IsEnabled && !PhysicsCollider.IsTrigger && (PhysicsBody.Config2D & PhysicsBody2D.ConfigFlags.IsKinematic) == default;
      } else if (TransformMode == EntityPrototypeTransformMode.Transform3D) {
        config3D = PhysicsCollider.Shape3D;
        isDynamic3D = PhysicsBody.IsEnabled && !PhysicsCollider.IsTrigger && (PhysicsBody.Config3D & PhysicsBody3D.ConfigFlags.IsKinematic) == default;
      }
    }

    if (Transform2DVertical.IsEnabled) {
#if QUANTUM_XY
      height = -Transform2DVertical.Height.AsFloat;
      position2D.y -= Transform2DVertical.PositionOffset.AsFloat;
#else
      height = Transform2DVertical.Height.AsFloat;
      position2D.y += Transform2DVertical.PositionOffset.AsFloat;
#endif
    }

    // handle overriding from components
    {
      var vertical = SafeGetPrototype(GetComponent<EntityComponentTransform2DVertical>());
      if (vertical != null) {
#if QUANTUM_XY
        height = -vertical.Height.AsFloat;
        position2D.z = -vertical.Position.AsFloat;
#else
        height = vertical.Height.AsFloat;
        position2D.y = vertical.Position.AsFloat;
#endif
      }


      var transform2D = SafeGetPrototype(GetComponent<EntityComponentTransform2D>());
      if (TransformMode == EntityPrototypeTransformMode.Transform2D || transform2D != null) {
        position2D = transform2D?.Position.ToUnityVector3() ?? position2D;
        rotation2DDeg = transform2D?.Rotation ?? rotation2DDeg;
        config2D = SafeGetPrototype(GetComponent<EntityComponentPhysicsCollider2D>())?.ShapeConfig ?? config2D;
        isDynamic2D |= GetComponent<EntityComponentPhysicsBody2D>();
      }

      var transform3D = SafeGetPrototype(GetComponent<EntityComponentTransform3D>());
      if (TransformMode == EntityPrototypeTransformMode.Transform3D || transform3D != null) {
        if (transform3D != null) {
          position3D = transform3D.Position.ToUnityVector3();
          rotation3D = Quaternion.Euler(transform3D.Rotation.ToUnityVector3());
        }
        config3D = SafeGetPrototype(GetComponent<EntityComponentPhysicsCollider3D>())?.ShapeConfig ?? config3D;
        isDynamic3D |= GetComponent<EntityComponentPhysicsBody3D>();
      }
    }

    if (config2D != null) {
      var color = isDynamic2D ? QuantumEditorSettings.Instance.DynamicColliderColor : QuantumEditorSettings.Instance.KinematicColliderColor;
      if (config2D.ShapeType == Shape2DType.Polygon) {
        var collider = UnityDB.FindAsset<PolygonColliderAsset>(config2D.PolygonCollider.Id);
        if (collider) {
          QuantumGameGizmos.DrawShape2DGizmo(Shape2D.CreatePolygon(collider.Settings, config2D.PositionOffset, FP.FromRaw((config2D.RotationOffset.RawValue * FP.Raw.Deg2Rad) >> FPLut.PRECISION)), position2D, rotation2DDeg.ToUnityQuaternionDegrees(), color, height, null);
        }
      } else if (config2D.ShapeType == Shape2DType.Compound) {
        foreach (var shape in config2D.CompoundShapes) {
          // nested compound shapes are not supported on the editor yet
          if (shape.ShapeType == Shape2DType.Compound) {
            continue;
          }
          
          if (shape.ShapeType == Shape2DType.Polygon) {
            var collider = UnityDB.FindAsset<PolygonColliderAsset>(shape.PolygonCollider.Id);
            if (collider) {
              QuantumGameGizmos.DrawShape2DGizmo(Shape2D.CreatePolygon(collider.Settings, shape.PositionOffset, FP.FromRaw((shape.RotationOffset.RawValue * FP.Raw.Deg2Rad) >> FPLut.PRECISION)), position2D, rotation2DDeg.ToUnityQuaternionDegrees(), color, height, null);
            }
          }
          else {
            QuantumGameGizmos.DrawShape2DGizmo(shape.CreateShape(null), position2D, rotation2DDeg.ToUnityQuaternionDegrees(), color, height, null);
          }
        }
      }
      else {
        QuantumGameGizmos.DrawShape2DGizmo(config2D.CreateShape(null), position2D, rotation2DDeg.ToUnityQuaternionDegrees(), color, height, null);
      }
    }

    if (config3D != null) {
      var color = isDynamic3D ? QuantumEditorSettings.Instance.DynamicColliderColor : QuantumEditorSettings.Instance.KinematicColliderColor;
      if (config3D.ShapeType == Shape3DType.Compound) {
        foreach (var shape in config3D.CompoundShapes) {
          // nested compound shapes are not supported on the editor yet
          if (shape.ShapeType == Shape3DType.Compound) {
            continue;
          }
          QuantumGameGizmos.DrawShape3DGizmo(shape.CreateShape(null), position3D, rotation3D, color);
        }
      }
      else {
        QuantumGameGizmos.DrawShape3DGizmo(config3D.CreateShape(null), position3D, rotation3D, color);
      }
    }
  }

  private T SafeGetPrototype<T>(EntityComponentBase<T> component) where T : Quantum.ComponentPrototype, new() {
    if (!component)
      return null;

    try {
      component.Refresh();
      return (T)component.CreatePrototype(null);
    } catch {
      return null;
    }
  }

#endif

  private class CheckComponentDuplicatesVisitor : Quantum.ComponentPrototypeVisitor {
    public EntityPrototype Source;
    public Dictionary<Type, List<Component>> TypeToSources;
    protected override void VisitFallback(ComponentPrototype prototype) {
      TypeToSources.Add(prototype.GetType(), new List<Component>() { Source });
    }
  }
}