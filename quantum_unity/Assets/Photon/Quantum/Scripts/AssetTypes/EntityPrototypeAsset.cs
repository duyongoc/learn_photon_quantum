using System.Collections.Generic;
using Quantum;
using UnityEngine;

public class EntityPrototypeAsset : AssetBase, IQuantumPrefabNestedAsset<EntityPrototype> {

  private class Visitor : Quantum.ComponentPrototypeVisitor {
    protected override void VisitFallback(ComponentPrototype prototype) {
      prototypeBuffer.Add(prototype);
    }
  }

  protected static readonly List<ComponentPrototype> prototypeBuffer = new List<ComponentPrototype>();
  private static readonly List<EntityComponentBase> behaviourBuffer = new List<EntityComponentBase>();
  private static readonly Visitor cache = new Visitor();

  public Quantum.EntityPrototype Settings;
  public override Quantum.AssetObject AssetObject => Settings;

  public EntityPrototype Parent;

  Component IQuantumPrefabNestedAsset.Parent => Parent;

  public override void Loaded() {
    Debug.Assert(Parent != null);
    base.Loaded();
  }

  public override void PrepareAsset() {
    base.PrepareAsset();

    if (Parent == null)
      return;

    try {
      // get built-ins first
      Parent.PreSerialize();
      Parent.SerializeImplicitComponents(cache, out var view);

      if ( view ) {
        // implicit view, check if there's an asset
        // TODO: there must be a better way to do this; we basically need to point to the other
        // nested asset without storing a reference in the behaviour
        const string expectedSuffix = nameof(EntityPrototype);
        if (!Settings.Path.EndsWith(expectedSuffix)) {
          Log.Error("Prototype assets' names are expected to end with: {0}", expectedSuffix);
        } else {
          var viewPath = Settings.Path.Substring(0, Settings.Path.Length - expectedSuffix.Length) + nameof(Quantum.EntityView);
          var viewGuid = UnityDB.GetAssetGuid(viewPath);
          if (!viewGuid.IsValid) {
            Log.Error("Expected to find a prefab asset at: {0}", viewPath);
          } else {
            prototypeBuffer.Add(new Quantum.Prototypes.View_Prototype() {
              Current = new AssetRefEntityView() { Id = viewGuid }
            });
          }
        }
      }

      var converter = new EntityPrototypeConverter(Parent);

      // now get custom ones
      Parent.GetComponents(behaviourBuffer);
      {
        foreach (var component in behaviourBuffer) { 
          component.Refresh();
          prototypeBuffer.Add(component.CreatePrototype(converter));
        }
      }

      if (Settings.Container == null) {
        Settings.Container = new EntityPrototypeContainer();
      }

      // store
      Settings.Container.Components = prototypeBuffer.ToArray();
      Settings.Container.SetDirty();

    } finally {
      prototypeBuffer.Clear();
      behaviourBuffer.Clear();
    }
  }

  public override void Reset() {
    if (Settings == null) {
      Settings = new Quantum.EntityPrototype();
    }

    base.Reset();

    if (Parent != null) {
      PrepareAsset();
    }
  }
}


public static partial class EntityPrototypeAssetExts {
  public static EntityPrototypeAsset GetUnityAsset(this Quantum.EntityPrototype data) {
    return data == null ? null : UnityDB.FindAsset<EntityPrototypeAsset>(data);
  }
}
