using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Quantum.Prototypes;
using UnityEngine;

namespace Quantum {
  public unsafe partial class EntityPrototypeConverter {

    public readonly global::EntityPrototype[] OrderedMapPrototypes;
    public readonly global::EntityPrototype AssetPrototype;
    public readonly MapData Map;

    public EntityPrototypeConverter(MapData map, global::EntityPrototype[] orderedMapPrototypes) {
      Map = map;
      OrderedMapPrototypes = orderedMapPrototypes;
      InitUser();
    }

    public EntityPrototypeConverter(global::EntityPrototype prototypeAsset) {
      AssetPrototype = prototypeAsset;
      InitUser();
    }

    partial void InitUser();

    public void Convert(global::EntityPrototype prototype, out MapEntityId result) {
      if (AssetPrototype != null) {
        result = AssetPrototype == prototype ? MapEntityId.Create(0) : MapEntityId.Invalid;
      } else {
        var index = Array.IndexOf(OrderedMapPrototypes, prototype);
        result = index >= 0 ? MapEntityId.Create(index) : MapEntityId.Invalid;
      }
    }

    public void Convert(EntityPrototypeRefWrapper prototype, out EntityPrototypeRef result) {
      var sceneReference = prototype.ScenePrototype;
      if (sceneReference != null && sceneReference.gameObject.scene.IsValid()) {
        Debug.Assert(Map != null);
        Debug.Assert(Map.gameObject.scene == sceneReference.gameObject.scene);

        var index = Array.IndexOf(OrderedMapPrototypes, sceneReference);
        if (index >= 0) {
          result = EntityPrototypeRef.FromMasterAsset(Map.Asset.Settings, index);
        } else {
          result = EntityPrototypeRef.Invalid;
        }
      } else if ( prototype.AssetPrototype.Id.IsValid) {
        result = EntityPrototypeRef.FromPrototypeAsset(prototype.AssetPrototype);
      } else {
        result = default;
      }
    }

    public void Convert(ComponentPrototypeRefWrapperBase prototype, out ComponentPrototypeRef_Prototype result) { 

      if ( prototype == null ) {
        result = default;
        return;
      }

      Convert(new EntityPrototypeRefWrapper() {
        AssetPrototype = prototype.AssetPrototype,
        ScenePrototype = prototype.ScenePrototype?.GetComponent<global::EntityPrototype>()
      }, out var entityPrototypeRef);

      if (entityPrototypeRef.IsValid) {

        string componentTypeName = prototype.ComponentTypeName;
        if ( prototype.ScenePrototype ) {
          componentTypeName = prototype.ScenePrototype.ComponentType.Name;
        }

        result = new ComponentPrototypeRef_Prototype() {
          EntityPrototypeRef = entityPrototypeRef,
          ComponentTypeShortName = componentTypeName
        };
      } else {
        result = default;
      }
    }
  }
}
