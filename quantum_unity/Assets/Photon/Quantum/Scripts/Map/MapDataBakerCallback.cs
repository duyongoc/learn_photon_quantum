using System.Collections.Generic;

public abstract class MapDataBakerCallback {
  /// <summary>
  /// Is called in the beginning of map baking. Both signatures are called.
  /// </summary>
  /// <param name="data">The MapData object that is currently baked.</param>
  public abstract void OnBeforeBake(MapData data);
  public virtual void OnBeforeBake(MapData data, MapDataBaker.BuildTrigger buildTrigger, QuantumMapDataBakeFlags bakeFlags) { }

  /// <summary>
  /// Is called after map baking when colliders and prototypes have been baked and before navmesh baking.
  /// </summary>
  /// <param name="data"></param>
  public abstract void OnBake(MapData data);

  /// <summary>
  /// Is called before any navmeshes are generated or any bake data is collected.
  /// </summary>
  /// <param name="data">The MapData object that is currently baked.</param>
  public virtual void OnBeforeBakeNavMesh(MapData data) { }

  /// <summary>
  /// Is called during navmesh baking with the current list of bake data retreived from Unity navmeshes flagged for Quantum navmesh baking.
  /// Add new BakeData objects to the navMeshBakeData list.
  /// </summary>
  /// <param name="data">The MapData object that is currently baked.</param>
  /// <param name="navMeshBakeData">Current list of bake data to be baked</param>
  public virtual void OnCollectNavMeshBakeData(MapData data, List<MapNavMesh.BakeData> navMeshBakeData) { }

  /// <summary>
  /// Is called after navmesh baking before serializing them to assets.
  /// Add new NavMesh objects the navmeshes list.
  /// </summary>
  /// <param name="data">The MapData object that is currently baked.</param>
  /// <param name="navmeshes">Current list of baked navmeshes to be saved to assets.</param>
  public virtual void OnCollectNavMeshes(MapData data, List<Quantum.NavMesh> navmeshes) { }

  /// <summary>
  /// Is called after the navmesh generation has been completed.
  /// Navmeshes assets references are stored in data.Asset.Settings.NavMeshLinks.
  /// </summary>
  /// <param name="data">The MapData object that is currently baked.</param>
  public virtual void OnBakeNavMesh(MapData data) { }
}
