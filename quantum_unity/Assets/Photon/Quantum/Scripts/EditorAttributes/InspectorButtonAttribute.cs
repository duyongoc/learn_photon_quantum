using System;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
using System.Reflection;
#endif

namespace Quantum {
  [AttributeUsage(AttributeTargets.Field)]
  public class InspectorButtonAttribute : PropertyAttribute {
    public string Method;
    public string Label;
    public bool IsToggleable;

    public InspectorButtonAttribute(string method) {
      Method = method;
      Label = method;
    }

    public InspectorButtonAttribute(string method, string label) {
      Method = method;
      Label = label;
    }

    public InspectorButtonAttribute(string method, bool isToggleable) {
      Method = method;
      IsToggleable = isToggleable;
    }

    public InspectorButtonAttribute(string method, string label, bool isToggleable) {
      Method = method;
      Label = label;
      IsToggleable = isToggleable;
    }
  }
}

#if UNITY_EDITOR

namespace Quantum.Editor {
  [CustomPropertyDrawer(typeof(InspectorButtonAttribute))]
  public class InspectorButtonDrawer : PropertyDrawer {

    public override void OnGUI(Rect position, SerializedProperty prop, GUIContent label) {

      var inspectorButtonAttribute = (InspectorButtonAttribute)attribute;
      var eventOwnerType = prop.serializedObject.targetObject.GetType();
      var method = eventOwnerType.GetMethod(inspectorButtonAttribute.Method, BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
      if (method != null) {
        bool guiEnabled = GUI.enabled;
        if (inspectorButtonAttribute.IsToggleable)
          GUI.enabled = prop.boolValue;
        if (GUI.Button(position, inspectorButtonAttribute.Label)) {
          method.Invoke(prop.serializedObject.targetObject, null);
        }
        GUI.enabled = guiEnabled;
      }
    }
  }
}

#endif