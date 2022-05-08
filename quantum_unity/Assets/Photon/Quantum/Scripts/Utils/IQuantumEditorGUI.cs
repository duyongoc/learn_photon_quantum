#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

namespace Quantum {
  public interface IQuantumEditorGUI {
#if UNITY_EDITOR
    bool Inspector(SerializedProperty prop, GUIContent label = null, string[] filters = null, bool skipRoot = true, bool drawScript = false, QuantumEditorGUIPropertyCallback callback = null);
    bool PropertyField(SerializedProperty property, GUIContent label, bool includeChildren, params GUILayoutOption[] options);
    void MultiTypeObjectField(SerializedProperty prop, GUIContent label, System.Type[] types, params GUILayoutOption[] options);
#endif
  }

#if UNITY_EDITOR
  public static class IQuantumEditorGUIExtensions {
    public static bool Inspector(this IQuantumEditorGUI gui, SerializedObject obj, string[] filters = null, QuantumEditorGUIPropertyCallback callback = null, bool drawScript = true) {
      return gui.Inspector(obj.GetIterator(), filters: filters, skipRoot: true, callback: callback, drawScript: drawScript);
    }

    public static bool Inspector(this IQuantumEditorGUI gui, SerializedObject obj, string propertyPath, string[] filters = null, bool skipRoot = true, QuantumEditorGUIPropertyCallback callback = null, bool drawScript = false) {
      return gui.Inspector(obj.FindPropertyOrThrow(propertyPath), filters: filters, skipRoot: skipRoot, callback: callback, drawScript: drawScript);
    }

    public static bool PropertyField(this IQuantumEditorGUI gui, SerializedProperty property, params GUILayoutOption[] options) {
      return gui.PropertyField(property, null, false, options);
    }

    public static bool PropertyField(this IQuantumEditorGUI gui, SerializedProperty property, GUIContent label, params GUILayoutOption[] options) {
      return gui.PropertyField(property, label, false, options);
    }

    public static void MultiTypeObjectField(this IQuantumEditorGUI gui, SerializedProperty prop, GUIContent label, params System.Type[] types) {
      gui.MultiTypeObjectField(prop, label, types);
    }
  }

  public delegate bool QuantumEditorGUIPropertyCallback(SerializedProperty property, System.Reflection.FieldInfo field, System.Type fieldType);
#endif
}

namespace Quantum.Editor {
  // an empty namespace to help with usings
}