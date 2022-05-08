using System;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
using System.Reflection;
#endif

namespace Quantum {
  [AttributeUsage(AttributeTargets.Field)]
  [Obsolete("Use Quantum.Inspector.EnumFlagsAttribute instead")]
  public class EnumFlagsAttribute : PropertyAttribute {
    public string tooltip { get; set; } = "";
  }
}

#if UNITY_EDITOR

namespace Quantum.Editor {
#pragma warning disable CS0618 // Type or member is obsolete
  [CustomPropertyDrawer(typeof(EnumFlagsAttribute))]
#pragma warning restore CS0618 // Type or member is obsolete
  [CustomPropertyDrawer(typeof(ProxyAttribute))]
  public class EnumFlagsDrawer : PropertyDrawer {

    private class ProxyAttribute : QuantumPropertyAttributeProxyAttribute {
      public ProxyAttribute(Quantum.Inspector.EnumFlagsAttribute attribute) : base(attribute) { }
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
      
      var enumType = fieldInfo.FieldType.HasElementType ? fieldInfo.FieldType.GetElementType() : fieldInfo.FieldType;

      Debug.Assert(enumType.IsEnum);
      var oldValue = (Enum)Enum.ToObject(enumType, property.intValue);

      EditorGUI.BeginProperty(position, label, property);
      EditorGUI.BeginChangeCheck();

      if ( string.IsNullOrEmpty(label.tooltip) ) {
#pragma warning disable CS0618 // Type or member is obsolete
        if (this.attribute is EnumFlagsAttribute enumAttribute) {
#pragma warning restore CS0618 // Type or member is obsolete
          label.tooltip = enumAttribute.tooltip;
        } else {
          label.tooltip = this.GetQuantumAttribute<Quantum.Inspector.EnumFlagsAttribute>().Tooltip;
        }

      }
      
      var newValue = EditorGUI.EnumFlagsField(position, label, oldValue);
      if (EditorGUI.EndChangeCheck()) {
        property.intValue = Convert.ToInt32(Convert.ChangeType(newValue, enumType));
      }

      EditorGUI.EndProperty();
    }
  }
}

#endif