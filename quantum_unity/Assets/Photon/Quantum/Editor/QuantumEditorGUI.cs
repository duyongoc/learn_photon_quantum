

#region quantum_unity/Assets/Photon/Quantum/Editor/EditorGUI/QuantumEditorGUI.Components.cs
namespace Quantum.Editor {

  using System;
  using System.Linq;
  using UnityEditor;
  using UnityEngine;

  partial class QuantumEditorGUI {
    public const float ThumbnailSpacing = 1.0f;
    public const float ThumbnailWidth = 32.0f;
    private const float ThumbnailMinHeight = 14.0f;

    private static readonly int ThumbnailFieldHash = "Thumbnail".GetHashCode();
    private static readonly GUIContent MissingComponentContent = new GUIContent("???");

    private static GUIStyle _thumbnailAcronymStyle;
    private static Texture2D _thumbnailBackground;
    private static GUIStyle _thumbnailImageStyle;

    public static Rect AssetThumbnailPrefix(Rect position, string assemblyQualifiedName, bool addSpacing = true) {
      if (QuantumEditorUtility.TryGetAssetType(assemblyQualifiedName, out var type)) {
        return AssetThumbnailPrefix(position, type, addSpacing);
      } else {
        // TODO: draw a placeholder
        return position.AddX(ThumbnailWidth + (addSpacing ? ThumbnailSpacing : 0));
      }
    }

    public static Rect AssetThumbnailPrefix(Rect position, Type componentType, bool addSpacing = true) {
      QuantumEditorUtility.GetAssetThumbnailData(componentType, out var label, out var color);
      return DrawThumbnail(position, addSpacing, label, color);
    }

    public static float CalcThumbnailsWidth(int count) {
      return count * ThumbnailWidth + Math.Max(0, count - 1) * ThumbnailSpacing;
    }

    public static Rect ComponentThumbnailPrefix(Rect position, string componentTypeName, bool addSpacing = true, bool assemblyQualified = false) {
      if (QuantumEditorUtility.TryGetComponentType(componentTypeName, out var type, assemblyQualified: assemblyQualified)) {
        return ComponentThumbnailPrefix(position, type, addSpacing);
      } else {
        return DrawThumbnail(position, addSpacing, MissingComponentContent, Color.red);
      }
    }

    public static Rect ComponentThumbnailPrefix(Rect position, Type componentType, bool addSpacing = true) {
      QuantumEditorUtility.GetComponentThumbnailData(componentType, out var label, out var color);
      return DrawThumbnail(position, addSpacing, label, color);
    }
    public static void ShowComponentTypePicker(Rect activatorRect, Type selectedType, Action<Type> selected, Predicate<Type> filter = null, Action clear = null) {
      var types = QuantumEditorUtility.ComponentTypes;
      if (filter != null) {
        types = types.Where(x => filter(x));
      }

      var content = new ComponentPopupContent(types.ToArray(),
        x => x == selectedType,
        (x, b) => {
          if (b) {
            selected(x);
          } else if (clear != null) {
            clear();
          }
        }
      ) {
        SingleMode = true,
        ShowClear = clear != null,
      };

      PopupWindow.Show(activatorRect, content);
    }

    public static void ShowComponentTypesPicker(Rect activatorRect, ComponentTypeSetSelector selector, Predicate<Type> filter = null, Action onChange = null) {
      var types = QuantumEditorUtility.ComponentTypes;
      if (filter != null) {
        types = types.Where(x => filter(x));
      }

      var content = new ComponentPopupContent(types.ToArray(),
        t => selector.ComponentTypeNames.Contains(t.Name),
        (t, s) => {
          ArrayUtility.Remove(ref selector.ComponentTypeNames, t.Name);
          if (s) {
            ArrayUtility.Add(ref selector.ComponentTypeNames, t.Name);
          }
        }
      ) {
        ShowClear = true,
        OnChange = onChange,
      };

      PopupWindow.Show(activatorRect, content);
    }

    public static void ShowComponentTypesPicker(Rect activatorRect, Func<Type, bool> isSelected, Action<Type, bool> setSelected, Predicate<Type> filter = null) {
      var types = QuantumEditorUtility.ComponentTypes;
      if (filter != null) {
        types = types.Where(x => filter(x));
      }

      var content = new ComponentPopupContent(types.ToArray(), isSelected, setSelected) {
        ShowClear = true,
      };

      PopupWindow.Show(activatorRect, content);
    }

    private static Rect DrawThumbnail(Rect position, bool addSpacing, GUIContent label, Color color) {
      EnsureThumbnailStylesLoaded();

      var rect = position.SetWidth(ThumbnailWidth);
      var style = label.image ? _thumbnailImageStyle : _thumbnailAcronymStyle;

      var height = Mathf.Clamp(style.CalcHeight(label, ThumbnailWidth), ThumbnailMinHeight, EditorGUIUtility.singleLineHeight);

      if (position.height > height) {
        rect.height = height;
        rect.y += (position.height - height) / 2;
      }

      int controlID = GUIUtility.GetControlID(ThumbnailFieldHash, FocusType.Passive, rect);

      if (Event.current.type == EventType.Repaint) {
        var originalColor = GUI.backgroundColor;
        try {
          GUI.backgroundColor = color;
          style.Draw(rect, label, controlID);
        } finally {
          GUI.backgroundColor = originalColor;
        }
      }

      return position.AddX(ThumbnailWidth + (addSpacing ? ThumbnailSpacing : 0));
    }

    private static void EnsureThumbnailStylesLoaded() {
      if (_thumbnailBackground == null) {
        ReloadThumbnailStyles();
      }
    }

    private static void ReloadThumbnailStyles() {
      byte[] data = {
        0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a, 0x00, 0x00, 0x00, 0x0d,
        0x49, 0x48, 0x44, 0x52, 0x00, 0x00, 0x00, 0x14, 0x00, 0x00, 0x00, 0x14,
        0x08, 0x06, 0x00, 0x00, 0x00, 0x8d, 0x89, 0x1d, 0x0d, 0x00, 0x00, 0x00,
        0x01, 0x73, 0x52, 0x47, 0x42, 0x00, 0xae, 0xce, 0x1c, 0xe9, 0x00, 0x00,
        0x00, 0x04, 0x67, 0x41, 0x4d, 0x41, 0x00, 0x00, 0xb1, 0x8f, 0x0b, 0xfc,
        0x61, 0x05, 0x00, 0x00, 0x00, 0x09, 0x70, 0x48, 0x59, 0x73, 0x00, 0x00,
        0x0e, 0xc3, 0x00, 0x00, 0x0e, 0xc3, 0x01, 0xc7, 0x6f, 0xa8, 0x64, 0x00,
        0x00, 0x00, 0xf2, 0x49, 0x44, 0x41, 0x54, 0x38, 0x4f, 0xed, 0x95, 0x31,
        0x0a, 0x83, 0x30, 0x14, 0x86, 0x63, 0x11, 0x74, 0x50, 0x74, 0x71, 0xf1,
        0x34, 0x01, 0x57, 0x6f, 0xe8, 0xe0, 0xd0, 0xa5, 0x07, 0x10, 0x0a, 0xbd,
        0x40, 0x0f, 0xe2, 0xa8, 0x9b, 0xee, 0xf6, 0x7d, 0x69, 0x4a, 0xa5, 0xd2,
        0x2a, 0xa6, 0x4b, 0xa1, 0x1f, 0x04, 0x5e, 0xc2, 0xff, 0xbe, 0x68, 0x90,
        0xa8, 0x5e, 0xd0, 0x69, 0x9a, 0x9e, 0xc3, 0x30, 0x1c, 0xa4, 0x9e, 0x3e,
        0x0d, 0x32, 0x64, 0xa5, 0xd6, 0x32, 0x16, 0xf8, 0x51, 0x14, 0x1d, 0xb3,
        0x2c, 0x1b, 0xab, 0xaa, 0x9a, 0xda, 0xb6, 0x9d, 0xd6, 0x20, 0x43, 0x96,
        0x1e, 0x7a, 0x71, 0xdc, 0x55, 0x02, 0x0b, 0x45, 0x51, 0x0c, 0x82, 0x8d,
        0x6f, 0x87, 0x1e, 0x7a, 0xad, 0xd4, 0xa0, 0xd9, 0x65, 0x8f, 0xec, 0x01,
        0xbd, 0x38, 0x70, 0x29, 0xce, 0x81, 0x47, 0x77, 0x05, 0x87, 0x39, 0x53,
        0x0e, 0x77, 0xcb, 0x99, 0xad, 0x81, 0x03, 0x97, 0x27, 0x8f, 0xc9, 0xdc,
        0xbc, 0xbb, 0x2b, 0x9e, 0xe7, 0xa9, 0x83, 0xad, 0xbf, 0xc6, 0x5f, 0xe8,
        0xce, 0x0f, 0x08, 0xe5, 0x63, 0x1c, 0xfb, 0xbe, 0xb7, 0xd3, 0xfd, 0xe0,
        0xc0, 0x75, 0x08, 0x82, 0xe0, 0xda, 0x34, 0x8d, 0x5d, 0xde, 0x0f, 0x0e,
        0x5c, 0xd4, 0x3a, 0xcf, 0x73, 0xe7, 0xcb, 0x01, 0x07, 0x2e, 0x84, 0x2a,
        0x8e, 0xe3, 0x53, 0x59, 0x96, 0xbb, 0xa4, 0xf4, 0xd0, 0x8b, 0xc3, 0xc8,
        0x2c, 0x3e, 0x0b, 0xec, 0x52, 0xd7, 0xf5, 0xd4, 0x75, 0x9d, 0x8d, 0xbf,
        0x87, 0x0c, 0x59, 0x7a, 0xac, 0xec, 0x79, 0xc1, 0xce, 0xd0, 0x49, 0x92,
        0x5c, 0xb8, 0x35, 0xa4, 0x5e, 0x5c, 0xfb, 0xf3, 0x41, 0x86, 0xac, 0xd4,
        0xb3, 0x5f, 0x80, 0x52, 0x37, 0xfd, 0x56, 0x1b, 0x09, 0x40, 0x56, 0xe4,
        0x85, 0x00, 0x00, 0x00, 0x00, 0x49, 0x45, 0x4e, 0x44, 0xae, 0x42, 0x60,
        0x82
      };

      var texture = new Texture2D(2, 2, TextureFormat.ARGB32, false);
      if (!texture.LoadImage(data)) {
        throw new InvalidOperationException();
      }

      _thumbnailBackground = texture;

      _thumbnailAcronymStyle = new GUIStyle() {
        normal = new GUIStyleState { background = _thumbnailBackground, textColor = Color.white },
        border = new RectOffset(6, 6, 6, 6),
        padding = new RectOffset(2, 1, 1, 1),
        imagePosition = ImagePosition.TextOnly,
        alignment = TextAnchor.MiddleCenter,
        clipping = TextClipping.Clip,
        wordWrap = true,
        stretchWidth = false,
        fontSize = 8,
        fontStyle = FontStyle.Bold,
        fixedWidth = ThumbnailWidth,
      };

      _thumbnailImageStyle = new GUIStyle() {
        imagePosition = ImagePosition.ImageOnly,
        alignment = TextAnchor.MiddleCenter,
        clipping = TextClipping.Clip,
        wordWrap = true,
        stretchWidth = false,
        fontSize = 8,
        fontStyle = FontStyle.Bold,
        fixedWidth = ThumbnailWidth,
        fixedHeight = 18,
      };
    }

    private class ComponentPopupContent : PopupWindowContent {
      private const float ScrollbarWidth = 25.0f;
      private const int ThumbnailSpacing = 2;
      private const float ToggleWidth = 16.0f;

      private readonly RectOffset marginOverride = new RectOffset(4, 2, 0, 0);

      private GUIStyle _compactLabel;
      private GUIStyle _compactRadioButton;

      private Func<Type, bool> _isSelected;
      private GUIContent[] _prettyNames;
      private Vector2 _scrollPos;
      private Action<Type, bool> _setSelected;
      private Type[] _types;
      public ComponentPopupContent(Type[] types, Func<Type, bool> isSelected, Action<Type, bool> setSelected) {
        _types = types;
        _isSelected = isSelected;
        _setSelected = setSelected;
        _prettyNames = types.Select(x => QuantumEditorUtility.GetComponentDisplayName(x)).ToArray();

        _compactRadioButton = new GUIStyle(EditorStyles.radioButton) {
          fixedHeight = EditorGUIUtility.singleLineHeight,
          margin = marginOverride,
          contentOffset = new Vector2(ThumbnailWidth + 2 * ThumbnailSpacing, 0),
        };

        _compactLabel = new GUIStyle(EditorStyles.label) {
          fixedHeight = EditorGUIUtility.singleLineHeight,
          margin = marginOverride,
          contentOffset = new Vector2(ThumbnailWidth + 2 * ThumbnailSpacing, 0)
        };
      }

      public Action OnChange { get; set; }
      public bool ShowClear { get; set; }
      public bool SingleMode { get; set; }

      public override Vector2 GetWindowSize() {
        var perfectWidth = _prettyNames.Max(x => EditorStyles.label.CalcSize(x).x) + ThumbnailWidth + (2 * ThumbnailSpacing) + ScrollbarWidth + marginOverride.horizontal;

        // ignoring vertical spacing here because we're overriding margins
        var perfectHeight = _prettyNames.Length * (EditorGUIUtility.singleLineHeight + marginOverride.vertical);
        if (ShowClear) {
          perfectHeight += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
        }

        perfectHeight += 10;
        perfectWidth += 10;

        return new Vector2(Mathf.Clamp(perfectWidth, 200, Screen.width), Mathf.Clamp(perfectHeight, 200, Screen.height));
      }

      public override void OnGUI(Rect rect) {
        EditorGUI.BeginChangeCheck();
        try {
          using (new GUI.GroupScope(rect)) {
            using (var scroll = new GUILayout.ScrollViewScope(_scrollPos)) {
              _scrollPos = scroll.scrollPosition;

              int firstSelected = Array.FindIndex(_types, x => _isSelected(x));

              for (int i = 0; i < _prettyNames.Length; ++i) {
                var label = _prettyNames[i];
                var type = _types[i];
                bool wasSelected = SingleMode ? (i == firstSelected) : _isSelected(type);

                Rect toggleRect = EditorGUILayout.GetControlRect(true, EditorGUIUtility.singleLineHeight, _compactLabel);

                EditorGUI.BeginChangeCheck();

                bool toggle;
                if (SingleMode) {
                  using (_compactRadioButton.FontStyleScope(bold: wasSelected)) {
                    toggle = GUI.Toggle(toggleRect, wasSelected, label, _compactRadioButton);
                  }
                } else {
                  using(_compactLabel.FontStyleScope(bold: wasSelected)) {
                    toggle = EditorGUI.ToggleLeft(toggleRect, label, wasSelected, _compactLabel);
                  }
                }

                ComponentThumbnailPrefix(toggleRect.AddX(ToggleWidth + ThumbnailSpacing), type);

                if (EditorGUI.EndChangeCheck()) {
                  _setSelected(type, SingleMode ? true : toggle);
                }
              }
            }

            if (ShowClear) {
              using (new GUILayout.HorizontalScope()) {
                if (GUILayout.Button("Clear")) {
                  foreach (var t in _types) {
                    if (_isSelected(t)) {
                      _setSelected(t, false);
                    }
                  }
                }
              }
            }
          }
        } finally {
          if (EditorGUI.EndChangeCheck()) {
            OnChange?.Invoke();
            if (SingleMode) {
              editorWindow.Close();
            }
          }
        }
      }
    }
  }
}
#endregion

#region quantum_unity/Assets/Photon/Quantum/Editor/EditorGUI/QuantumEditorGUI.Inspector.cs
namespace Quantum.Editor {

  using System.Linq;
  using UnityEditor;
  using UnityEngine;

  public partial class QuantumEditorGUI {

    public delegate bool PropertyCallback(SerializedProperty property, System.Reflection.FieldInfo field, System.Type fieldType);

    public static bool Inspector(SerializedObject obj, string[] filters = null, PropertyCallback callback = null, bool drawScript = true) {
      return InspectorInternal(obj.GetIterator(), filters: filters, skipRoot: true, callback: callback, drawScript: drawScript);
    }

    public static bool Inspector(SerializedObject obj, string propertyPath, string[] filters = null, bool skipRoot = true, PropertyCallback callback = null, bool drawScript = false) {
      return InspectorInternal(obj.FindPropertyOrThrow(propertyPath), filters: filters, skipRoot: skipRoot, callback: callback, drawScript: drawScript);
    }

    public static bool Inspector(SerializedProperty prop, GUIContent label = null, string[] filters = null, bool skipRoot = true, bool drawScript = false, PropertyCallback callback = null) {
      return InspectorInternal(prop, label, filters, skipRoot, drawScript, callback);
    }

    internal static bool InspectorInternal(SerializedProperty prop, GUIContent label = null, string[] filters = null, bool skipRoot = true, bool drawScript = false, PropertyCallback callback = null) {
      int minDepth = prop.depth;
      prop = prop.Copy();

      bool enterChildren = false;

      EditorGUI.BeginChangeCheck();
      int indentLevel = EditorGUI.indentLevel;
      int referenceIndentLevel = indentLevel;
      int readOnlyDepth = int.MaxValue;

      try {
        do {
          var guiContent = label;
          label = null;

          if (skipRoot) {
            enterChildren = true;
            skipRoot = false;
            referenceIndentLevel -= 1;
            continue;
          }

          if (filters?.Any(f => prop.propertyPath.StartsWith(f)) == true) {
            enterChildren = false;
            continue;
          }

          bool makeReadOnly = false;
          if (prop.depth > readOnlyDepth) {
            makeReadOnly = true;
          } else {
            readOnlyDepth = int.MaxValue;
          }

          using (new EditorGUI.DisabledScope(makeReadOnly)) {
            EditorGUI.indentLevel = referenceIndentLevel + prop.depth - minDepth;
            if (callback != null) {
              var field = UnityInternal.ScriptAttributeUtility.GetFieldInfoFromProperty(prop, out var fieldType);
              var propCopy = prop.Copy();
              var callbackResult = callback(propCopy, field, fieldType);
              if (callbackResult == false) {
                // will go ahead
              } else {
                continue;
              }
            }

            enterChildren = false;
            if (prop.propertyPath == ScriptPropertyName) {
              if (drawScript) {
                using (new EditorGUI.DisabledScope(true)) {
                  EditorGUILayout.PropertyField(prop);
                }
              }
            } else {
              var result = PropertyFieldLayoutInternal(prop, guiContent, false);
              if ((result & PropertyFieldResult.InspectChildren) == PropertyFieldResult.InspectChildren) {
                enterChildren = true;
              }
              if ((result & PropertyFieldResult.ReadOnly) == PropertyFieldResult.ReadOnly) {
                if (prop.depth < readOnlyDepth) {
                  readOnlyDepth = prop.depth;
                }
              }
            }
          }
        } while (prop.NextVisible(enterChildren) && prop.depth > minDepth);
      } finally {
        EditorGUI.indentLevel = indentLevel;
      }

      if (EditorGUI.EndChangeCheck()) {
        prop.serializedObject.ApplyModifiedProperties();
        return true;
      }
      return false;
    }
  }
}
#endregion

#region quantum_unity/Assets/Photon/Quantum/Editor/EditorGUI/QuantumEditorGUI.Legacy.cs
public partial class CustomEditorsHelper : Quantum.Editor.QuantumEditorGUI {

  [System.Obsolete]
  public class State { }

  [System.Obsolete("Use QuantumGUI.LayoutInspector instead")]
  public static bool DrawDefaultInspector(UnityEditor.SerializedObject obj, string path, string[] filter, bool showFoldout = true, PropertyCallback callback = null, State state = null) {
    return Inspector(obj, path, filters: filter, skipRoot: !showFoldout, callback: callback);
  }

  [System.Obsolete("Use QuantumGUI.LayoutInspector instead")]
  public static bool DrawDefaultInspector(UnityEditor.SerializedObject obj, string path, string[] filter, bool showFoldout, ref bool foldout, PropertyCallback callback = null, State state = null) {
    var prop = Quantum.SerializedObjectExtensions.FindPropertyOrThrow(obj, path);
    prop.isExpanded = foldout;
    var result = Inspector(obj, path, filters: filter, skipRoot: !showFoldout, callback: callback);
    foldout = prop.isExpanded;
    return result;
  }

  [System.Obsolete("Use InspectorLayout instead")]
  public static bool DrawDefaultInspector(UnityEditor.SerializedObject obj, string[] filter = null, PropertyCallback callback = null, State state = null) {
    return Inspector(obj, filter, callback);
  }

  [System.Obsolete("Use InspectorLayout instead")]
  public static bool DrawDefaultInspector(UnityEditor.SerializedProperty root, string[] filter = null, bool skipRoot = true, PropertyCallback callback = null, UnityEngine.GUIContent label = null, State state = null) {
    return Inspector(root, filters: filter, callback: callback, skipRoot: skipRoot, label: label);
  }

  [System.Obsolete("Use Header instead")]
  public static void DrawHeadline(string header) {
    Header(header);
  }

  [System.Obsolete("Use ScriptPropertyField instead")]
  public static void DrawScript(UnityEngine.Object obj) {
    ScriptPropertyField(obj);
  }

  [System.Obsolete("Use PrefixIcon instead")]
  internal static UnityEngine.Rect DrawIconPrefix(UnityEngine.Rect rect, string tooltip, UnityEditor.MessageType messageType) {
    return PrefixIcon(rect, tooltip, messageType);
  }
}
#endregion

#region quantum_unity/Assets/Photon/Quantum/Editor/EditorGUI/QuantumEditorGUI.Menu.cs
namespace Quantum.Editor {

  using System;
  using System.Collections.Generic;
  using System.Linq;
  using UnityEditor;
  using UnityEngine;

  public partial class QuantumEditorGUI {
    public static MenuBuilder<T1> BuildMenu<T1>() => new MenuBuilder<T1>();
    public static MenuBuilder<T1, T2> BuildMenu<T1, T2>() => new MenuBuilder<T1, T2>();
    public static MenuBuilder<T1, T2, T3> BuildMenu<T1, T2, T3>() => new MenuBuilder<T1, T2, T3>();
    public static MenuBuilder<T1, T2, T3, T4> BuildMenu<T1, T2, T3, T4>() => new MenuBuilder<T1, T2, T3, T4>();

    public abstract class MenuBuilderBase<T, TAction, TPredicate>
      where TAction : System.Delegate
      where TPredicate : System.Delegate {

      public delegate void GenerateCallback(Action<string, TAction, TPredicate> addItem);

      private List<GUIContent> _labels = new List<GUIContent>();
      private List<TAction> _handlers = new List<TAction>();
      private List<TPredicate> _filters = new List<TPredicate>();
      private List<GenerateCallback> _generators = new List<GenerateCallback>();

      public T AddItem(string content, TAction onClick, TPredicate predicate = null) =>
        AddItem(new GUIContent(content), onClick, predicate);

      public T AddItem(GUIContent content, TAction onClick, TPredicate predicate = null) {
        _labels.Add(content);
        _handlers.Add(onClick);
        _filters.Add(predicate);
        return (T)(object)this;
      }

      public T AddGenerator(GenerateCallback p) {
        _generators.Add(p);
        return (T)(object)this;
      }

      public void Build(out GUIContent[] labels, out TAction[] actions, out TPredicate[] predicates) {
        var allLabels = _labels.ToList();
        var allHandlers = _handlers.ToList();
        var allPredicates = _filters.ToList();

        foreach (var generator in _generators) {
          generator((name, handler, predicate) => {
            allLabels.Add(new GUIContent(name));
            allHandlers.Add(handler);
            allPredicates.Add(predicate);
          });
        }

        labels = allLabels.ToArray();
        actions = allHandlers.ToArray();
        predicates = allPredicates.ToArray();
      }
    }

    public class MenuBuilder : MenuBuilderBase<MenuBuilder, Action, Func<bool>> {

      static public implicit operator Action<Rect>(MenuBuilder builder) {
        return (Rect rect) => {
          builder.Build(out var labels, out var handlers, out var filters);
          EditorUtility.DisplayCustomMenu(rect,
             labels,
             i => filters[i]?.Invoke() ?? true,
             -1,
             (ud, opts, selected) => {
               handlers[selected]();
             },
             null
           );
        };
      }
    }

    public class MenuBuilder<T1> : MenuBuilderBase<MenuBuilder<T1>, Action<T1>, Func<T1, bool>> {

      static public implicit operator Action<Rect, T1>(MenuBuilder<T1> builder) {
        return (Rect rect, T1 t1) => {
          builder.Build(out var labels, out var handlers, out var filters);
          EditorUtility.DisplayCustomMenu(rect,
             labels,
             i => filters[i]?.Invoke(t1) ?? true,
             -1,
             (ud, opts, selected) => {
               handlers[selected](t1);
             },
             null
           );
        };
      }
    }

    public class MenuBuilder<T1, T2> : MenuBuilderBase<MenuBuilder<T1, T2>, Action<T1, T2>, Func<T1, T2, bool>> {

      static public implicit operator Action<Rect, T1, T2>(MenuBuilder<T1, T2> builder) {
        return (Rect rect, T1 t1, T2 t2) => {
          builder.Build(out var labels, out var handlers, out var filters);
          EditorUtility.DisplayCustomMenu(rect,
             labels,
             i => filters[i]?.Invoke(t1, t2) ?? true,
             -1,
             (ud, opts, selected) => {
               handlers[selected](t1, t2);
             },
             null
           );
        };
      }
    }

    public class MenuBuilder<T1, T2, T3> : MenuBuilderBase<MenuBuilder<T1, T2, T3>, Action<T1, T2, T3>, Func<T1, T2, T3, bool>> {

      static public implicit operator Action<Rect, T1, T2, T3>(MenuBuilder<T1, T2, T3> builder) {
        return (Rect rect, T1 t1, T2 t2, T3 t3) => {
          builder.Build(out var labels, out var handlers, out var filters);
          EditorUtility.DisplayCustomMenu(rect,
             labels,
             i => filters[i]?.Invoke(t1, t2, t3) ?? true,
             -1,
             (ud, opts, selected) => {
               handlers[selected](t1, t2, t3);
             },
             null
           );
        };
      }
    }

    public class MenuBuilder<T1, T2, T3, T4> : MenuBuilderBase<MenuBuilder<T1, T2, T3, T4>, Action<T1, T2, T3, T4>, Func<T1, T2, T3, T4, bool>> {

      static public implicit operator Action<Rect, T1, T2, T3, T4>(MenuBuilder<T1, T2, T3, T4> builder) {
        return (Rect rect, T1 t1, T2 t2, T3 t3, T4 t4) => {
          builder.Build(out var labels, out var handlers, out var filters);
          EditorUtility.DisplayCustomMenu(rect,
             labels,
             i => filters[i]?.Invoke(t1, t2, t3, t4) ?? true,
             -1,
             (ud, opts, selected) => {
               handlers[selected](t1, t2, t3, t4);
             },
             null
           );
        };
      }
    }

    public static bool HandleContextMenu(Rect rect, out Rect menuRect, bool showButton = true) {
      // Options button
      const float optionsButtonWidth = 16f;
      const float optionsButtonHeight = 16f;
      const float margin = 4f;

      if (showButton) {

        Rect buttonRect = new Rect(rect.xMax - optionsButtonWidth - margin, rect.y + (rect.height - optionsButtonHeight) * 0.5f, optionsButtonWidth, rect.height);

        if (Event.current.type == EventType.Repaint) {
          UnityInternal.Styles.OptionsButtonStyle.Draw(buttonRect, false, false, false, false);
        }

        if (EditorGUI.DropdownButton(buttonRect, GUIContent.none, FocusType.Passive, GUIStyle.none)) {
          menuRect = buttonRect;
          return true;
        }
      }

      if (Event.current.type == EventType.ContextClick) {
        if (rect.Contains(Event.current.mousePosition)) {
          menuRect = new Rect(Event.current.mousePosition, Vector2.one);
          return true;
        }
      }

      menuRect = default;
      return false;
    }

    public static bool HandleContextMenu(Rect rect, System.Action<Rect> menu, bool showButton = true) {
      if (HandleContextMenu(rect, out var menuRect, showButton: showButton)) {
        menu(menuRect);
        return true;
      }
      return false;
    }

    public static bool HandleContextMenu<T>(Rect rect, T item, System.Action<Rect, T> menu, bool showButton = true) {
      if (HandleContextMenu(rect, out var menuRect, showButton: showButton)) {
        menu(menuRect, item);
        return true;
      }
      return false;
    }
  }
}
#endregion

#region quantum_unity/Assets/Photon/Quantum/Editor/EditorGUI/QuantumEditorGUI.Proxy.cs
namespace Quantum.Editor {
  using System;
  using System.Linq;
  using UnityEditor;
  using UnityEngine;

  public partial class QuantumEditorGUI {

    public static IQuantumEditorGUI ProxyInstance = new Proxy();

    private sealed class Proxy : IQuantumEditorGUI {
      bool IQuantumEditorGUI.Inspector(SerializedProperty prop, GUIContent label, string[] filters, bool skipRoot, bool drawScript, QuantumEditorGUIPropertyCallback callback) {
        return Inspector(prop, label: label, filters: filters, skipRoot: skipRoot, drawScript: drawScript, callback: (callback != null ? new PropertyCallback(callback) : null));
      }

      void IQuantumEditorGUI.MultiTypeObjectField(SerializedProperty prop, GUIContent label, Type[] types, params GUILayoutOption[] options) {
        MultiTypeObjectField(prop, label, types, options);
      }

      bool IQuantumEditorGUI.PropertyField(SerializedProperty property, GUIContent label, bool includeChildren, params GUILayoutOption[] options) {
        return PropertyField(property, label, includeChildren, options);
      }
    }
  }
}
#endregion

#region quantum_unity/Assets/Photon/Quantum/Editor/EditorGUI/QuantumEditorGUI.Scopes.cs
namespace Quantum.Editor {

  using System;
  using UnityEditor;
  using UnityEngine;

  public partial class QuantumEditorGUI {

    public sealed class CustomEditorScope : IDisposable {

      private SerializedObject serializedObject;
      public bool HadChanges { get; private set; }

      public CustomEditorScope(SerializedObject so) {
        serializedObject = so;
        EditorGUI.BeginChangeCheck();
        so.UpdateIfRequiredOrScript();
        ScriptPropertyField(so);
      }

      public void Dispose() {
        serializedObject.ApplyModifiedProperties();
        HadChanges = EditorGUI.EndChangeCheck();
      }
    }

    public sealed class BackgroundColorScope : GUI.Scope {
      private readonly Color value;

      public BackgroundColorScope(Color color) {
        value = GUI.backgroundColor;
        GUI.backgroundColor = color;
      }

      protected override void CloseScope() {
        GUI.backgroundColor = value;
      }
    }

    public sealed class ColorScope : GUI.Scope {
      private readonly Color value;

      public ColorScope(Color color) {
        value = GUI.color;
        GUI.color = color;
      }

      protected override void CloseScope() {
        GUI.color = value;
      }
    }

    public sealed class ContentColorScope : GUI.Scope {
      private readonly Color value;

      public ContentColorScope(Color color) {
        value = GUI.contentColor;
        GUI.contentColor = color;
      }

      protected override void CloseScope() {
        GUI.contentColor = value;
      }
    }

    public sealed class FieldWidthScope : GUI.Scope {
      private float value;

      public FieldWidthScope(float fieldWidth) {
        value = EditorGUIUtility.fieldWidth;
        EditorGUIUtility.fieldWidth = fieldWidth;
      }

      protected override void CloseScope() {
        EditorGUIUtility.fieldWidth = value;
      }
    }

    public sealed class HierarchyModeScope : GUI.Scope {
      private bool value;

      public HierarchyModeScope(bool value) {
        this.value = EditorGUIUtility.hierarchyMode;
        EditorGUIUtility.hierarchyMode = value;
      }

      protected override void CloseScope() {
        EditorGUIUtility.hierarchyMode = value;
      }
    }

    public sealed class IndentLevelScope : GUI.Scope {
      private readonly int value;

      public IndentLevelScope(int indentLevel) {
        value = EditorGUI.indentLevel;
        EditorGUI.indentLevel = indentLevel;
      }

      protected override void CloseScope() {
        EditorGUI.indentLevel = value;
      }
    }

    public sealed class LabelWidthScope : GUI.Scope {
      private float value;

      public LabelWidthScope(float labelWidth) {
        value = EditorGUIUtility.labelWidth;
        EditorGUIUtility.labelWidth = labelWidth;
      }

      protected override void CloseScope() {
        EditorGUIUtility.labelWidth = value;
      }
    }

    public sealed class ShowMixedValueScope : GUI.Scope {
      private bool value;

      public ShowMixedValueScope(bool show) {
        value = EditorGUI.showMixedValue;
        EditorGUI.showMixedValue = show;
      }

      protected override void CloseScope() {
        EditorGUI.showMixedValue = value;
      }
    }

    public sealed class PropertyScope : GUI.Scope {

      public PropertyScope(Rect position, GUIContent label, SerializedProperty property) {
        EditorGUI.BeginProperty(position, label, property);
      }

      protected override void CloseScope() {
        EditorGUI.EndProperty();
      }
    }

    public sealed class PropertyScopeWithPrefixLabel : GUI.Scope {
      private int indent;

      public PropertyScopeWithPrefixLabel(Rect position, GUIContent label, SerializedProperty property, out Rect indentedPosition) {
        EditorGUI.BeginProperty(position, label, property);
        indentedPosition = EditorGUI.PrefixLabel(position, label);
        indent = EditorGUI.indentLevel;
        EditorGUI.indentLevel = 0;
      }

      protected override void CloseScope() {
        EditorGUI.indentLevel = indent;
        EditorGUI.EndProperty();
      }
    }


    public static void BeginSection(string headline = null) {
      if (string.IsNullOrEmpty(headline)) {
        EditorGUILayout.Space();
      } else {
        Header(headline);
      }
    }

    public static void EndSection() {
    }


    public sealed class SectionScope : IDisposable {
      public SectionScope(string headline = null) {
        BeginSection(headline);
      }

      public void Dispose() {
      }
    }

    public static bool BeginBox(string headline = null, int indentLevel = 1, bool? foldout = null) {
      bool result = true;
      GUILayout.BeginVertical(EditorStyles.helpBox);
      if (!string.IsNullOrEmpty(headline)) {
        if (foldout.HasValue) {
          result = EditorGUILayout.Foldout(foldout.Value, headline);
        } else {
          EditorGUILayout.LabelField(headline, EditorStyles.boldLabel);
        }
      }
      EditorGUI.indentLevel += indentLevel;
      return result;
    }

    public static void EndBox(int indentLevel = 1) {
      EditorGUI.indentLevel -= indentLevel;
      GUILayout.EndVertical();
    }

    public sealed class BoxScope : IDisposable {
      private readonly SerializedObject _serializedObject;
      private readonly int _indentLevel;

#if !UNITY_2019_3_OR_NEWER
      private readonly Color _backgroundColor;
#endif

      public BoxScope(string headline = null, SerializedObject serializedObject = null, int indentLevel = 1, bool? foldout = null) {
        _indentLevel = indentLevel;
        _serializedObject = serializedObject;

#if !UNITY_2019_3_OR_NEWER
        _backgroundColor = GUI.backgroundColor;
        if (EditorGUIUtility.isProSkin) {
          GUI.backgroundColor = Color.grey;
        }
#endif

        IsFoldout = BeginBox(headline: headline, indentLevel: indentLevel, foldout: foldout);

        if (_serializedObject != null) {
          EditorGUI.BeginChangeCheck();
        }
      }

      public bool IsFoldout { get; private set; }

      public void Dispose() {
        if (_serializedObject != null && EditorGUI.EndChangeCheck()) {
          _serializedObject.ApplyModifiedProperties();
        }

        EndBox(indentLevel: _indentLevel);

#if !UNITY_2019_3_OR_NEWER
        GUI.backgroundColor = _backgroundColor;
#endif
      }
    }
  }
}
#endregion

#region quantum_unity/Assets/Photon/Quantum/Editor/EditorGUI/QuantumEditorGUI.SerializedProperty.cs
namespace Quantum.Editor {

  using System;
  using System.Collections.Generic;
  using System.Linq;
  using System.Reflection;
  using System.Text;
  using Quantum.Inspector;
  using UnityEditor;
  using UnityEditorInternal;
  using UnityEngine;

  public partial class QuantumEditorGUI {

    private const bool DefaultIncludeChildrenInGetPropertyHeight = true;
    private const bool DefaultIncludeChildrenInPropertyField = false;
    internal const string DictionaryKeyPropertyName = "Key";
    internal const string DictionaryValuePropertyName = "Value";
    private static readonly Lazy<ILookup<Type, Type>> s_attributeToUnityAttribute = new Lazy<ILookup<Type, Type>>(() => GenerateDecoratorEntries());
    private static readonly PropertyHandlerMeta s_defaultMeta = new PropertyHandlerMeta();
    private static readonly string DynamicCollectionWarning = $"Collection does not have [{GetPrettyAttributeTypeName(typeof(Core.FreeOnComponentRemovedAttribute))}] attribute and needs to be manually disposed in the simulation code to avoid memory leaks.";

    private static int s_LastInspectionTarget;
    private static int s_LastInspectorNumComponents;

    [Flags]
    private enum ArrayPropertyModifiers : int {
      None,
      HideSize            = (1 << 3) | (1 << 0),
      InlineFirstElement  = (1 << 3) | (1 << 1),
      Dictionary          = (1 << 3) | (1 << 2),
      Dynamic             = (1 << 4),
      ForceReorderable    = (1 << 5),
    }
    private static bool HasFlags(ArrayPropertyModifiers field, ArrayPropertyModifiers flags) {
      return ((int)field & (int)flags) == (int)flags;
    }

    [Flags]
    private enum PropertyFieldResult {
      None = 0,
      InspectChildren = 1,
      ReadOnly = 2
    }

    private enum PropertyVisibilityModifiers {
      None,
      Hidden,
      Disabled
    }

    public static float GetPropertyHeight(SerializedProperty property, bool includeChildren = DefaultIncludeChildrenInGetPropertyHeight) {
      return GetPropertyHeight(property, null, includeChildren);
    }

    public static float GetPropertyHeight(SerializedProperty property, GUIContent label, bool includeChildren = DefaultIncludeChildrenInGetPropertyHeight) {
      using (AcquireHandler(property, out var handler, out var qhandler)) {
        return GetPropertyHeightInternal(property, label, includeChildren, handler, qhandler);
      }
    }

    public static bool PropertyField(Rect position, SerializedProperty property, bool includeChildren = DefaultIncludeChildrenInPropertyField) {
      return PropertyField(position, property, null, includeChildren);
    }

    public static bool PropertyField(Rect position, SerializedProperty property, GUIContent label, bool includeChildren = DefaultIncludeChildrenInPropertyField) {
      using (AcquireHandler(property, out var handler, out var qhandler)) {
        return PropertyFieldInternal(position, property, label, includeChildren, handler, qhandler) == PropertyFieldResult.InspectChildren;
      }
    }

    public static bool PropertyField(SerializedProperty property, params GUILayoutOption[] options) {
      return PropertyField(property, null, DefaultIncludeChildrenInPropertyField, options);
    }

    public static bool PropertyField(SerializedProperty property, bool includeChildren, params GUILayoutOption[] options) {
      return PropertyField(property, null, includeChildren, options);
    }

    public static bool PropertyField(SerializedProperty property, GUIContent label, params GUILayoutOption[] options) {
      return PropertyField(property, label, DefaultIncludeChildrenInPropertyField, options);
    }

    public static bool PropertyField(SerializedProperty property, GUIContent label, bool includeChildren, params GUILayoutOption[] options) {
      return PropertyFieldLayoutInternal(property, label, includeChildren, options) == PropertyFieldResult.InspectChildren;
    }

    

    private static IDisposable AcquireHandler(SerializedProperty property, out UnityInternal.PropertyHandler handler, out PropertyHandlerMeta meta) {
      handler = UnityInternal.ScriptAttributeUtility.propertyHandlerCache.GetHandler(property);
      
      if (!handler) {
        handler = InjectQuantumPropertyAttributes(property);
      } else if (handler.HasPropertyDrawer<QuantumInspectorAttributeDrawer>()) {
        // need to make sure we've injected
        if (handler.decoratorDrawers?.OfType<PropertyHandlerMeta>().Any() != true) {
          handler = InjectQuantumPropertyAttributes(property, forceAddMeta: true);
        }
      }

      meta = handler.decoratorDrawers?.OfType<PropertyHandlerMeta>().SingleOrDefault() ?? s_defaultMeta;

      return meta;
    }

    private static void AddDecorator(ref UnityInternal.PropertyHandler handler, SerializedProperty property, DecoratorDrawer decorator) {
      if (handler == UnityInternal.ScriptAttributeUtility.sharedNullHandler) {
        // need a new one...
        var newHandler = UnityInternal.PropertyHandler.New();
        UnityInternal.ScriptAttributeUtility.propertyHandlerCache.SetHandler(property, newHandler);
        handler = newHandler;
      }

      if (handler.decoratorDrawers == null) {
        handler.decoratorDrawers = new List<DecoratorDrawer>();
      }
      handler.decoratorDrawers.Add(decorator);
    }

    private static void AddPropertyAttribute(ref UnityInternal.PropertyHandler handler, SerializedProperty property, UnityEngine.PropertyAttribute attribute, FieldInfo field, Type propertyType) {
      if (handler == UnityInternal.ScriptAttributeUtility.sharedNullHandler) {
        // need a new one...
        var newHandler = UnityInternal.PropertyHandler.New();
        UnityInternal.ScriptAttributeUtility.propertyHandlerCache.SetHandler(property, newHandler);
        handler = newHandler;
      }

      if (handler.supportsMultiplePropertyDrawers && handler.hasPropertyDrawer) {
        // attribute drawer needs to be added prior
        var drawerType = UnityInternal.ScriptAttributeUtility.GetDrawerTypeForType(attribute.GetType());
        if (typeof(PropertyDrawer).IsAssignableFrom(drawerType)) {
          // attribute drawers override property drawers
          handler.ClearPropertyDrawers();
        }
      }

      handler.HandleAttribute(property, attribute, field, propertyType);
    }

    private static ILookup<Type, Type> GenerateDecoratorEntries() {
      var allCustomPropertyAttributes = AppDomain.CurrentDomain.GetAssemblies()
        .Where(x => !x.IsDynamic)
        .SelectMany(x => {
          try {
            return x.GetTypes();
          } catch (Exception) {
            return Array.Empty<Type>();
          }
        })
        .Where(x => x.IsSubclassOf(typeof(PropertyDrawer)) || x.IsSubclassOf(typeof(DecoratorDrawer)))
        .Where(x => !x.IsAbstract && !x.IsGenericTypeDefinition)
        .SelectMany(x => x.GetCustomAttributesData())
        .Where(x => x.AttributeType == typeof(CustomPropertyDrawer))
        .ToList();

      var allProxyAttributes = allCustomPropertyAttributes
        .Select(x => (Type)x.ConstructorArguments[0].Value)
        .Where(x => !x.IsAbstract && x.IsSubclassOf(typeof(QuantumPropertyAttributeProxyAttribute)))
        .ToList();

      return allProxyAttributes.Select(unityAttribute =>
        new {
          unityAttribute,
          quantumAttribute = unityAttribute.GetConstructors()
            .Select(x => x.GetParameters().FirstOrDefault()?.ParameterType)
            .FirstOrDefault(x => x?.IsSubclassOf(typeof(Quantum.Inspector.PropertyAttribute)) == true)
        })
        .Where(x => x.quantumAttribute != null)
        .ToLookup(x => x.quantumAttribute, x => x.unityAttribute);
    }

    private static float GetPropertyHeightInternal(SerializedProperty property, GUIContent label, bool includeChildren, UnityInternal.PropertyHandler handler, PropertyHandlerMeta qhandler) {
      float height = 0.0f;

      // handle visibility modifiers
      if (qhandler.GetVisibilityModifiers(property) == PropertyVisibilityModifiers.Hidden) {
        return 0.0f;
      } else if (property.propertyType == SerializedPropertyType.ArraySize && qhandler.GetArraySizeModifiers(property, out _, out var modifiers) && modifiers == ArrayPropertyModifiers.HideSize) {
        return 0.0f;
      }

      if (!handler.isCurrentlyNested) {
        // handle base decorators
        if (handler.decoratorDrawers != null) {
          foreach (var decorator in handler.decoratorDrawers) {
            height += decorator.GetHeight();
          }
        }

        if (qhandler.TryGetOptionalState(property, out bool? optionalEnabled) && optionalEnabled != true) {
          height += GetSinglePropertyHeight(property, label);
          return height;
        }
      }

      // handle regular PropertyDrawer
      if (handler.currentPropertyDrawer != null) {
        var propertyDrawer = handler.currentPropertyDrawer;
        handler.nestingLevel++;
        try {
          height += UnityInternal.PropertyDrawer.GetPropertyHeightSafe(propertyDrawer, property.Copy(), label ?? TempContent(property.displayName));
        } finally {
          handler.nestingLevel--;
        }
        return height;
      }

      if (IsReorderableArray(property, qhandler)) {
        return height + GetReorderableWrapper(property, qhandler, label).GetHeight(property.isExpanded);
      }

      // property line/foldout
      height += GetSinglePropertyHeight(property, label);
      if (!property.isExpanded) {
        return height;
      }

      

      if (includeChildren && HasVisibleChildFields(property)) {
        int arraySize = 0;

        if (property.isArray) {
          if (qhandler.GetArrayModifiers(property, out arraySize, out var arrayModifiers)) {
            if (HasFlags(arrayModifiers, ArrayPropertyModifiers.InlineFirstElement) && property.arraySize > 0) {
              // first element starts at the same line as the array
              height -= GetSinglePropertyHeight(property, label);
              height += GetPropertyHeight(property.GetArrayElementAtIndex(0), WhitespaceContent, true);
              return height;
            }
          }
        }

        // default handlers
        bool enterChildren = true;
        for (SerializedProperty child = property.Copy(), end = property.GetEndProperty(); child.NextVisible(enterChildren) && !SerializedProperty.EqualContents(child, end); enterChildren = false) {
          if (property.isArray) {
            if (child.propertyType != SerializedPropertyType.ArraySize && --arraySize < 0) {
              break;
            }
          }

          float h = GetPropertyHeight(child, TempContent(property.displayName), true);
          if (h > 0) {
            height += h + EditorGUIUtility.standardVerticalSpacing;
          }
        }
      }

      return height;
    }

    private static string GetPrettyAttributeTypeName(Type attributeType) {
      Debug.Assert(attributeType.IsSubclassOf(typeof(Attribute)));
      string name = attributeType.Name;
      if (name.EndsWith("Attribute")) {
        return name.Substring(0, name.Length - "Attribute".Length);
      }
      return name;
    }

    private static float GetSinglePropertyHeight(SerializedProperty prop, GUIContent label) {
      return prop != null ? EditorGUI.GetPropertyHeight(prop.propertyType, label) : EditorGUIUtility.singleLineHeight;
    }

    static partial void HandleUnknownAttributeUser(ref UnityInternal.PropertyHandler handler, SerializedProperty property, Inspector.PropertyAttribute attribute, FieldInfo field, Type fieldType);

    private static bool HasVisibleChildFields(SerializedProperty property) {
      switch (property.propertyType) {
        case SerializedPropertyType.Vector3:
        case SerializedPropertyType.Vector2:
        case SerializedPropertyType.Vector3Int:
        case SerializedPropertyType.Vector2Int:
        case SerializedPropertyType.Rect:
        case SerializedPropertyType.RectInt:
        case SerializedPropertyType.Bounds:
        case SerializedPropertyType.BoundsInt:
          return false;
      }

      //if (!isUIElements && PropertyHandler.IsNonStringArray(property)) return false;

      return property.hasVisibleChildren;
    }

    private static UnityInternal.PropertyHandler InjectQuantumPropertyAttributes(SerializedProperty property, bool forceAddMeta = false) {
      var handler = UnityInternal.ScriptAttributeUtility.GetHandler(property);

      PropertyHandlerMeta meta = null;

      var field = UnityInternal.ScriptAttributeUtility.GetFieldInfoFromProperty(property, out var fieldType);
      if (field == null) {
        if (property.propertyType == SerializedPropertyType.ArraySize) {
          // array size properties may be hidden 
          const string ArraySizeSuffix = ".Array.size";
          string path = property.propertyPath;
          if (path.EndsWith(ArraySizeSuffix)) {
            var arrayProperty = property.serializedObject.FindProperty(path.Substring(0, path.Length - ArraySizeSuffix.Length));
            if (arrayProperty != null) {
              field = UnityInternal.ScriptAttributeUtility.GetFieldInfoFromProperty(arrayProperty, out fieldType);
              var arrayLength = field?.GetCustomAttribute<ArrayLengthAttribute>();
              if (arrayLength != null) {
                meta = new PropertyHandlerMeta();
                AddDecorator(ref handler, property, meta);
                meta.specialAttributes.Add(arrayLength);
              }
            }
          }
        }
        return handler;
      }

      if (UnityInternal.PropertyHandler.UseReorderabelListControl(property)) {
        forceAddMeta = true;
      }

      if (forceAddMeta) {
        meta = new PropertyHandlerMeta();
        AddDecorator(ref handler, property, meta);
      }

      foreach (var attribute in field.GetCustomAttributes<Quantum.Inspector.PropertyAttribute>()) {
        // following attributes are special and can't work with the usual decorator-drawer setup
        if (attribute is ArrayLengthAttribute ||
            attribute is DrawIfAttribute ||
            attribute is OptionalAttribute ||
            attribute is HideInInspectorAttribute ||
            attribute is ReadOnlyAttribute ||
            attribute is DictionaryAttribute ||
            attribute is DynamicCollectionAttribute ||
            attribute is ReorderableAttribute) {
          if (meta == null) {
            meta = new PropertyHandlerMeta();
            AddDecorator(ref handler, property, meta);
          }
          if (attribute is DynamicCollectionAttribute) {
            if (field.GetCustomAttribute<Core.FreeOnComponentRemovedAttribute>() == null) {
              meta.isDynamic = true;
            }
          } else {
            meta.specialAttributes.Add(attribute);
          }
        } else {
          // decorators are for scalars and arrays as a whole
          if (!field.FieldType.IsArrayOrList() || fieldType.IsArrayOrList()) {
            if (attribute is DisplayNameAttribute displayName) {
              if (meta == null) {
                meta = new PropertyHandlerMeta();
                AddDecorator(ref handler, property, meta);
              }
              meta.specialAttributes.Add(displayName);
              continue;
            }

            if (attribute is Inspector.HeaderAttribute headerAttribute) {
              AddPropertyAttribute(ref handler, property, new UnityEngine.HeaderAttribute(headerAttribute.Header), field, fieldType);
              continue;
            } else if (attribute is Inspector.SpaceAttribute space) {
              AddPropertyAttribute(ref handler, property, new UnityEngine.SpaceAttribute(space.Height), field, fieldType);
              continue;
            } else if (attribute is Inspector.TooltipAttribute tooltip) {
              AddPropertyAttribute(ref handler, property, new UnityEngine.TooltipAttribute(tooltip.Tooltip), field, fieldType);
              continue;
            }
          }

          if (!fieldType.IsArrayOrList()) {
            var unityAttributeType = s_attributeToUnityAttribute.Value[attribute.GetType()].FirstOrDefault();
            if (unityAttributeType != null) {
              var unityAttribute = (UnityEngine.PropertyAttribute)Activator.CreateInstance(unityAttributeType, attribute);
              AddPropertyAttribute(ref handler, property, unityAttribute, field, fieldType);
              continue;
            }
          }

          HandleUnknownAttributeUser(ref handler, property, attribute, field, fieldType);
        }
      }

      if (meta != null) {
        meta.leafFieldType = field.FieldType.GetUnityLeafType();
      }

      return handler;
    }

    private static bool IsReorderableArray(SerializedProperty property, PropertyHandlerMeta meta) {
      if (meta.isArrayReorderable == null) {
        if (property.isArray) {
          meta.isArrayReorderable = UnityInternal.PropertyHandler.UseReorderabelListControl(property);

          meta.GetArrayModifiers(property, out _, out var modifiers);
          if (HasFlags(modifiers, ArrayPropertyModifiers.ForceReorderable)) {
            meta.isArrayReorderable = true;
          }
        } else {
          meta.isArrayReorderable = false;
        }
      }

      return meta.isArrayReorderable.Value;
    }

    private static PropertyFieldResult PropertyFieldInternal(Rect position, SerializedProperty property, GUIContent label, bool includeChildren, UnityInternal.PropertyHandler handler, PropertyHandlerMeta qhandler) {

      TestInvalidateCache();

      using (var guiState = PropertyFieldGUIState.Capture(position)) {
        float labelWidth = EditorGUIUtility.labelWidth;
        float remainingHeight = position.height;

        position.height = 0;

        if (label != GUIContent.none) {
          if (qhandler.TryGetDisplayNameOverride(out var displayName)) {
            label = TempContent(displayName);
          } else {
            label = label ?? TempContent(property.displayName);
          }
        }

        if (!string.IsNullOrEmpty(handler.tooltip)) {
          label.tooltip = handler.tooltip;
        }

        var result = PropertyFieldResult.None;

        int forcedArraySize = 0;
        ArrayPropertyModifiers arrayModifiers = ArrayPropertyModifiers.None;

        if ( property.isArray) {
          if (qhandler.GetArrayModifiers(property, out forcedArraySize, out arrayModifiers) && property.arraySize != forcedArraySize) {
            property.arraySize = forcedArraySize;
            GUI.changed = true;
          }
        } else if ( property.propertyType == SerializedPropertyType.ArraySize ) {
          if (qhandler.GetArraySizeModifiers(property, out forcedArraySize, out arrayModifiers)) {
            if (property.intValue != forcedArraySize) {
              property.intValue = forcedArraySize;
              GUI.changed = true;
            }
            if (HasFlags(arrayModifiers, ArrayPropertyModifiers.HideSize)) {
              return PropertyFieldResult.None;
            }
          }
        }

        var visibilityModifier = qhandler.GetVisibilityModifiers(property);
        if (visibilityModifier == PropertyVisibilityModifiers.Hidden) {
          return PropertyFieldResult.None;
        } else if (visibilityModifier == PropertyVisibilityModifiers.Disabled) {
          GUI.enabled = false;
          result = PropertyFieldResult.ReadOnly;
        }

        if (!handler.isCurrentlyNested) {
          // handle base decorators
          if (handler.decoratorDrawers != null) {
            foreach (var decorator in handler.decoratorDrawers) {
              position.height = decorator.GetHeight();

              decorator.OnGUI(position);
              EditorGUIUtility.labelWidth = labelWidth;
              EditorGUIUtility.fieldWidth = guiState.fieldWidth;

              position.y += position.height;
              remainingHeight -= position.height;
            }
          }

          // optional can only be applied after the decorators are done
          if (qhandler.TryGetOptionalState(property, out bool? optionalEnabled)) {
            const float ToggleWidth = 16.0f;
            EditorGUI.BeginChangeCheck();
            var toggleRect = position.SetHeight(GetSinglePropertyHeight(property, label)).SetWidth(EditorGUIUtility.labelWidth + ToggleWidth);
            bool toggleValue = optionalEnabled != false;

            using (new PropertyScope(toggleRect, label, property)) {
              using (new ShowMixedValueScope(optionalEnabled == null)) {
                toggleValue = EditorGUI.Toggle(toggleRect, label, toggleValue);
              }
            }

            if (EditorGUI.EndChangeCheck()) {
              qhandler.SetOptionalState(property, toggleValue);
            }

            if (optionalEnabled == true) {
              // in case of single-line properties, expand the label so that the default drawer
              // won't draw on top of the toggle
              if (remainingHeight < 2 * EditorGUIUtility.singleLineHeight) {
                EditorGUIUtility.labelWidth = labelWidth += ToggleWidth;
              }
              label = WhitespaceContent;
            } else {
              property.isExpanded = false;
              return result;
            }
          }
        }

        // handle regular PropertyDrawer
        if (handler.currentPropertyDrawer != null) {
          position.height = remainingHeight;
          var propertyDrawer = handler.currentPropertyDrawer;
          handler.nestingLevel++;         
          try {
            UnityInternal.PropertyDrawer.OnGUISafe(propertyDrawer, position, property.Copy(), label);
          } finally {
            handler.nestingLevel--;
          }

          EditorGUIUtility.labelWidth = labelWidth;
          EditorGUIUtility.fieldWidth = guiState.fieldWidth;

          return result;
        }

        position.height = remainingHeight;

        Rect defaultPropertyRect = position.SetHeight(GetSinglePropertyHeight(property, label));

        string propertyMessage = null;
        MessageType propertyMessageType = MessageType.None;

        if (HasFlags(arrayModifiers, ArrayPropertyModifiers.Dynamic) && forcedArraySize > 0) {
          propertyMessage = DynamicCollectionWarning;
          propertyMessageType = MessageType.Info;
        }

        if (HasFlags(arrayModifiers, ArrayPropertyModifiers.Dictionary)) {
          string verifyMessage = VerifyDictionary(property);
          if (!string.IsNullOrEmpty(verifyMessage)) {
            propertyMessage = verifyMessage;
            propertyMessageType = MessageType.Error;
          }
        }


        if (IsReorderableArray(property, qhandler)) {
          UnityInternal.ReorderableListWrapper wrapper = GetReorderableWrapper(property, qhandler, label);

          var infinityRect = new Rect(0.0f, 0.0f, float.PositiveInfinity, float.PositiveInfinity);

          Rect sizeRect = defaultPropertyRect.SetXMin(defaultPropertyRect.xMax - 48.0f);

          // don't allow for size selection
          if (HasFlags(arrayModifiers, ArrayPropertyModifiers.HideSize)) {
            if (Event.current.isMouse && sizeRect.Contains(Event.current.mousePosition)) {
              Event.current.Use();
            }
          }

          wrapper.Property = property;


          if (!property.isExpanded && HasFlags(arrayModifiers, ArrayPropertyModifiers.HideSize)) {
            // we'll draw the size box ourselves
            wrapper.Draw(label, position, infinityRect, label.tooltip, false);
            using (new EditorGUI.DisabledScope(true)) {
              EditorGUI.TextField(sizeRect, property.arraySize.ToString());
            }
          } else {
            wrapper.Draw(label, position, infinityRect, label.tooltip, true);
          }

          if (!string.IsNullOrEmpty(propertyMessage)) {
            PrefixIcon(sizeRect, propertyMessage, propertyMessageType, true);
          }

          return result;
        }

        {
          bool expanded = UnityInternal.EditorGUI.DefaultPropertyField(defaultPropertyRect, property, label);

          if (!string.IsNullOrEmpty(propertyMessage)) {
            PrefixIcon(defaultPropertyRect.AddXMin(EditorGUIUtility.labelWidth), propertyMessage, propertyMessageType);
          }

          if (!expanded) {
            return result;
          }
        }

        if (!includeChildren) {
          return result | PropertyFieldResult.InspectChildren;
        }

        if (HasVisibleChildFields(property)) {
          GUIContent childLabel = null;

          if (property.isArray) {
            if (HasFlags(arrayModifiers, ArrayPropertyModifiers.InlineFirstElement) && property.arraySize > 0) {
              // make sure it draws on top of
              var firstElement = property.GetArrayElementAtIndex(0);
              firstElement.isExpanded = true;
              PropertyField(position, firstElement, GUIContent.none, true);
              return result;
            }
          }

          position = position.SetYMin(defaultPropertyRect.yMax + EditorGUIUtility.standardVerticalSpacing);

          bool enterChildren = true;
          for (SerializedProperty child = property.Copy(), end = property.GetEndProperty(); child.NextVisible(enterChildren) && !SerializedProperty.EqualContents(child, end);) {
            enterChildren = false;
            bool fullPass = false;

            using (AcquireHandler(child, out var childHandler, out var childQHandler)) {
              fullPass = childQHandler.NeedsFullDrawPass(child, childQHandler);

              EditorGUI.indentLevel = guiState.indent + child.depth - property.depth;
              position.height = GetPropertyHeightInternal(child, null, fullPass, childHandler, childQHandler);

              if (position.height <= 0) {
                continue;
              }

              EditorGUI.BeginChangeCheck();

              // NOTE: this is where it diverges from the base implementation - we pass includeChildren as true here
              enterChildren =
                (PropertyFieldInternal(position, child, childLabel, fullPass, childHandler, childQHandler) & PropertyFieldResult.InspectChildren) == PropertyFieldResult.InspectChildren
                && HasVisibleChildFields(child)
                && !fullPass;

              if (EditorGUI.EndChangeCheck()) {
                break;
              }
            }

            position.y += position.height + EditorGUIUtility.standardVerticalSpacing;
          }
        }

        return result;
      }
    }

    private static UnityInternal.ReorderableListWrapper GetReorderableWrapper(SerializedProperty property, PropertyHandlerMeta qhandler, GUIContent label) {
      string propertyIdentifier = UnityInternal.ReorderableListWrapper.GetPropertyIdentifier(property);

      bool isDirty = false;

      if (!UnityInternal.PropertyHandler.s_reorderableLists.TryGetValue(propertyIdentifier, out var wrapper)) {
        wrapper = UnityInternal.ReorderableListWrapper.Create(property, label);
        UnityInternal.PropertyHandler.s_reorderableLists.Add(propertyIdentifier, wrapper);
        isDirty = true;
      } else if (wrapper.Property != property) {
        wrapper.Property = property;
        isDirty = true;
      }

      if (isDirty) {
        var list = wrapper.m_ReorderableList;

        list.headerHeight = 0.0f;
        list.elementHeightCallback = index => {
          return GetPropertyHeight(list.serializedProperty.GetArrayElementAtIndex(index), GUIContent.none, true) + EditorGUIUtility.standardVerticalSpacing;
        };
        list.drawElementCallback = (r, index, isActive, isFocused) => {
          PropertyField(r, list.serializedProperty.GetArrayElementAtIndex(index), TempContent($"Element {index}"), true);
        };

        if (qhandler.TryGetSpecialAttribute(out ArrayLengthAttribute arrayLength)) {
          if (arrayLength.MinLength == arrayLength.MaxLength) {
            list.displayAdd = false;
            list.displayRemove = false;
            list.footerHeight = 0.0f;
          }
        }
      }

      return wrapper;
    }

    private static PropertyFieldResult PropertyFieldLayoutInternal(SerializedProperty property, GUIContent label, bool includeChildren, params GUILayoutOption[] options) {
      using (AcquireHandler(property, out var handler, out var qhandler)) {
        bool hasText = true;
        if (label == null || (string.IsNullOrEmpty(label.text) && label.image == null)) {
          hasText = false;
        }

        float h = GetPropertyHeightInternal(property, label, includeChildren, handler, qhandler);

        if (h > 0) {
          Rect r = EditorGUILayout.GetControlRect(hasText, h, options);
          return PropertyFieldInternal(r, property, label, includeChildren, handler, qhandler);
        } else {
          return PropertyFieldResult.None;
        }
      }
    }

    private static HashSet<SerializedProperty> _dictionaryKeyHash = new HashSet<SerializedProperty>(new SerializedPropertyEqualityComparer());

    private static string VerifyDictionary(SerializedProperty prop) {
      Debug.Assert(prop.isArray);
      try {
        for (int i = 0; i < prop.arraySize; ++i) {
          var keyProperty = prop.GetArrayElementAtIndex(i).FindPropertyRelativeOrThrow(DictionaryKeyPropertyName);
          if (!_dictionaryKeyHash.Add(keyProperty)) {

            // there are duplicates - take the slow and allocating path now
            return string.Join("\n",
              Enumerable.Range(0, prop.arraySize)
                .GroupBy(x => prop.GetArrayElementAtIndex(x).FindPropertyRelative(DictionaryKeyPropertyName), x => x, _dictionaryKeyHash.Comparer)
                .Where(x => x.Count() > 1)
                .Select(x => $"Duplicate keys for elements: {string.Join(", ", x)}")
              );
          }
        }

        return null;

      } finally {
        _dictionaryKeyHash.Clear();
      }
    }


    private static void TestInvalidateCache() {
      GameObject activeObject = Selection.activeObject as GameObject;
      if (activeObject != null) {
        var components = activeObject.GetComponents(typeof(Component));
        if (s_LastInspectionTarget != activeObject.GetInstanceID() ||
            s_LastInspectorNumComponents != components.Length) {

          UnityInternal.PropertyHandler.s_reorderableLists.Clear();

          s_LastInspectionTarget = activeObject.GetInstanceID();
          s_LastInspectorNumComponents = components.Length;
        }
      }
    }


    private struct PropertyFieldGUIState : IDisposable {
      public bool enabled;
      public float fieldWidth;
      public Vector2 iconSize;
      public int indent;
      public float labelWidth;
      public Rect position;

      public static PropertyFieldGUIState Capture(Rect position) {
        return new PropertyFieldGUIState() {
          labelWidth = EditorGUIUtility.labelWidth,
          fieldWidth = EditorGUIUtility.fieldWidth,
          enabled = GUI.enabled,
          iconSize = EditorGUIUtility.GetIconSize(),
          indent = EditorGUI.indentLevel,
          position = position
        };
      }

      public void Dispose() {
        EditorGUIUtility.labelWidth = labelWidth;
        EditorGUIUtility.fieldWidth = fieldWidth;
        GUI.enabled = enabled;
        EditorGUIUtility.SetIconSize(iconSize);
        EditorGUI.indentLevel = indent;
      }
    }

    private sealed class PropertyHandlerMeta : DecoratorDrawer, IDisposable {
      public List<Quantum.Inspector.PropertyAttribute> specialAttributes = new List<Inspector.PropertyAttribute>();
      public bool isDynamic;
      public Type leafFieldType;
      public bool? isArrayReorderable;

      public override bool CanCacheInspectorGUI() {
        return false;
      }

      public void Dispose() {
      }

      public bool GetArraySizeModifiers(SerializedProperty property, out int count, out ArrayPropertyModifiers modifiers) {
        Debug.Assert(property.propertyType == SerializedPropertyType.ArraySize);

        modifiers = ArrayPropertyModifiers.None;

        if (TryGetSpecialAttribute(out ArrayLengthAttribute arrayLength)) {
          count = Mathf.Clamp(property.intValue, arrayLength.MinLength, arrayLength.MaxLength);
          if (arrayLength.MinLength == 0 && arrayLength.MaxLength == 1 || arrayLength.MinLength == arrayLength.MaxLength) {
            modifiers = ArrayPropertyModifiers.HideSize;
          }
          return true;
        }

        count = property.intValue;
        return false;
      }


      public bool GetArrayModifiers(SerializedProperty property, out int count, out ArrayPropertyModifiers modifiers) {
        Debug.Assert(property.isArray);

        modifiers = isDynamic ? ArrayPropertyModifiers.Dynamic : ArrayPropertyModifiers.None;

        if (TryGetSpecialAttribute(out Quantum.Inspector.ReorderableAttribute _)) {
          modifiers |= ArrayPropertyModifiers.ForceReorderable;
        }

        if (TryGetSpecialAttribute(out DictionaryAttribute _)) {
          modifiers |= ArrayPropertyModifiers.Dictionary;
          count = property.arraySize;
          return true;
        }

        if (TryGetSpecialAttribute(out ArrayLengthAttribute arrayLength)) {
          count = Mathf.Clamp(property.arraySize, arrayLength.MinLength, arrayLength.MaxLength);
          if (arrayLength.MinLength == 0 && arrayLength.MaxLength == 1) {
            modifiers |= ArrayPropertyModifiers.InlineFirstElement;
          } else if (arrayLength.MinLength == arrayLength.MaxLength) {
            modifiers |= ArrayPropertyModifiers.HideSize;
          }
          return true;
        }

        count = property.arraySize;
        return false;
      }

      public override float GetHeight() {
        return 0;
      }

      public PropertyVisibilityModifiers GetVisibilityModifiers(SerializedProperty property) {
        if (TryGetSpecialAttribute(out HideInInspectorAttribute _)) {
          return PropertyVisibilityModifiers.Hidden;
        }

        var result = PropertyVisibilityModifiers.None;

        foreach (var attr in specialAttributes) {
          if (attr is DrawIfAttribute drawIf) {
            var otherProperty = property.FindPropertyRelativeToParentOrThrow(drawIf.FieldName);
            if (otherProperty.hasMultipleDifferentValues || !DrawIfAttribute.CheckDraw(drawIf, otherProperty.GetIntegerValue())) {
              if (drawIf.Hide == DrawIfHideType.Hide) {
                return PropertyVisibilityModifiers.Hidden;
              } else if (drawIf.Hide == DrawIfHideType.ReadOnly) {
                result = PropertyVisibilityModifiers.Disabled;
              }
            }
          }
        }

        if (TryGetSpecialAttribute(out ReadOnlyAttribute _)) {
          result = PropertyVisibilityModifiers.Disabled;
        }

        return result;
      }

      public bool NeedsFullDrawPass(SerializedProperty property, PropertyHandlerMeta meta) {
        if (property.isArray) {
          if (IsReorderableArray(property, meta)) {
            return true;
          }

          return TryGetSpecialAttribute(out ArrayLengthAttribute _);
        }
        return TryGetSpecialAttribute(out OptionalAttribute _);
      }

      public override void OnGUI(Rect position) {
      }

      public bool TryGetDisplayNameOverride(out string name) {
        if (TryGetSpecialAttribute(out DisplayNameAttribute displayName)) {
          name = displayName.Name;
          return true;
        } else {
          name = null;
          return false;
        }
      }

      public bool TryGetSpecialAttribute<T>(out T value) where T : Quantum.Inspector.PropertyAttribute {
        if (specialAttributes == null) {
          value = default;
          return false;
        }

        value = null;
        foreach (var attr in specialAttributes) {
          if (attr is T attrT) {
            value = attrT;
            break;
          }
        }
        return value != null;
      }

      internal void SetOptionalState(SerializedProperty property, bool v) {
        if (TryGetSpecialAttribute(out OptionalAttribute optional)) {
          var optionalProperty = property.FindPropertyRelativeToParent(optional.EnabledPropertyPath);
          if (optionalProperty != null) {
            if (optionalProperty.type == nameof(QBoolean)) {
              optionalProperty = optionalProperty.FindPropertyRelativeOrThrow(nameof(QBoolean.Value));
            }
            optionalProperty.boolValue = v;
          }
        } else if (property.isArray && TryGetSpecialAttribute(out ArrayLengthAttribute arrayLength) && arrayLength.MinLength == 0 && arrayLength.MaxLength == 1) {
          var optionalProperty = property.GetArraySizePropertyOrThrow();
          Debug.Assert(optionalProperty.propertyType == SerializedPropertyType.ArraySize);
          optionalProperty.boolValue = v;
        }
      }

      internal bool TryGetOptionalState(SerializedProperty property, out bool? optionalEnabled) {
        if (TryGetSpecialAttribute(out OptionalAttribute optional)) {
          var optionalProperty = property.FindPropertyRelativeToParent(optional.EnabledPropertyPath);

          if (optionalProperty == null) {
            Debug.LogAssertion($"Optional flag {optional.EnabledPropertyPath} not found for {property.propertyPath}");
            optionalEnabled = default;
            return false;
          } else {
            if (optionalProperty?.type == nameof(QBoolean)) {
              optionalProperty = optionalProperty.FindPropertyRelativeOrThrow(nameof(QBoolean.Value));
            }

            if (optionalProperty.hasMultipleDifferentValues) {
              optionalEnabled = null;
            } else {
              optionalEnabled = optionalProperty?.boolValue ?? true;
            }
            return true;
          }
        } else if (property.isArray && TryGetSpecialAttribute(out ArrayLengthAttribute arrayLength) && arrayLength.MinLength == 0 && arrayLength.MaxLength == 1) {
          var optionalProperty = property.GetArraySizePropertyOrThrow();
          Debug.Assert(optionalProperty.propertyType == SerializedPropertyType.ArraySize);
          if (optionalProperty.hasMultipleDifferentValues) {
            optionalEnabled = null;
          } else {
            optionalEnabled = optionalProperty.boolValue;
          }
          return true;
        }

        optionalEnabled = false;
        return false;
      }
    }
  }
}
#endregion

#region quantum_unity/Assets/Photon/Quantum/Editor/EditorGUI/QuantumEditorGUI.Utils.cs
namespace Quantum.Editor {
  using System.Linq;
  using UnityEditor;
  using UnityEngine;

  public partial class QuantumEditorGUI {
    public const int CheckboxWidth = 16;
    public const float MinPrefixWidth = 20.0f;
    public const string ScriptPropertyName = "m_Script";
    public static readonly GUIContent WhitespaceContent = new GUIContent(" ");
    private static GUIStyle _overlayStyle;
    private static GUIContent _tempContent = new GUIContent();

    private static GUIContent TempContent(string str) {
      _tempContent.text = str;
      _tempContent.image = null;
      _tempContent.tooltip = null;
      return _tempContent;
    }

    private static GUIStyle MiddleRightMiniLabelStyle {
      get {
        if (_overlayStyle == null) {
          _overlayStyle = new GUIStyle(EditorStyles.miniLabel) {
            alignment = TextAnchor.MiddleRight,
            contentOffset = new Vector2(-2, 0),
          };
        }
        return _overlayStyle;
      }
    }

    public static float GetLinesHeight(int count) {
      return count * (EditorGUIUtility.singleLineHeight) + (count - 1) * EditorGUIUtility.standardVerticalSpacing;
    }

    public static float GetLinesHeightWithNarrowModeSupport(int count) {
      if (!EditorGUIUtility.wideMode) {
        count++;
      }
      return count * (EditorGUIUtility.singleLineHeight) + (count - 1) * EditorGUIUtility.standardVerticalSpacing;
    }

    public static void MultiTypeObjectField(Rect rect, SerializedProperty prop, GUIContent label, params System.Type[] types) {
      UnityEngine.Object obj = prop.objectReferenceValue;
      using (new PropertyScope(rect, label, prop)) {
        rect = EditorGUI.PrefixLabel(rect, label);
        using (new EditorGUI.IndentLevelScope(-EditorGUI.indentLevel)) {
          EditorGUI.BeginChangeCheck();
          if (obj != null) {
            var matchingType = types.SingleOrDefault(x => x.IsInstanceOfType(obj));
            if (matchingType != null) {
              obj = EditorGUI.ObjectField(rect, obj, matchingType, true);
            } else {
              obj = EditorGUI.ObjectField(rect, obj, typeof(UnityEngine.Object), true);
              Decorate(rect, $"Type not supported: {obj?.GetType()}", MessageType.Error);
            }
          } else {
            var r = rect.SetWidth(rect.width / types.Length);
            foreach (var t in types) {
              var value = EditorGUI.ObjectField(r, null, t, true);
              if (obj == null) {
                obj = value;
              }
              r.x += r.width;
            }
          }
          if (EditorGUI.EndChangeCheck()) {
            prop.objectReferenceValue = obj;
            prop.serializedObject.ApplyModifiedProperties();
          }
        }
      }
    }

    public static void MultiTypeObjectField(SerializedProperty prop, GUIContent label, params System.Type[] types) {
      MultiTypeObjectField(EditorGUILayout.GetControlRect(), prop, label, types);
    }

    public static void MultiTypeObjectField(SerializedProperty prop, GUIContent label, System.Type[] types, params GUILayoutOption[] options) {
      MultiTypeObjectField(EditorGUILayout.GetControlRect(options), prop, label, types);
    }

    public static Rect PrefixIcon(Rect rect, string tooltip, MessageType messageType, bool alignLeft = false) {
      var content = EditorGUIUtility.TrTextContentWithIcon(string.Empty, tooltip, messageType);
      var iconRect = rect;
      iconRect.width = Mathf.Min(MinPrefixWidth, rect.width);
      if ( alignLeft ) {
        iconRect.x -= iconRect.width;
      }

      GUI.Label(iconRect, content, new GUIStyle());

      rect.width = Mathf.Max(0, rect.width - iconRect.width);
      rect.x += iconRect.width;

      return rect;
    }

    public static void Overlay(Rect rect, string message, Color? color = null) {
      if (color == null) {
        var c = EditorGUIUtility.isProSkin ? Color.yellow : Color.blue;
        c.a = 0.75f;
        color = c;
      }

      using (new ColorScope(color.Value)) {
        GUI.Label(rect, message, MiddleRightMiniLabelStyle);
      }
    }

    public static void Decorate(Rect rect, string tooltip, MessageType messageType, bool hasLabel = false, bool drawBorder = true) {

      if (hasLabel) {
        rect.xMin += EditorGUIUtility.labelWidth;
      }

      var content = EditorGUIUtility.TrTextContentWithIcon(string.Empty, tooltip, messageType);
      var iconRect = rect;
      iconRect.width = Mathf.Min(20, rect.width);
      iconRect.xMin -= iconRect.width;

      GUI.Label(iconRect, content, new GUIStyle());

      if (drawBorder) {
        Color borderColor;
        switch (messageType) {
          case MessageType.Warning:
            borderColor = new Color(1.0f, 0.5f, 0.0f);
            break;
          case MessageType.Error:
            borderColor = new Color(1.0f, 0.0f, 0.0f);
            break;
          default:
            borderColor = Color.white;
            break;
        }
        GUI.DrawTexture(rect, Texture2D.whiteTexture, ScaleMode.StretchToFill, false, 0, borderColor, 1.0f, 1.0f);
      }
    }

    public static void Header(string header) {
      EditorGUILayout.Space();
      EditorGUILayout.LabelField(header, EditorStyles.boldLabel);
    }

    public static void ScriptPropertyField(SerializedObject obj) {
      var scriptProperty = obj.FindProperty(ScriptPropertyName);
      if (scriptProperty != null) {
        using (new EditorGUI.DisabledScope(true)) {
          EditorGUILayout.PropertyField(scriptProperty);
        }
      }
    }

    public static void ScriptPropertyField(UnityEngine.Object obj) {
      MonoScript script = null;
      var asScriptableObject = obj as ScriptableObject;
      if (asScriptableObject) {
        script = MonoScript.FromScriptableObject(asScriptableObject);
      } else {
        var asMonoBehaviour = obj as MonoBehaviour;
        if (asMonoBehaviour) {
          script = MonoScript.FromMonoBehaviour(asMonoBehaviour);
        }
      }

      using (new EditorGUI.DisabledScope(true)) {
        script = EditorGUILayout.ObjectField("Script", script, typeof(MonoScript), false) as MonoScript;
      }
    }

    public static bool IsNullOrEmpty(GUIContent content) {
      if (content == null || (string.IsNullOrEmpty(content.text) && content.image == null)) {
        return true;
      }
      return false;
    }

    public static void LargeTooltip(Rect areaRect, Rect itemRect, GUIContent content) {

      const float ArrowWidth = 64.0f;
      const float ArrowHeight = 6.0f;

      var contentSize = UnityInternal.Styles.AnimationEventTooltip.CalcSize(content);
      var anchor = new Vector2(itemRect.center.x, itemRect.yMax);

      var arrowRect = new Rect(anchor.x - ArrowWidth / 2.0f, anchor.y, ArrowWidth, ArrowHeight);
      var labelRect = new Rect(anchor.x, anchor.y + ArrowHeight, contentSize.x, contentSize.y);

      // these are some magic values that Unity seems to be using with this style
      if (labelRect.xMax > areaRect.xMax + 16)
        labelRect.x = areaRect.xMax - labelRect.width + 16;
      if (arrowRect.xMax > areaRect.xMax + 20)
        arrowRect.x = areaRect.xMax - arrowRect.width + 20;
      if (labelRect.xMin < areaRect.xMin + 30)
        labelRect.x = areaRect.xMin + 30;
      if (arrowRect.xMin < areaRect.xMin - 20)
        arrowRect.x = areaRect.xMin - 20;

      // flip tooltip if too close to bottom (but do not flip if flipping would mean the tooltip is too high up)
      var flipRectAdjust = (itemRect.height + labelRect.height + 2 * arrowRect.height);
      var flipped = (anchor.y + contentSize.y + 6 > areaRect.yMax) && (labelRect.y - flipRectAdjust > 0);
      if (flipped) {
        labelRect.y -= flipRectAdjust;
        arrowRect.y -= (itemRect.height + 2 * arrowRect.height);
      }

      using (new GUI.ClipScope(arrowRect)) {
        var oldMatrix = GUI.matrix;
        try {
          if (flipped)
            GUIUtility.ScaleAroundPivot(new Vector2(1.0f, -1.0f), new Vector2(arrowRect.width * 0.5f, arrowRect.height));
          GUI.Label(new Rect(0, 0, arrowRect.width, arrowRect.height), GUIContent.none, UnityInternal.Styles.AnimationEventTooltipArrow);
        } finally {
          GUI.matrix = oldMatrix;
        }
      }

      GUI.Label(labelRect, content, UnityInternal.Styles.AnimationEventTooltip);
    }

  }
}
#endregion

#region quantum_unity/Assets/Photon/Quantum/Editor/EditorGUI/QuantumEditorUtility.cs
namespace Quantum.Editor {

  using System;
  using System.Collections.Generic;
  using System.IO;
  using System.Linq;
  using System.Reflection;
  using System.Text;
  using System.Text.RegularExpressions;
  using Photon.Deterministic;
  using Quantum.Core;
  using Quantum.Prototypes;
  using UnityEditor;
  using UnityEngine;


  public unsafe partial class QuantumEditorUtility {

    public static IEnumerable<Type> ComponentTypes => Statics.Components.Keys;

    public static string DumpPointer(FrameBase frame, void* ptr, Type type) {
      var printer = Statics.Printer;
      try {
        printer.Reset(frame);
        printer.AddPointer("#ROOT", ptr, type);
        return printer.ToString();
      } finally {
        printer.Reset(null);
      }
    }

    public static string GenerateAcronym(string str) {
      StringBuilder acronymBuilder = new StringBuilder();

      for (int i = 0; i < str.Length; ++i) {
        var c = str[i];
        if (i != 0 && char.IsLower(c)) {
          continue;
        }
        acronymBuilder.Append(c);
      }

      return acronymBuilder.ToString();
    }

    public static GUIContent GetComponentAcronym(Type componentType) {
      return Statics.Components[componentType].ThumbnailContent;
    }

    public static Color GetComponentColor(Type componentType) {
      return Statics.Components[componentType].ThumbnailColor;
    }

    public static GUIContent GetComponentDisplayName(Type componentType) {
      return Statics.Components[componentType].DisplayName;
    }

    public static void* GetComponentPointer(FrameBase frame, EntityRef entityRef, Type componentType) {
      return frame.Unsafe.GetPointer(entityRef, Statics.Components[componentType].Id);
    }

    public static void GetComponentThumbnailData(Type componentType, out GUIContent label, out Color color) {
      var entry = Statics.Components[componentType];
      label = entry.ThumbnailContent;
      color = entry.ThumbnailColor;
    }

    public static bool TryGetComponentType(string name, out Type type, bool assemblyQualified = false) {
      if (assemblyQualified) {
        if (Statics.ComponentsByAQName.TryGetValue(name, out var entry)) {
          type = entry.ComponentType;
          return true;
        }
      } else {
        if (Statics.ComponentsByName.TryGetValue(name, out var entry)) {
          type = entry.ComponentType;
          return true;
        }
      }
      type = null;
      return false;
    }

    public static Type GetComponentType(string componentName, bool assemblyQualified = false) {
      if (TryGetComponentType(componentName, out var result, assemblyQualified)) {
        return result;
      }
      throw new ArgumentOutOfRangeException($"Component not found: {componentName} (assemblyQualified: {assemblyQualified})");
    }

    public static bool TryGetAssetType(string assemblyQualifiedName, out Type type) {
      if (Statics.AssetsByAQName.TryGetValue(assemblyQualifiedName, out var entry)) {
        type = entry.AssetType;
        return true;
      }
      type = null;
      return false;
    }

    public static void GetAssetThumbnailData(Type assetType, out GUIContent label, out Color color) {
      var entry = Statics.Assets[assetType];
      label = entry.ThumbnailContent;
      color = entry.ThumbnailColor;
    }

    public static SerializedProperty GetKnownObjectRoot(object obj) {
      var container = QuantumEditorUtilityContainer.instance;
      container.ObjectsContainer.Store(obj);

      var so = new SerializedObject(container);
      var arrayProperty = so.FindPropertyOrThrow($"{nameof(container.ObjectsContainer)}.{obj.GetType().Name}");
      return arrayProperty.GetArrayElementAtIndex(0);
    }

    public static SerializedProperty GetPendingEntityPrototypeRoot(bool clear = false) {
      var container = QuantumEditorUtilityContainer.instance;
      if (clear) {
        container.PendingPrototype = new FlatEntityPrototypeContainer();
      }

      var so = new SerializedObject(container);
      var rootProperty = so.FindPropertyOrThrow(nameof(container.PendingPrototype));
      return rootProperty;
    }

    public static EntityPrototype FinishPendingEntityPrototype() {
      var container = QuantumEditorUtilityContainer.instance;
      var flatContainer = container.PendingPrototype;

      var prototypes = new List<ComponentPrototype>();
      flatContainer.Collect(prototypes);
      container.PendingPrototype = new FlatEntityPrototypeContainer();

      return new EntityPrototype() {
        Container = new EntityPrototypeContainer() {
          Components = prototypes.ToArray()
        }
      };
    }

    public static Color GetPersistentColor(string str) {
      return GeneratePastelColor(GetPersistentHashCode(str));
    }

    public const int PersistentHashCodeStart = 5381;

    public static int GetPersistentHashCode(string str, int prevHash = PersistentHashCodeStart) {
      int hash = prevHash;
      int len = str.Length;
      fixed (char* c = str) {
        for (int i = 0; i < len; ++i) {
          hash = hash * 33 + c[i].GetHashCode();
        }
      }
      return hash;
    }

    public static object MakeKnownObjectSerializable(object obj) {
      if (obj is AssetObject asset) {
        if (asset is Quantum.EntityPrototype ep) {
          var surrogate = new EntityPrototypeSurrogate() {
            Identifier = asset.Identifier,
            Container = new FlatEntityPrototypeContainer()
          };
          surrogate.Container.Store(ep.Container.Components);
          return surrogate;
        } else if (asset is Quantum.Map map) {
          var surrogate = new MapSurrogate() {
            Map = map,
            MapEntities = new List<FlatEntityPrototypeContainer>()
          };

          foreach (var mapEntity in map.MapEntities) {
            var c = new FlatEntityPrototypeContainer();
            c.Store(mapEntity.Components);
            surrogate.MapEntities.Add(c);
          }

          return surrogate;
        } else {
          return asset;
        }
      } else if (obj is RuntimeConfig) {
        return obj;
      } else if (obj is DeterministicSessionConfig) {
        return obj;
      } else {
        throw new ArgumentException(nameof(obj));
      }
    }

    public static void TraverseDump(string dump, Func<string, bool> onEnter, Action onExit, Action<string, string> onValue) {
      using (var reader = new StringReader(dump)) {
        Debug.Assert(reader.ReadLine() == "#ROOT:");

        int groupDepth = 1;
        int ignoreDepth = int.MaxValue;

        for (string line = reader.ReadLine(); line != null; line = reader.ReadLine()) {
          var valueSplitter = line.IndexOf(':');
          if (valueSplitter < 0) {
            Debug.LogWarning($"Invalid line format: {line}");
            continue;
          }

          int indent = 0;
          while (indent < line.Length && line[indent] == ' ')
            ++indent;

          Debug.Assert(indent >= 2);
          Debug.Assert(indent % 2 == 0);
          var depth = indent / 2;

          if (depth >= ignoreDepth)
            continue;

          ignoreDepth = int.MaxValue;

          if (depth > groupDepth) {
            for (int i = groupDepth + 1; i < depth; ++i) {
              Debug.LogError("Missing node at " + i);
              if (onEnter("???")) {
                groupDepth = i;
              } else {
                ignoreDepth = i;
                break;
              }
            }

            if (ignoreDepth < int.MaxValue) {
              continue;
            }
          } else if (depth < groupDepth) {
            Debug.Assert(depth > 0);
            while (groupDepth > depth) {
              --groupDepth;
              onExit();
            }
          }

          var name = line.Substring(indent, valueSplitter - indent);
          bool hasValue = false;
          int valueIndex = valueSplitter;
          while (!hasValue && ++valueIndex < line.Length) {
            hasValue = !char.IsWhiteSpace(line[valueIndex]);
          }

          Debug.Assert(groupDepth == depth);
          if (hasValue) {
            onValue(name, line.Substring(valueIndex));
          } else {
            if (onEnter(name)) {
              groupDepth = depth + 1;
            } else {
              ignoreDepth = depth + 1;
            }
          }
        }

        while (--groupDepth > 0) {
          onExit();
        }
      }
    }

    public static bool TryParseAssetRef(string str, out AssetRef assetRef) {
      var match = Statics.AssetRefRegex.Match(str);
      if (match.Success && AssetGuid.TryParse(match.Groups[1].Value, out var guid, includeBrackets: false)) {
        assetRef = new AssetRef() {
          Id = guid
        };
        return true;
      } else {
        assetRef = default;
        return false;
      }
    }

    [Serializable]
    public sealed class HierarchicalFoldoutCache {
      public List<int> ExpandedPathIDs = new List<int>();

      private const string PathSplitter = "/";
      private Stack<int> _pathHash = new Stack<int>();
      public void BeginPathTraversal() {
        _pathHash.Clear();
        _pathHash.Push(PersistentHashCodeStart);
      }

      public bool IsPathExpanded(int id) {
        return ExpandedPathIDs.BinarySearch(id) >= 0;
      }

      public void PopNestedPath() {
        _pathHash.Pop();
      }

      public int PushNestedPath(string name) {
        var parentHash = _pathHash.Peek();
        var hash = GetPersistentHashCode(PathSplitter, parentHash);
        hash = GetPersistentHashCode(name, hash);
        _pathHash.Push(hash);
        return hash;
      }
      public void SetPathExpanded(int id, bool expanded) {
        var index = ExpandedPathIDs.BinarySearch(id);
        if (expanded) {
          if (index < 0) {
            ExpandedPathIDs.Insert(~index, id);
          }
        } else {
          if (index >= 0) {
            ExpandedPathIDs.RemoveAt(index);
          }
        }
      }
    }

    private static Color GeneratePastelColor(int seed) {
      var rng = new System.Random(seed);
      int r = rng.Next(256) + 128;
      int g = rng.Next(256) + 128;
      int b = rng.Next(256) + 128;

      r = Mathf.Min(r / 2, 255);
      g = Mathf.Min(g / 2, 255);
      b = Mathf.Min(b / 2, 255);

      var result = new Color32((byte)r, (byte)g, (byte)b, 255);
      return result;
    }

    private static class Statics {
      public static readonly Regex AssetRefRegex = new Regex(@"^\s*Id: \[([A-Fa-f\d]+)\]\s*$", RegexOptions.Compiled);

      public static readonly Dictionary<Type, ComponentEntry> Components;
      public static readonly Dictionary<string, ComponentEntry> ComponentsByName;
      public static readonly Dictionary<string, ComponentEntry> ComponentsByAQName;
      public static readonly FramePrinter Printer = new FramePrinter();

      public static readonly Dictionary<Type, AssetEntry> Assets;
      public static readonly Dictionary<string, AssetEntry> AssetsByAQName;

      static Statics() {
        Frame.InitStatic();

        Components = ComponentTypeId.Type.Where(x => x != null).ToDictionary(x => x, x => CreateComponentEntry(x));
        ComponentsByName = Components.ToDictionary(x => x.Key.Name, x => x.Value);
        ComponentsByAQName = Components.ToDictionary(x => x.Key.AssemblyQualifiedName, x => x.Value);

        Assets = AppDomain.CurrentDomain.GetAssemblies()
          .SelectMany(x => x.GetLoadableTypes())
          .Where(x => x?.IsSubclassOf(typeof(AssetObject)) == true)
          .ToDictionary(x => x, x => CreateAssetEntry(x));

        AssetsByAQName = Assets.ToDictionary(x => x.Key.AssemblyQualifiedName, x => x.Value);
      }

      private static AssetEntry CreateAssetEntry(Type type) {
        return new AssetEntry() {
          AssetType = type,
          DisplayName = new GUIContent(type.Name, type.FullName),
          ThumbnailColor = GetPersistentColor(type.FullName),
          ThumbnailContent = new GUIContent(GenerateAcronym(type.Name), type.FullName)
        };
      }

      private static ComponentEntry CreateComponentEntry(Type type) {
        Debug.Assert(type.GetInterface(typeof(IComponent).FullName) != null);

        var result = new ComponentEntry() {
          ThumbnailColor = GetPersistentColor(type.FullName),
          ComponentType = type,
          DisplayName = new GUIContent(type.Name, type.FullName),
          Id = ComponentTypeId.GetComponentIndex(type),
        };


        result.PrototypeType = type.Assembly.GetType($"Quantum.Prototypes.{type.Name}_Prototype");
        if (result.PrototypeType != null) {
          var prototypeScriptName = $"EntityComponent{type.Name}";
          var monoScript = (MonoScript)UnityInternal.EditorGUIUtility.GetScript(prototypeScriptName);
          if (monoScript != null) {
            result.UnityPrototypeComponentType = monoScript.GetClass();
            result.CustomIcon = UnityInternal.EditorGUIUtility.GetIconForObject(monoScript);
            if (result.CustomIcon != null) {
              result.ThumbnailContent = new GUIContent(result.CustomIcon, type.Name);
              result.ThumbnailColor = Color.white;
            }
          }
        }

        if (result.ThumbnailContent == null) {
          result.ThumbnailContent = new GUIContent(GenerateAcronym(type.Name), type.Name);
        }

        return result;
      }
    }

    private class ComponentEntry {
      public Type ComponentType;
      public Texture2D CustomIcon;
      public GUIContent DisplayName;
      public Type PrototypeType;
      public Color ThumbnailColor;
      public GUIContent ThumbnailContent;
      public Type UnityPrototypeComponentType;
      public int Id;
    }

    private class AssetEntry {
      public Type AssetType;
      public GUIContent DisplayName;
      public Color ThumbnailColor;
      public GUIContent ThumbnailContent;
    }


    [Serializable]
    public sealed class EntityPrototypeSurrogate {
      public FlatEntityPrototypeContainer Container;
      public AssetObjectIdentifier Identifier;
    }

    [Serializable]
    public sealed class MapSurrogate {
      public Quantum.Map Map;
      public List<FlatEntityPrototypeContainer> MapEntities;
    }

    public abstract class SerializableObjectsContainerBase {
      public DeterministicSessionConfig[] DeterministicSessionConfig = { };
      public EntityPrototypeSurrogate[] EntityPrototypeSurrogate = { };
      public MapSurrogate[] MapSurrogate = { };
      public RuntimeConfig[] RuntimeConfig = { };

      internal void Store(object obj) {
        var typeName = obj.GetType().Name;
        var field = GetType().GetFieldOrThrow(typeName);
        var value = Array.CreateInstance(obj.GetType(), 1);
        value.SetValue(obj, 0);
        field.SetValue(this, value);
      }
    }
  }
}
#endregion

#region quantum_unity/Assets/Photon/Quantum/Editor/EditorGUI/QuantumSimulationObjectInspector.cs
namespace Quantum.Editor {
  using System;
  using UnityEditor;
  using UnityEngine;

  using StateNodeType = QuantumSimulationObjectInspectorState.NodeType;

  [Serializable]
  public class QuantumSimulationObjectInspector {
    private static Lazy<Skin> _skin = new Lazy<Skin>(() => new Skin());

    [SerializeField]
    private QuantumEditorUtility.HierarchicalFoldoutCache _foldoutCache = new QuantumEditorUtility.HierarchicalFoldoutCache();
    [SerializeField]
    private Vector2 _scroll = default;

    private static Skin skin => _skin.Value;

    public Action<Rect, EntityRef, string> ComponentMenu;
    public Action<Rect, EntityRef> EntityMenu;


    public void DoGUILayout(QuantumSimulationObjectInspectorState inspectorState, bool useScrollView = false) {

      _foldoutCache.BeginPathTraversal();

      if (inspectorState == null) {
        return;
      }

      using (var scrollScope = useScrollView ? new EditorGUILayout.ScrollViewScope(_scroll) : null) {
        _scroll = scrollScope?.scrollPosition ?? default;

        int originalIndent = EditorGUI.indentLevel;
        bool originalHierarchyMode = EditorGUIUtility.hierarchyMode;
        int depth = 0;

        EntityRef currentEntity = new EntityRef() {
          Index = inspectorState.EntityRefIndex,
          Version = inspectorState.EntityRefVersion
        };

        if (!string.IsNullOrEmpty(inspectorState.ExceptionString)) {
          EditorGUILayout.HelpBox(inspectorState.ExceptionString, MessageType.Error, true);
        }

        using (new QuantumEditorGUI.HierarchyModeScope(true)) {
          int expectedEndGroupCount = 0;

          using (new GUILayout.VerticalScope()) {
            for (int nodeIndex = 0; nodeIndex < inspectorState.Nodes.Count; ++nodeIndex) {
              var node = inspectorState.Nodes[nodeIndex];

              // this skips folded groups
              {
                if (expectedEndGroupCount > 0) {
                  if ((node.Type & StateNodeType.ScopeBeginFlag) == StateNodeType.ScopeBeginFlag) {
                    ++expectedEndGroupCount;
                    continue;
                  } else if (node.Type == StateNodeType.ScopeEnd) {
                    --expectedEndGroupCount;
                  }
                }
                if (expectedEndGroupCount > 0)
                  continue;
              }

              if ((node.Type & StateNodeType.ScopeBeginFlag) == StateNodeType.ScopeBeginFlag) {
                float labelOffset = 0;
                Action<Rect> drawThumbnail = null;
                Action<Rect> doMenu = null;

                if ((node.Type & StateNodeType.ComponentScopeBegin) == StateNodeType.ComponentScopeBegin) {
                  labelOffset = 30;
                  drawThumbnail = r => QuantumEditorGUI.ComponentThumbnailPrefix(r, node.Name);
                  if (ComponentMenu != null) {
                    doMenu = r => ComponentMenu(r, currentEntity, node.Name);
                  }
                } else if ((node.Type & StateNodeType.EntityRefScopeBegin) == StateNodeType.EntityRefScopeBegin) {
                  if (EntityMenu != null) {
                    doMenu = r => EntityMenu(r, currentEntity);
                  }
                }

                if (!BeginFoldout(node.Name, depth++, labelOffset, drawThumbnail, doMenu, placeholder: (node.Type & StateNodeType.PlaceholderFlag) == StateNodeType.PlaceholderFlag)) {
                  expectedEndGroupCount = 1;
                }
              } else if (node.Type == StateNodeType.ScopeEnd) {
                EndFoldout(--depth);
              } else if (node.Type == StateNodeType.FramePrinterDump) {
                QuantumEditorUtility.TraverseDump(node.Value,
                  name => {
                    var pathId = _foldoutCache.PushNestedPath(name);
                    var result = EditorGUILayout.Foldout(_foldoutCache.IsPathExpanded(pathId), name);
                    _foldoutCache.SetPathExpanded(pathId, result);
                    if (result) {
                      ++EditorGUI.indentLevel;
                    } else {
                      _foldoutCache.PopNestedPath();
                    }
                    return result;
                  },
                  () => {
                    _foldoutCache.PopNestedPath();
                    --EditorGUI.indentLevel;
                  },
                  (name, value) => {
                    DrawValue(name, value);
                  });
              } else if (node.Type == StateNodeType.SerializableTypeDump) {
                var type = Type.GetType(node.Name);
                if (type == null) {
                  EditorGUILayout.HelpBox($"Unknown type: {node.Name}", MessageType.Error);
                } else {
                  try {
                    var obj = JsonUtility.FromJson(node.Value, type);
                    var sp = QuantumEditorUtility.GetKnownObjectRoot(obj);
                    QuantumEditorGUI.Inspector(sp);
                  } catch (Exception ex) {
                    EditorGUILayout.TextArea(node.Value, EditorStyles.wordWrappedLabel, GUILayout.ExpandHeight(true));
                    EditorGUILayout.HelpBox(ex.ToString(), MessageType.Error);
                    GUILayout.FlexibleSpace();
                  }
                }
              } else {
                Debug.Assert(node.Type == StateNodeType.Value);
                DrawValue(node.Name, node.Value);
              }
            }
          }
        }

        Debug.Assert(originalIndent == EditorGUI.indentLevel, $"{originalIndent} {EditorGUI.indentLevel}");
        Debug.Assert(depth == 0);
      }
    }

    private static void DrawValue(string name, string value) {
      var rect = EditorGUILayout.GetControlRect(true);
      rect = EditorGUI.PrefixLabel(rect, new GUIContent(name));
      using (new EditorGUI.IndentLevelScope(-EditorGUI.indentLevel)) {
        if (QuantumEditorUtility.TryParseAssetRef(value, out var assetRef)) {
          var halfRect = rect.SetWidth(rect.width / 2);
          EditorGUI.SelectableLabel(halfRect, value);
          AssetRefDrawer.DrawAsset(halfRect.AddX(halfRect.width), assetRef.Id);
        } else {
          EditorGUI.TextField(rect, value);
        }
      }
    }

    private bool BeginFoldout(string label, int depth, float thumbnailWidth = 0, Action<Rect> drawThumbnail = null, Action<Rect> doMenu = null, bool placeholder = false) {
      var pathId = _foldoutCache.PushNestedPath(label);
      bool isExpanded = _foldoutCache.IsPathExpanded(pathId);

      bool foldout;

      if (depth == 0) {
        using (new QuantumEditorGUI.HierarchyModeScope(false)) {

          var style = skin.foldoutHeader;

          var rect = GUILayoutUtility.GetRect(GUIContent.none, style);
          using (style.ContentOffsetScope(style.contentOffset + new Vector2(thumbnailWidth, 0))) {
            using (style.FontStyleScope(italic: placeholder)) {
              foldout = BeginFoldoutHeaderGroup(rect, isExpanded, label, style: style, menuAction: doMenu);
            }

            if (drawThumbnail != null) {
              var thumbnailRect = EditorGUI.IndentedRect(rect).AddX(skin.foldoutWidth).SetWidth(thumbnailWidth);
              drawThumbnail(thumbnailRect);
            }
          }
        }
      } 
      else 
      {
        foldout = EditorGUILayout.Foldout(isExpanded, label, true);
      }

      ++EditorGUI.indentLevel;

      _foldoutCache.SetPathExpanded(pathId, foldout);
      return foldout;
    }

    private bool BeginFoldoutHeaderGroup(Rect rect, bool isExpanded, string label, GUIStyle style, Action<Rect> menuAction) {
      var indentedRect = EditorGUI.IndentedRect(rect);

#if UNITY_2019_3_OR_NEWER
      bool foldout = EditorGUI.BeginFoldoutHeaderGroup(rect, isExpanded, label, style: style, menuAction: menuAction);

      if (Event.current.type == EventType.Repaint) {
        // the titlebar style seems special and doesn't have the foldout; it needs to be drawn manually
        skin.foldoutHeaderToggle.Draw(rect, false, false, foldout, false);
      }

      EditorGUILayout.EndFoldoutHeaderGroup();
      return foldout;

#else

      if (Event.current.type == EventType.Repaint) {
        style.Draw(indentedRect, new GUIContent(label), false, false, false, false);
      }


      if ( menuAction != null ) {
        QuantumEditorGUI.HandleContextMenu(rect, menuAction, true);
      } 

      return EditorGUI.Foldout(rect, isExpanded, GUIContent.none, true);
#endif
    }

    private void EndFoldout(int depth) {
      --EditorGUI.indentLevel;
      _foldoutCache.PopNestedPath();
    }

    

    private sealed class Skin {

      public readonly GUIContent[] componentMenuItems = new[] {
        new GUIContent("Remove Component"),
      };

      public readonly GUIContent[] entityMenuItems = new[] {
        new GUIContent("Remove Entity"),
        new GUIContent("Add Components...")
      };

      public readonly GUIStyle foldoutHeader = new GUIStyle(UnityInternal.Styles.InspectorTitlebar) {
        alignment = TextAnchor.MiddleLeft,
      };

      public readonly GUIStyle foldoutHeaderToggle = UnityInternal.Styles.FoldoutTitlebar;

      public readonly GUIStyle foldoutHeaderWithOffset = new GUIStyle(UnityInternal.Styles.InspectorTitlebar) {
        alignment = TextAnchor.MiddleLeft,
        contentOffset = new Vector2(30, 0),
      };
      public float foldoutWidth => 13;
      public float minThumbnailWidth => 30;
    }
  }
}
#endregion

#region quantum_unity/Assets/Photon/Quantum/Editor/EditorGUI/QuantumSimulationObjectInspectorState.cs
namespace Quantum.Editor {
  using System;
  using System.Collections.Generic;
  using System.Text;
  using Photon.Deterministic;
  using Quantum.Core;
  using UnityEngine;

  [Serializable]
  public sealed class QuantumSimulationObjectInspectorState {

    [Flags]
    public enum NodeType {
      Value = 1 << 0,
      ScopeEnd = 1 << 1,
      ScopeBeginFlag = 1 << 2,
      PlaceholderFlag = 1 << 10,

      StructScopeBegin = (1 << 3) | ScopeBeginFlag,
      EntityRefScopeBegin = (1 << 4) | ScopeBeginFlag,
      ComponentScopeBegin = (1 << 5) | ScopeBeginFlag,

      FramePrinterDump = 1 << 7,
      SerializableTypeDump = 1 << 8,
    }

    [Serializable]
    public struct Node {
      public string Name;
      public string Value;
      public NodeType Type;
    }

    

    public List<Node> Nodes = new List<Node>();
    public int EntityRefVersion;
    public int EntityRefIndex;
    public AssetGuid AssetGuid;
    public string SerializableClassesContainerJson;
    public string ExceptionString;

    public bool FromEntity(FrameBase frame, EntityRef entityRef) {
      try {
        Clear();

        EntityRefIndex = entityRef.Index;
        EntityRefVersion = entityRef.Version;

        BeginEntityRefScope(entityRef);
        try {
          AddValue("Value", entityRef);
        } finally {
          EndScope();
        }

        unsafe {
          for (int componentTypeIndex = 1; componentTypeIndex < ComponentTypeId.Type.Length; componentTypeIndex++) {
            if (frame.Has(entityRef, componentTypeIndex)) {
              var componentType = ComponentTypeId.Type[componentTypeIndex];

              var componentPtr = QuantumEditorUtility.GetComponentPointer(frame, entityRef, componentType);
              BeginComponentScope(componentType);
              try {
                AddInlineDump(frame, componentPtr, componentType);
              } finally {
                EndScope();
              }
            }
          }
        }
        return true;
      } catch (Exception ex) {
        return NotifyException(ex);
      }
    }

    public unsafe bool FromStruct(FrameBase frame, string name, void* ptr, Type type) {
      Clear();
      try {
        BeginStructScope(name);
        try {
          AddInlineDump(frame, ptr, type);
        } finally {
          EndScope();
        }
        return true;
      } catch (Exception ex) {
        return NotifyException(ex);
      }
    }

    public bool FromAsset(AssetObject asset) {
      Clear();
      try {
        var assetName = asset.GetType().Name;

        BeginStructScope(assetName);
        try {
          AddKnowObjectTypeJsonDump(asset);
        } finally {
          EndScope();
        }
        return true;
      } catch (Exception ex) {
        return NotifyException(ex);
      }
    }

    public unsafe bool FromSession(DeterministicSession session) {
      Clear();
      try {
        BeginStructScope("DeterministicSession");
        try {
          AddValue("MaxVerifiedTicksPerUpdate", session.MaxVerifiedTicksPerUpdate);
          AddValue("AccumulatedTime", session.AccumulatedTime);
          AddValue("InitialTick", session.InitialTick);
          AddValue("TimeScale", session.TimeScale);
          AddValue("LocalInputOffset", session.LocalInputOffset);
          AddValue("PredictedFrames", session.PredictedFrames);
          AddValue("SimulationTimeElasped", session.SimulationTimeElasped);
          AddValue("GameMode", session.GameMode);
          AddValue("IsStalling", session.IsStalling);
          AddValue("IsPaused", session.IsPaused);
          AddValue("IsLockstep", session.IsLockstep);
          AddValue("IsReplayFinished", session.IsReplayFinished);

          AddValue("Predicted Frame #", session.FramePredicted.Number);
          AddValue("Verified Frame #", session.FrameVerified.Number);

          BeginStructScope("LocalPlayerIndices");
          try {
            int index = 0;
            foreach (var value in session.LocalPlayerIndices) {
              AddValue($"[{index++}]", value);
            }
          } finally {
            EndScope();
          }

          BeginStructScope("PlatformInfo");
          try {
            AddValue("Architecture", session.PlatformInfo.Architecture);
            AddValue("Platform", session.PlatformInfo.Platform);
            AddValue("RuntimeHost", session.PlatformInfo.RuntimeHost);
            AddValue("Runtime", session.PlatformInfo.Runtime);
            AddValue("CoreCount", session.PlatformInfo.CoreCount);
            AddValue("Allocator", session.PlatformInfo.Allocator?.GetType().FullName);
            AddValue("TaskRunner", session.PlatformInfo.TaskRunner?.GetType().FullName);
          } finally {
            EndScope();
          }

          BeginStructScope("Stats");
          try {
            AddValue("Ping", session.Stats.Ping);
            AddValue("Frame", session.Stats.Frame);
            AddValue("Offset", session.Stats.Offset);
            AddValue("Predicted", session.Stats.Predicted);
            AddValue("ResimulatedFrames", session.Stats.ResimulatedFrames);
            AddValue("UpdateTime", session.Stats.UpdateTime);
          } finally {
            EndScope();
          }

        } finally {
          EndScope();
        }

        session.GetLocalConfigs(out var sessionConfig, out var runtimeConfig);

        BeginStructScope("DeterministicSessionConfig");
        try {
          AddKnowObjectTypeJsonDump(sessionConfig);
        } finally {
          EndScope();
        }

        BeginStructScope("RuntimeConfig");
        try {
          AddKnowObjectTypeJsonDump(RuntimeConfig.FromByteArray(runtimeConfig));
        } finally {
          EndScope();
        }

        return true;
      } catch (Exception ex) {
        return NotifyException(ex);
      }
    }

    internal void Remove(string path) {
      var hash = QuantumEditorUtility.GetPersistentHashCode(path);

      StringBuilder pathBuilder = new StringBuilder();

      for (int nodeIndex = 0; nodeIndex < Nodes.Count; ++nodeIndex) {
        var node = Nodes[nodeIndex];

        if ((node.Type & NodeType.ScopeBeginFlag) == NodeType.ScopeBeginFlag) {
          pathBuilder.Append("/").Append(node.Name);
          if (pathBuilder.ToString() == path) {
            int expectedEndGroupCount = 1;
            int removeCount = 1;

            for (int j = nodeIndex + 1; j < Nodes.Count && expectedEndGroupCount > 0; ++j, ++removeCount) {
              var n = Nodes[j];
              if ((n.Type & NodeType.ScopeBeginFlag) == NodeType.ScopeBeginFlag) {
                ++expectedEndGroupCount;
              } else if (n.Type == NodeType.ScopeEnd) {
                --expectedEndGroupCount;
              }
            }

            Nodes.RemoveRange(nodeIndex, removeCount);
            return;
          }
        } else if (node.Type == NodeType.ScopeEnd) {
          var idx = pathBuilder.ToString().LastIndexOf('/');
          pathBuilder.Remove(idx, pathBuilder.Length - idx);
        } else {
          pathBuilder.Append("/").Append(node.Name);
          if ( pathBuilder.ToString() == path ) {
            Nodes.RemoveAt(nodeIndex);
            return;
          }
          var idx = pathBuilder.ToString().LastIndexOf('/');
          pathBuilder.Remove(idx, pathBuilder.Length - idx);
        }
      }
    }

    public void FromException(Exception ex) {
      Clear();
      ExceptionString = ex.ToString();
    }

    private bool NotifyException(Exception ex) {
      ExceptionString = ex.ToString();
      return false;
    }


    private void Clear() {
      Nodes.Clear();
      EntityRefVersion = 0;
      EntityRefIndex = 0;
      SerializableClassesContainerJson = string.Empty;
      AssetGuid = default;
      ExceptionString = string.Empty;
    }

    private void BeginStructScope(string name) {
      Nodes.Add(new Node() {
        Name = name,
        Type = NodeType.StructScopeBegin,
      });
    }

    private void BeginComponentScope(Type componentType) {
      Nodes.Add(new Node() {
        Name = componentType.Name,
        Type = NodeType.ComponentScopeBegin,
      });
    }

    private void BeginEntityRefScope(EntityRef entityRef) {
      Nodes.Add(new Node() {
        Name = "Entity",
        Type = NodeType.EntityRefScopeBegin,
      });
    }

    private void EndScope() {
      Nodes.Add(new Node() {
        Type = NodeType.ScopeEnd
      });
    }

    private unsafe void AddKnowObjectTypeJsonDump(object obj) {
      var serializableObject = QuantumEditorUtility.MakeKnownObjectSerializable(obj);
      var json = JsonUtility.ToJson(serializableObject);
      Nodes.Add(new Node() {
        Name = serializableObject.GetType().AssemblyQualifiedName,
        Type = NodeType.SerializableTypeDump,
        Value = json
      });
    }

    private unsafe void AddInlineDump(FrameBase frame, void* ptr, Type type) {
      var dump = QuantumEditorUtility.DumpPointer(frame, ptr, type);
      Nodes.Add(new Node() {
        Name = type.FullName,
        Type = NodeType.FramePrinterDump,
        Value = dump
      });
    }

    private void AddValue(string name, string value) {
      Nodes.Add(new Node() {
        Name = name,
        Type = NodeType.Value,
        Value = value
      });
    }

    private void AddValue<T>(string name, T value) {
      AddValue(name, value.ToString());
    }

    public void AddComponentPlaceholder(string componentTypeName) {
      Nodes.Add(new Node() {
        Name = componentTypeName,
        Type = NodeType.ComponentScopeBegin | NodeType.PlaceholderFlag,
      });
      EndScope();
    }
  }
}

#endregion