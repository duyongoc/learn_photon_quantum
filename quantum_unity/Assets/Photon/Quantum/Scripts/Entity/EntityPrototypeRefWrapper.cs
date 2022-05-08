using System;
using Quantum;
using UnityEngine;

[Serializable]
public struct EntityPrototypeRefWrapper {
  [Quantum.LocalReference]
  public EntityPrototype ScenePrototype;
  public AssetRefEntityPrototype AssetPrototype;
}
