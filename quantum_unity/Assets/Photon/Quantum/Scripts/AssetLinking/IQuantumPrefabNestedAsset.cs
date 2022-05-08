/// <summary>
/// This interface has an implicit requirement:
/// - the implementation should implement this interface through IQuantumPrefabNestedAsset&lt;T&gt; interface
/// - the implementation needs to be a sub class of AssetBase
/// - the implementation needs to have a serializable property Parent
/// </summary>
/// <seealso cref="EntityViewAsset"/>
/// <seealso cref="EntityPrototypeAsset"/>
public interface IQuantumPrefabNestedAsset {
  UnityEngine.Component Parent {
    get;
  }
}

public interface IQuantumPrefabNestedAsset<THostingComponent> : IQuantumPrefabNestedAsset where THostingComponent : UnityEngine.Component {
}