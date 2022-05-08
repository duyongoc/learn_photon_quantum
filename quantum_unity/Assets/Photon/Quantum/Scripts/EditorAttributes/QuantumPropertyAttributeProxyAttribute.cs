using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Quantum {
  public abstract class QuantumPropertyAttributeProxyAttribute : PropertyAttribute {
    public QuantumPropertyAttributeProxyAttribute(Quantum.Inspector.PropertyAttribute attribute) {
      Attribute = attribute;
    }

    public Inspector.PropertyAttribute Attribute { get; }
  }

#if UNITY_EDITOR
  public static class PropertyDrawerExtensions {
    public static T GetQuantumAttribute<T>(this UnityEditor.PropertyDrawer drawer) where T : Quantum.Inspector.PropertyAttribute {
      if (drawer.attribute is QuantumPropertyAttributeProxyAttribute proxy) {
        return proxy.Attribute as T;
      }
      return default;
    }
  }
#endif
}
