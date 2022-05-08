using System;
using Quantum;
using Quantum.Editor;
using UnityEngine;
using UnityEngine.Serialization;

[RequireComponent(typeof(EntityPrototype))]
public abstract class EntityComponentBase : MonoBehaviour {
  private const string ExpectedTypeNamePrefix = "EntityComponent";

  public abstract System.Type PrototypeType { get; }
  public System.Type ComponentType => ComponentPrototype.PrototypeTypeToComponentType(PrototypeType);

  public virtual void Refresh() {
  }

  public abstract ComponentPrototype CreatePrototype(EntityPrototypeConverter converter);

  public static Type UnityComponentTypeToQuantumPrototypeType(Type type) {
    if (type == null) {
      throw new ArgumentNullException(nameof(type));
    }

    var baseType = type.BaseType;
    if (baseType?.IsGenericType == true &&
        (baseType.GetGenericTypeDefinition() == typeof(EntityComponentBase<>) || baseType.GetGenericTypeDefinition() == typeof(EntityComponentBase<,>))) {
      return baseType.GetGenericArguments()[0];
    } else {
      throw new InvalidOperationException($"Type {type} is not a subclass of {typeof(EntityComponentBase<>)} or {typeof(EntityComponentBase<,>)}");
    }
  }

  public static Type UnityComponentTypeToQuantumComponentType(Type type) => ComponentPrototype.PrototypeTypeToComponentType(UnityComponentTypeToQuantumPrototypeType(type));

#if UNITY_EDITOR
  public virtual void OnInspectorGUI(UnityEditor.SerializedObject so, IQuantumEditorGUI QuantumEditorGUI) {
    DrawPrototype(so, QuantumEditorGUI);
    DrawNonPrototypeFields(so, QuantumEditorGUI);
  }

  protected void DrawPrototype(UnityEditor.SerializedObject so, IQuantumEditorGUI QuantumEditorGUI) {
    QuantumEditorGUI.Inspector(so, "Prototype");
  }

  protected void DrawNonPrototypeFields(UnityEditor.SerializedObject so, IQuantumEditorGUI QuantumEditorGUI) {
    QuantumEditorGUI.Inspector(so, filters: new[] { "Prototype" }, drawScript: false);
  }

#endif
}

public abstract class EntityComponentBase<TPrototype> : EntityComponentBase
  where TPrototype : ComponentPrototype, new() {

  [FormerlySerializedAs("prototype")]
  public TPrototype Prototype = new TPrototype();

  public override System.Type PrototypeType => typeof(TPrototype);

  [Obsolete("Use Prototype field")]
  public TPrototype prototype => Prototype;

  public override ComponentPrototype CreatePrototype(EntityPrototypeConverter converter) {
    return Prototype;
  }
}

public abstract class EntityComponentBase<TPrototype, TAdapter> : EntityComponentBase
  where TPrototype : ComponentPrototype, new()
  where TAdapter : PrototypeAdapter<TPrototype>, new() {

  [FormerlySerializedAs("prototype")]
  public TAdapter Prototype = new TAdapter();

  public override System.Type PrototypeType => typeof(TPrototype);

  [Obsolete("Use Prototype field")]
  public TAdapter prototype => Prototype;

  public override ComponentPrototype CreatePrototype(EntityPrototypeConverter converter) {
    return Prototype.Convert(converter);
  }
}