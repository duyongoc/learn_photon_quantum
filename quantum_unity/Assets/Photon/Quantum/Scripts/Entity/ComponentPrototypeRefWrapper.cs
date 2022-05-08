using System;
using Quantum;
using UnityEngine;

public abstract class ComponentPrototypeRefWrapperBase {
  public AssetRefEntityPrototype AssetPrototype;
  public abstract string ComponentTypeName { get; }
  public abstract EntityComponentBase ScenePrototype { get; }
}

public abstract class ComponentPrototypeRefWrapperBase<T, U> : ComponentPrototypeRefWrapperBase where T : EntityComponentBase where U : unmanaged, IComponent {

  [LocalReference]
  [SerializeField]
  private T _scenePrototype = default;

  public override string ComponentTypeName => typeof(U).Name;
  public override EntityComponentBase ScenePrototype => _scenePrototype;
}

[Serializable]
public class ComponentPrototypeRefWrapper : ComponentPrototypeRefWrapperBase {

  [LocalReference]
  [SerializeField]
  private EntityComponentBase _scenePrototype = default;

  [SerializeField]
  private string _componentTypeName = default;

  public override string ComponentTypeName => _componentTypeName;
  public override EntityComponentBase ScenePrototype => _scenePrototype;
}