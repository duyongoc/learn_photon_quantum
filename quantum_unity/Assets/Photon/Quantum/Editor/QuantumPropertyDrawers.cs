

#region quantum_unity/Assets/Photon/Quantum/Editor/PropertyDrawers/AssetGuidDrawer.cs
namespace Quantum.Editor {

  using UnityEditor;
  using UnityEngine;

  [CustomPropertyDrawer(typeof(AssetGuid))]
  public unsafe class AssetGuidDrawer : PropertyDrawer {
    private bool hadError = false;

    public override void OnGUI(Rect rect, SerializedProperty prop, GUIContent label) {
      using (new QuantumEditorGUI.PropertyScope(rect, label, prop)) {
        var valueProp = prop.FindPropertyRelativeOrThrow(nameof(AssetGuid.Value));
        var str = new AssetGuid(valueProp.longValue).ToString(false);
        EditorGUI.BeginChangeCheck();

        rect = EditorGUI.PrefixLabel(rect, label);

        using (new EditorGUI.IndentLevelScope(-EditorGUI.indentLevel)) {
          var id = GUIUtility.GetControlID(UnityInternal.EditorGUI.TextFieldHash, FocusType.Keyboard, rect);
          str = UnityInternal.EditorGUI.TextFieldInternal(id, rect, str, EditorStyles.textField);

          if (GUIUtility.keyboardControl != id) {
            hadError = false;
          }

          if (EditorGUI.EndChangeCheck()) {
            if (AssetGuid.TryParse(str, out var guid, includeBrackets: false)) {
              hadError = false;
              valueProp.longValue = guid.Value;
              prop.serializedObject.ApplyModifiedProperties();
            } else {
              hadError = true;
            }
          }

          if (hadError) {
            QuantumEditorGUI.Decorate(rect, "Failed", MessageType.Error);
          }
        }
      }
    }
  }
}
#endregion

#region quantum_unity/Assets/Photon/Quantum/Editor/PropertyDrawers/AssetObjectIdentifierDrawer.cs
namespace Quantum.Editor {

  using UnityEditor;
  using UnityEngine;

  [CustomPropertyDrawer(typeof(AssetObjectIdentifier))]
  public class AssetObjectIdentifierDrawer : PropertyDrawer {
    private bool _editPath;
    private bool _editGuid;

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label) {
      return QuantumEditorGUI.GetLinesHeight(2);
    }

    public override void OnGUI(Rect p, SerializedProperty prop, GUIContent label) {
      const float ButtonSize = 40;
      p = p.SetLineHeight();

      using (new EditorGUI.DisabledScope(!_editPath)) {
        var pathRect = p;
        pathRect.xMax -= ButtonSize + 5;
        var guid = prop.FindPropertyRelative("Path");
        EditorGUI.PropertyField(pathRect, guid, false);
      }

      var buttonRect   = p;
      buttonRect.xMin  = p.xMax - ButtonSize;
      buttonRect.width = ButtonSize;
      if (GUI.Button(buttonRect, new GUIContent("Edit", "Set the field edit-able to copy the path for example."))) {
        _editPath = !_editPath;
      }

      using (new EditorGUI.DisabledScope(!_editGuid)) {
        p = p.AddLine();
        var guidRect = p;
        guidRect.xMax -= ButtonSize + 5;
        var guid64 = prop.FindPropertyRelative("Guid");
        EditorGUI.PropertyField(guidRect, guid64, false);
      }

      buttonRect       = p;
      buttonRect.xMin  = p.xMax - ButtonSize;
      buttonRect.width = ButtonSize;
      if (GUI.Button(buttonRect, new GUIContent("Edit", "Set the field edit-able set a certain guid (not recommended)."))) {
        _editGuid = !_editGuid;
      }
    }
  }
}
#endregion

#region quantum_unity/Assets/Photon/Quantum/Editor/PropertyDrawers/AssetRefDrawer.cs
namespace Quantum.Editor {
  using System;
  using UnityEditor;
  using UnityEngine;

  [CustomPropertyDrawer(typeof(AssetRef))]
  public class AssetRefDrawer : PropertyDrawer {

    public static string RawValuePath = SerializedObjectExtensions.GetPropertyPath((AssetRef x) => x.Id.Value);

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
      DrawAssetRefSelector(position, property, label);
    }

    public static unsafe void DrawAssetRefSelector(Rect position, SerializedProperty property, GUIContent label, Type type = null, Func<AssetBase> createAssetCallback = null) {
      type = type ?? typeof(AssetBase);

      var valueProperty = property.FindPropertyRelativeOrThrow(RawValuePath);
      var guid = (AssetGuid)valueProperty.longValue;

      var currentObject = default(AssetBase);
      if (guid.IsValid) {
        currentObject = UnityDB.FindAssetForInspector(guid);
      }

      EditorGUI.BeginChangeCheck();
      EditorGUI.BeginProperty(position, label, valueProperty);

      AssetBase selected = null;

      if (valueProperty.hasMultipleDifferentValues) {
        selected = EditorGUI.ObjectField(position, label, null, type, false) as AssetBase;
      } else if (currentObject == null && !guid.IsValid) {
        position.width -= 25;
        selected = EditorGUI.ObjectField(position, label, null, type, false) as AssetBase;
        var buttonPosition = position.AddX(position.width).SetWidth(20);
        using (new EditorGUI.DisabledScope(type.IsAbstract)) {
          if (GUI.Button(buttonPosition, "+", EditorStyles.miniButton)) {
            if (createAssetCallback != null) {
              selected = createAssetCallback();
            } else {
              selected = ScriptableObject.CreateInstance(type) as AssetBase;
              var assetPath = string.Format($"{QuantumEditorSettings.Instance.DefaultAssetSearchPath}/{type.ToString()}.asset");
              assetPath = AssetDatabase.GenerateUniqueAssetPath(assetPath);
              AssetDatabase.CreateAsset(selected, assetPath);
              AssetDatabase.SaveAssets();
            }
          }
        }
      } else {
        var rect = EditorGUI.PrefixLabel(position, label);
        using (new EditorGUI.IndentLevelScope(-EditorGUI.indentLevel)) {
          selected = DrawAsset(rect, guid, type);
        }
      }

      EditorGUI.EndProperty();

      if (EditorGUI.EndChangeCheck()) {
        valueProperty.longValue = selected != null ? selected.AssetObject.Guid.Value : 0L;
      }
    }

    private static Func<AssetBase, Boolean> ObjectFilter(AssetGuid guid, Type type) {
      return
        obj => obj &&
        type.IsInstanceOfType(obj) &&
        obj.AssetObject != null &&
        obj.AssetObject.Guid == guid;
    }

    internal static AssetBase DrawAsset(Rect position, AssetGuid assetGuid, Type assetType = null) {

      assetType = assetType ?? typeof(AssetBase);
      Debug.Assert(assetType.IsSubclassOf(typeof(AssetBase)) || assetType == typeof(AssetBase));
      

      if (!assetGuid.IsValid) {
        return (AssetBase)EditorGUI.ObjectField(position, null, assetType, false);
      }

      if (assetGuid.IsDynamic) {
        // try to get an asset from the main runner
        var frame = QuantumRunner.Default ? QuantumRunner.Default.Game.Frames.Verified : null;
        if (frame != null) {
          var asset = frame.FindAsset<AssetObject>(assetGuid);
          if (asset != null) {
            if (EditorGUI.DropdownButton(position, new GUIContent(asset.ToString()), FocusType.Keyboard)) {
              // serialize asset
              var content = frame.Context.AssetSerializer.PrintAsset(asset);
              PopupWindow.Show(position, new TextPopupContent() { Text = content });
            }
          } else {
            EditorGUI.ObjectField(position, null, assetType, false);
            QuantumEditorGUI.Decorate(position, $"Dynamic Asset {assetGuid} not found", MessageType.Error);
          }
        } else {
          EditorGUI.ObjectField(position, null, assetType, false);
          QuantumEditorGUI.Decorate(position, $"Dynamic Asset {assetGuid} not found", MessageType.Error);
        }
        return null;
      } else {
        var asset = UnityDB.FindAssetForInspector(assetGuid);

        Type effectiveAssetType = assetType;
        if (asset != null && asset.GetType() != assetType && !asset.GetType().IsSubclassOf(assetType)) {
          effectiveAssetType = asset.GetType();
        }

        var result = EditorGUI.ObjectField(position, asset, effectiveAssetType, false);
        if (asset == null) {
          QuantumEditorGUI.Decorate(position, $"Asset {assetGuid} missing", MessageType.Error);
        } else if (effectiveAssetType != assetType) {
          QuantumEditorGUI.Decorate(position, $"Asset type mismatch: expected {assetType}, got {effectiveAssetType}", MessageType.Error);
        }

        return (AssetBase)result;
      }
    }

    private sealed class TextPopupContent : PopupWindowContent {

      public string Text;
      private Vector2? _size;
      private Vector2 _scroll;

      public override Vector2 GetWindowSize() {
        if (_size == null) {
          var size = EditorStyles.textArea.CalcSize(new GUIContent(Text));
          size.x += 25; // account for the scroll bar & margins
          size.y += 10; // account for margins
          size.x = Mathf.Min(500, size.x);
          size.y = Mathf.Min(400, size.y);
          _size = size;
        }
        return _size.Value;
      }

      public override void OnGUI(Rect rect) {

        using (new GUILayout.AreaScope(rect)) {
          using (var scroll = new EditorGUILayout.ScrollViewScope(_scroll)) {
            _scroll = scroll.scrollPosition;
            EditorGUILayout.TextArea(Text);
          }
        }
      }
    }
  }

  [CustomPropertyDrawer(typeof(AssetRefCharacterController2DConfig))]
  public class AssetRefCharacterController2DConfigPropertyDrawer : PropertyDrawer {
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
      AssetRefDrawer.DrawAssetRefSelector(position, property, label, typeof(CharacterController2DConfigAsset));
    }
  }

  [CustomPropertyDrawer(typeof(AssetRefCharacterController3DConfig))]
  public class AssetRefCharacterController3DConfigPropertyDrawer : PropertyDrawer {
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
      AssetRefDrawer.DrawAssetRefSelector(position, property, label, typeof(CharacterController3DConfigAsset));
    }
  }

  [CustomPropertyDrawer(typeof(AssetRefEntityView))]
  public class AssetRefEntityViewPropertyDrawer : PropertyDrawer {
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
      AssetRefDrawer.DrawAssetRefSelector(position, property, label, typeof(EntityViewAsset));
    }
  }

  [CustomPropertyDrawer(typeof(AssetRefMap))]
  public class AssetRefMapPropertyDrawer : PropertyDrawer {
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
      AssetRefDrawer.DrawAssetRefSelector(position, property, label, typeof(MapAsset));
    }
  }

  [CustomPropertyDrawer(typeof(AssetRefNavMesh))]
  public class AssetRefNavMeshPropertyDrawer : PropertyDrawer {
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
      AssetRefDrawer.DrawAssetRefSelector(position, property, label, typeof(NavMeshAsset));
    }
  }

  [CustomPropertyDrawer(typeof(AssetRefNavMeshAgentConfig))]
  public class AssetRefNavMeshAgentConfigPropertyDrawer : PropertyDrawer {
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
      AssetRefDrawer.DrawAssetRefSelector(position, property, label, typeof(NavMeshAgentConfigAsset));
    }
  }

  [CustomPropertyDrawer(typeof(AssetRefPhysicsMaterial))]
  public class AssetRefPhysicsMaterialPropertyDrawer : PropertyDrawer {
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
      AssetRefDrawer.DrawAssetRefSelector(position, property, label, typeof(PhysicsMaterialAsset));
    }
  }

  [CustomPropertyDrawer(typeof(AssetRefPolygonCollider))]
  public class AssetRefPolygonColliderPropertyDrawer : PropertyDrawer {
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
      AssetRefDrawer.DrawAssetRefSelector(position, property, label, typeof(PolygonColliderAsset));
    }
  }

  [CustomPropertyDrawer(typeof(AssetRefSimulationConfig))]
  public class AssetRefSimulationConfigPropertyDrawer : PropertyDrawer {
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
      AssetRefDrawer.DrawAssetRefSelector(position, property, label, typeof(SimulationConfigAsset));
    }
  }

  [CustomPropertyDrawer(typeof(AssetRefTerrainCollider))]
  public class AssetRefTerrainColliderPropertyDrawer : PropertyDrawer {
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
      AssetRefDrawer.DrawAssetRefSelector(position, property, label, typeof(TerrainColliderAsset));
    }
  }

  [CustomPropertyDrawer(typeof(AssetRefBinaryData))]
  public class AssetRefBinaryDataDrawer : PropertyDrawer {
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
      AssetRefDrawer.DrawAssetRefSelector(position, property, label, typeof(BinaryDataAsset));
    }
  }
}
#endregion

#region quantum_unity/Assets/Photon/Quantum/Editor/PropertyDrawers/ComponentPrototypeRefWrapperDrawer.cs
namespace Quantum.Editor {

  using System;
  using System.Linq;
  using UnityEditor;
  using UnityEngine;

  [CustomPropertyDrawer(typeof(ComponentPrototypeRefWrapper))]
  public class ComponentPrototypeRefWrapperDrawer : PropertyDrawer {

    //private static Dictionary<string, Type> _quantumTypeNameToUnityType = Unit

    public static void DrawMultiField(Rect position, SerializedProperty property, GUIContent label, string componentTypeName, bool hasComponentTypeName = false) {
      var sceneReferenceProperty = property.FindPropertyRelativeOrThrow("_scenePrototype");
      var assetRefProperty = property.FindPropertyRelativeOrThrow(nameof(ComponentPrototypeRefWrapper.AssetPrototype));
      var assetRefValueProperty = assetRefProperty.FindPropertyRelative("Id.Value");
      var assetRefValue = new AssetGuid(assetRefValueProperty.longValue);

      using (new QuantumEditorGUI.PropertyScope(position, label, property)) {
        var rect = EditorGUI.PrefixLabel(position, label);

        bool showAssetRef = assetRefValue.IsValid || sceneReferenceProperty.objectReferenceValue == null;
        bool showReference = sceneReferenceProperty.objectReferenceValue != null || !assetRefValue.IsValid;
        bool showTypeCombo = hasComponentTypeName & !showReference;

        Debug.Assert(showAssetRef || showReference);

        if (showAssetRef && showReference) {
          rect.width /= 2;
        } else if (showTypeCombo) {
          rect.width /= 2;
        }

        if (showReference) {
          EditorGUI.BeginChangeCheck();
          using (new EditorGUI.IndentLevelScope(-EditorGUI.indentLevel)) {
            EditorGUI.PropertyField(rect, sceneReferenceProperty, GUIContent.none);
          }

          rect.x += rect.width;

          if (EditorGUI.EndChangeCheck()) {
            assetRefValueProperty.longValue = 0;
            property.serializedObject.ApplyModifiedProperties();
          }
        }

        string[] typePickerOptions = Array.Empty<string>();

        if (showAssetRef) {

          Rect assetRefRect = rect;

          string error = null;
          if (assetRefValue.IsValid) {
            if (UnityDB.FindAssetForInspector(new AssetGuid(assetRefValueProperty.longValue)) is EntityPrototypeAsset asset) {

              var components = asset.Parent.GetComponents<EntityComponentBase>();

              if (components.Length == 0) {
                error = $"Prototype has no components";
              } else if (string.IsNullOrEmpty(componentTypeName)) {
                error = $"Component type not selected";
              } else {
                var component = Array.Find(components, c => c.ComponentType.Name == componentTypeName);
                if (!component) {
                  error = $"Component {componentTypeName} not found";
                }
              }

              if (hasComponentTypeName) {
                typePickerOptions = components.Select(x => x.ComponentType.Name).ToArray();
              }
            }
          }

          EditorGUI.BeginChangeCheck();
          using (new EditorGUI.IndentLevelScope(-EditorGUI.indentLevel)) {
            EditorGUI.PropertyField(assetRefRect, assetRefProperty, GUIContent.none);
            if (error != null) {
              QuantumEditorGUI.Decorate(assetRefRect, error, MessageType.Error);
            }
          }

          rect.x += rect.width;
          if (EditorGUI.EndChangeCheck()) {
            sceneReferenceProperty.objectReferenceValue = null;
            property.serializedObject.ApplyModifiedProperties();
          }
        }

        if (showTypeCombo) {
          var componentTypeProperty = property.FindPropertyRelativeOrThrow("_componentTypeName");
          using (new EditorGUI.IndentLevelScope(-EditorGUI.indentLevel)) {
            using (new QuantumEditorGUI.PropertyScope(rect, GUIContent.none, componentTypeProperty)) {
              var index = Array.FindIndex(typePickerOptions, x => string.Equals(x, componentTypeName, StringComparison.OrdinalIgnoreCase));
              EditorGUI.BeginChangeCheck();
              index = EditorGUI.Popup(rect, index, typePickerOptions);
              if (EditorGUI.EndChangeCheck()) {
                componentTypeProperty.stringValue = typePickerOptions[index];
                property.serializedObject.ApplyModifiedProperties();
              }
            }
          }
        }
      }
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
      var componentTypeName = property.FindPropertyRelative("_componentTypeName");
      DrawMultiField(position, property, label, componentTypeName.stringValue, hasComponentTypeName: true);
    }
  }

  [CustomPropertyDrawer(typeof(ComponentPrototypeRefWrapperBase), true)]
  public class ComponentPrototypeRefWrapperBaseDrawer : PropertyDrawer {

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
      var elementType = fieldInfo.FieldType;
      if (elementType.HasElementType) {
        elementType = elementType.GetElementType();
      }

      var baseType = elementType.BaseType;
      Debug.Assert(baseType.IsGenericType && baseType.GetGenericTypeDefinition() == typeof(ComponentPrototypeRefWrapperBase<,>));

      var componentType = baseType.GetGenericArguments()[1];
      Debug.Assert(componentType.GetInterface("Quantum.IComponent") != null);

      ComponentPrototypeRefWrapperDrawer.DrawMultiField(position, property, label, componentType.Name, hasComponentTypeName: false);
    }
  }
}
#endregion

#region quantum_unity/Assets/Photon/Quantum/Editor/PropertyDrawers/ComponentTypeSelectorDrawer.cs
namespace Quantum.Editor {

  using System;
  using System.Linq;
  using UnityEditor;
  using UnityEngine;

  [CustomPropertyDrawer(typeof(ComponentTypeSelector))]
  public unsafe class ComponentTypeSelectorDrawer : PropertyDrawer {
    public override void OnGUI(Rect p, SerializedProperty prop, GUIContent label) {

      var componentName = prop.FindPropertyRelativeOrThrow(nameof(ComponentTypeSelector.ComponentTypeName));

      string error = ImportObsolete(prop, out var errorType);

      using (new QuantumEditorGUI.PropertyScopeWithPrefixLabel(p, label, prop, out p)) {
        QuantumEditorGUI.PropertyField(p, componentName, GUIContent.none);
        if (!string.IsNullOrEmpty(error)) {
          QuantumEditorGUI.Decorate(p, error, errorType);
        }
      }
    }

    private static string ImportObsolete(SerializedProperty prop, out MessageType errorType) {

#pragma warning disable CS0612 // Type or member is obsolete
      var qualifiedName = prop.FindPropertyRelativeOrThrow(nameof(ComponentTypeSelector.QualifiedName));
#pragma warning restore CS0612 // Type or member is obsolete

      string error = null;
      errorType = MessageType.Error;

      if (!string.IsNullOrEmpty(qualifiedName.stringValue)) {
        var componentName = prop.FindPropertyRelativeOrThrow(nameof(ComponentTypeSelector.ComponentTypeName));
        if (string.IsNullOrEmpty(componentName.stringValue)) {
          var type = Type.GetType(qualifiedName.stringValue);
          if (type != null) {
            componentName.stringValue = type.Name;
            Debug.Log($"Imported obsolete QualifiedName \"{qualifiedName.stringValue}\" to {componentName.propertyPath}", prop.serializedObject.targetObject);
            qualifiedName.stringValue = string.Empty;
            qualifiedName.serializedObject.ApplyModifiedPropertiesWithoutUndo();
          } else {
            errorType = MessageType.Error;
            error = $"Failed to import QualifiedName: {qualifiedName.stringValue}";
          }
        } else {
          errorType = MessageType.Warning;
          error = $"Obsolete {qualifiedName.name} has values, but the new {componentName.name} is not empty. Not importing nor clearing the old property.";
        }
      }

      return error;
    }
  }
}

#endregion

#region quantum_unity/Assets/Photon/Quantum/Editor/PropertyDrawers/ComponentTypeSetSelectorDrawer.cs
namespace Quantum.Editor {
  using System;
  using System.Collections.Generic;
  using System.Text;
  using UnityEditor;
  using UnityEngine;

  [CustomPropertyDrawer(typeof(ComponentTypeSetSelector))]
  public unsafe class ComponentTypeSetSelectorDrawer : PropertyDrawer {
    public override void OnGUI(Rect position, SerializedProperty prop, GUIContent label) {

      var componentNames = prop.FindPropertyRelativeOrThrow(nameof(ComponentTypeSetSelector.ComponentTypeNames));

      string error = ImportObsolete(prop, out var errorType);

      using (new QuantumEditorGUI.PropertyScope(position, label, prop)) {
        QuantumEditorGUI.PropertyField(position, componentNames, label, prop.isExpanded);
        prop.isExpanded = componentNames.isExpanded;
      }

      if (!string.IsNullOrEmpty(error)) {
        position.xMin += EditorGUIUtility.labelWidth;
        QuantumEditorGUI.Decorate(position.SetLineHeight(), error, errorType);
      }
    }

    public override float GetPropertyHeight(SerializedProperty prop, GUIContent label) {
      var arrayProp = prop.FindPropertyRelativeOrThrow(nameof(ComponentTypeSetSelector.ComponentTypeNames));
      return QuantumEditorGUI.GetPropertyHeight(arrayProp, prop.isExpanded);
    }

    private static string ImportObsolete(SerializedProperty prop, out MessageType errorType) {

#pragma warning disable CS0612 // Type or member is obsolete
      var qualifiedNames = prop.FindPropertyRelativeOrThrow(nameof(ComponentTypeSetSelector.QualifiedNames));
#pragma warning restore CS0612 // Type or member is obsolete

      string error = "";
      errorType = MessageType.Error;

      if (qualifiedNames.arraySize > 0) {

        var componentNames = prop.FindPropertyRelativeOrThrow(nameof(ComponentTypeSetSelector.ComponentTypeNames));

        // importing from an obsolete property
        if (componentNames.arraySize == 0) {
          for (int i = 0; i < qualifiedNames.arraySize; ++i) {
            var qualifiedName = qualifiedNames.GetArrayElementAtIndex(i);
            if (Type.GetType(qualifiedName.stringValue) == null) {
              errorType = MessageType.Error;
              error = $"Failed to import obsolete QualifiedName: {qualifiedName.stringValue}";
              break;
            }
          }

          if (string.IsNullOrEmpty(error)) {
            for (int i = 0; i < qualifiedNames.arraySize; ++i) {
              var qualifiedName = qualifiedNames.GetArrayElementAtIndex(i);
              var type = Type.GetType(qualifiedName.stringValue, throwOnError: true);
              componentNames.InsertArrayElementAtIndex(componentNames.arraySize);
              var componentName = componentNames.GetArrayElementAtIndex(componentNames.arraySize - 1);
              componentName.stringValue = type.Name;
              Debug.Log($"Imported obsolete QualifiedName \"{qualifiedName.stringValue}\" to {componentName.propertyPath}", prop.serializedObject.targetObject);
            }

            qualifiedNames.arraySize = 0;
            qualifiedNames.serializedObject.ApplyModifiedPropertiesWithoutUndo();
          }
        } else {
          error = $"Obsolete {qualifiedNames.name} has values, but the new {componentNames.name} is not empty. Not importing nor clearing the old property.";
          errorType = MessageType.Warning;
        }
      }

      return error;
    }
  }
}

#endregion

#region quantum_unity/Assets/Photon/Quantum/Editor/PropertyDrawers/EntityPrototypeRefWapperDrawer.cs
namespace Quantum.Editor {
  using UnityEditor;
  using UnityEngine;

  [CustomPropertyDrawer(typeof(EntityPrototypeRefWrapper))]
  public class EntityPrototypeRefWrapperEditor : PropertyDrawer {

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
      var sceneReferenceProperty = property.FindPropertyRelativeOrThrow(nameof(EntityPrototypeRefWrapper.ScenePrototype));
      var assetRefProperty = property.FindPropertyRelativeOrThrow(nameof(EntityPrototypeRefWrapper.AssetPrototype));
      var assetRefValueProperty = assetRefProperty.FindPropertyRelative("Id.Value");

      using (new QuantumEditorGUI.PropertyScope(position, label, property)) {
        var rect = EditorGUI.PrefixLabel(position, label);

        bool showAssetRef = assetRefValueProperty.longValue != 0 || sceneReferenceProperty.objectReferenceValue == null;
        bool showReference = sceneReferenceProperty.objectReferenceValue != null || assetRefValueProperty.longValue == 0;

        Debug.Assert(showAssetRef || showReference);

        if (showAssetRef && showReference) {
          rect.width /= 2;
        }

        if (showReference) {
          EditorGUI.BeginChangeCheck();
          using (new EditorGUI.IndentLevelScope(-EditorGUI.indentLevel)) {
            EditorGUI.PropertyField(rect, sceneReferenceProperty, GUIContent.none);
          }
          rect.x += rect.width;
          if (EditorGUI.EndChangeCheck()) {
            assetRefValueProperty.longValue = 0;
            property.serializedObject.ApplyModifiedProperties();
          }
        }

        if (showAssetRef) {
          EditorGUI.BeginChangeCheck();
          using (new EditorGUI.IndentLevelScope(-EditorGUI.indentLevel)) {
            EditorGUI.PropertyField(rect, assetRefProperty, GUIContent.none);
          }
          if (EditorGUI.EndChangeCheck()) {
            sceneReferenceProperty.objectReferenceValue = null;
            property.serializedObject.ApplyModifiedProperties();
          }
        }
      }
    }
  }
}
#endregion

#region quantum_unity/Assets/Photon/Quantum/Editor/PropertyDrawers/EnumWrapperPropertyDrawers.cs
namespace Quantum.Editor {
  using Quantum;
  using UnityEngine;
  using UnityEditor;

  [CustomPropertyDrawer(typeof(RotationFreezeFlags_Wrapper))]
  [CustomPropertyDrawer(typeof(QueryOptions_Wrapper))]
  [CustomPropertyDrawer(typeof(CallbackFlags_Wrapper))]
  [CustomPropertyDrawer(typeof(PhysicsBody2D.ConfigFlagsWrapper))]
  [CustomPropertyDrawer(typeof(PhysicsBody3D.ConfigFlagsWrapper))]
  partial class PrototypeDrawer { }
}
#endregion

#region quantum_unity/Assets/Photon/Quantum/Editor/PropertyDrawers/FixedAnimationCurveDrawer.cs
namespace Quantum.Editor {
  using Photon.Deterministic;
  using System.Collections.Generic;
  using UnityEditor;
  using UnityEngine;

  [CustomPropertyDrawer(typeof(FPAnimationCurve))]
  public class FixedCurveDrawer : PropertyDrawer {

    private Dictionary<string, AnimationCurve> _animationCurveCache = new Dictionary<string, AnimationCurve>();

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label) {
      return QuantumEditorGUI.GetLinesHeight(3);
    }

    public override void OnGUI(Rect position, SerializedProperty prop, GUIContent label) {

      // Get properties accessors.
      var resolutionProperty = prop.FindPropertyRelative("Resolution");
      var samplesProperty = prop.FindPropertyRelative("Samples");
      var startTimeProperty = GetPropertyNext(prop, "StartTime");
      var endTimeProperty = GetPropertyNext(prop, "EndTime");
      var preWrapModeProperty = prop.FindPropertyRelative("PreWrapMode");
      var postWrapModeProperty = prop.FindPropertyRelative("PostWrapMode");
      var preWrapModeOriginalProperty = prop.FindPropertyRelative("OriginalPreWrapMode");
      var postWrapModeOriginalProperty = prop.FindPropertyRelative("OriginalPostWrapMode");
      var keysProperty = prop.FindPropertyRelative("Keys");

      // Default values (because we changed FPAnimationCurve to be a struct)
      if (resolutionProperty.intValue <= 1) {
        resolutionProperty.intValue = 32;
        startTimeProperty.longValue = 0;
        endTimeProperty.longValue = FP.RAW_ONE;
      }

      AnimationCurve animationCurve;

      var propertyKey = prop.propertyPath + "_" + prop.serializedObject.GetHashCode();
      if (!_animationCurveCache.TryGetValue(propertyKey, out animationCurve)) {
        // Load the Quantum data into a Unity animation curve.
        animationCurve = new AnimationCurve();
        _animationCurveCache[propertyKey] = animationCurve;
        animationCurve.preWrapMode = (WrapMode)preWrapModeOriginalProperty.intValue;
        animationCurve.postWrapMode = (WrapMode)postWrapModeOriginalProperty.intValue;
        for (int i = 0; i < keysProperty.arraySize; i++) {
          var keyProperty = keysProperty.GetArrayElementAtIndex(i);
          var key = new Keyframe();
          key.time = FP.FromRaw(GetPropertyNext(keyProperty, "Time").longValue).AsFloat;
          key.value = FP.FromRaw(GetPropertyNext(keyProperty, "Value").longValue).AsFloat;
          key.inTangent = FP.FromRaw(GetPropertyNext(keyProperty, "InTangent").longValue).AsFloat;
          key.outTangent= FP.FromRaw(GetPropertyNext(keyProperty, "OutTangent").longValue).AsFloat;

          animationCurve.AddKey(key);

          var leftTangentMode = (AnimationUtility.TangentMode)keyProperty.FindPropertyRelative("TangentModeLeft").intValue;
          var rightTangentMode = (AnimationUtility.TangentMode)keyProperty.FindPropertyRelative("TangentModeRight").intValue;

          // Since 2018.1 key.TangentMode is depricated. AnimationUtility was already working on 2017, so just do the conversion here. 
          var depricatedTangentMode = keyProperty.FindPropertyRelative("TangentMode").intValue;
          if (depricatedTangentMode > 0) { 
            leftTangentMode = ConvertTangetMode(depricatedTangentMode, true);
            rightTangentMode = ConvertTangetMode(depricatedTangentMode, false);
#pragma warning disable 0618
            keyProperty.FindPropertyRelative("TangentMode").intValue = key.tangentMode;
#pragma warning restore 0618
            Debug.LogFormat("FPAnimationCurve: Converted tangent for key {0} from depricated={1} to left={2}, right={3}", i, depricatedTangentMode, leftTangentMode, rightTangentMode);
          }
            
          AnimationUtility.SetKeyLeftTangentMode(animationCurve, animationCurve.length - 1, leftTangentMode);
          AnimationUtility.SetKeyRightTangentMode(animationCurve, animationCurve.length - 1, rightTangentMode);
        }
      }

      EditorGUI.BeginChangeCheck();

      var p = position.SetLineHeight();

      EditorGUI.LabelField(p, prop.displayName);
      p = p.AddLine();

      EditorGUI.indentLevel++;

      resolutionProperty.intValue = EditorGUI.IntField(p, "Resolution", resolutionProperty.intValue);
      resolutionProperty.intValue = Mathf.Clamp(resolutionProperty.intValue, 2, 1024);

      p = p.AddLine();
      animationCurve = EditorGUI.CurveField(p, "Samples", animationCurve);
      _animationCurveCache[propertyKey] = animationCurve;

      EditorGUI.indentLevel--;

      if (EditorGUI.EndChangeCheck()) {

        // Save information to restore the Unity AnimationCurve.
        keysProperty.ClearArray();
        keysProperty.arraySize = animationCurve.keys.Length;
        for (int i = 0; i < animationCurve.keys.Length; i++) {
          var key = animationCurve.keys[i];
          var keyProperty = keysProperty.GetArrayElementAtIndex(i);
          GetPropertyNext(keyProperty, "Time").longValue = FP.FromFloat_UNSAFE(key.time).RawValue;
          GetPropertyNext(keyProperty, "Value").longValue = FP.FromFloat_UNSAFE(key.value).RawValue;
          try {
            GetPropertyNext(keyProperty, "InTangent").longValue = FP.FromFloat_UNSAFE(key.inTangent).RawValue;
          }
          catch (System.OverflowException) {
            GetPropertyNext(keyProperty, "InTangent").longValue = Mathf.Sign(key.inTangent) < 0.0f ? FP.MinValue.RawValue : FP.MaxValue.RawValue;
          }
          try {
            GetPropertyNext(keyProperty, "OutTangent").longValue = FP.FromFloat_UNSAFE(key.outTangent).RawValue;
          }
          catch (System.OverflowException) {
            GetPropertyNext(keyProperty, "OutTangent").longValue = Mathf.Sign(key.outTangent) < 0.0f ? FP.MinValue.RawValue : FP.MaxValue.RawValue;
          }

          keyProperty.FindPropertyRelative("TangentModeLeft").intValue = (int)AnimationUtility.GetKeyLeftTangentMode(animationCurve, i);
          keyProperty.FindPropertyRelative("TangentModeRight").intValue = (int)AnimationUtility.GetKeyRightTangentMode(animationCurve, i);
          keyProperty.FindPropertyRelative("TangentMode").intValue = 0;
        }

        // Save the curve onto the Quantum FPAnimationCurve object via SerializedObject.
        preWrapModeProperty.intValue = (int)GetWrapMode(animationCurve.preWrapMode);
        postWrapModeProperty.intValue = (int)GetWrapMode(animationCurve.postWrapMode);
        preWrapModeOriginalProperty.intValue = (int)animationCurve.preWrapMode;
        postWrapModeOriginalProperty.intValue = (int)animationCurve.postWrapMode;

        // Get the used segment.
        float startTime = animationCurve.keys.Length == 0 ? 0.0f : float.MaxValue;
        float endTime = animationCurve.keys.Length == 0 ? 1.0f : float.MinValue; ;
        for (int i = 0; i < animationCurve.keys.Length; i++) {
          startTime = Mathf.Min(startTime, animationCurve[i].time);
          endTime = Mathf.Max(endTime, animationCurve[i].time);
        }

        startTimeProperty.longValue = FP.FromFloat_UNSAFE(startTime).RawValue;
        endTimeProperty.longValue = FP.FromFloat_UNSAFE(endTime).RawValue;

        // Save the curve inside an array with specific resolution.
        var resolution = resolutionProperty.intValue;
        if (resolution <= 0)
          return;
        samplesProperty.ClearArray();
        samplesProperty.arraySize = resolution + 1;
        var deltaTime = (endTime - startTime) / (float)resolution;
        for (int i = 0; i < resolution + 1; i++) {
          var time = startTime + deltaTime * i;
          var fp = FP.FromFloat_UNSAFE(animationCurve.Evaluate(time));
          GetArrayElementNext(samplesProperty, i).longValue = fp.RawValue;
        }

        prop.serializedObject.ApplyModifiedProperties();
      }
    }

    private static SerializedProperty GetPropertyNext(SerializedProperty prop, string name) {
      var result = prop.FindPropertyRelative(name);
      if (result != null)
        result.Next(true);
      
      return result;
    }

    private static SerializedProperty GetArrayElementNext(SerializedProperty prop, int index) {
      var result = prop.GetArrayElementAtIndex(index);
      result.Next(true);
      return result;
    }

    private static FPAnimationCurve.WrapMode GetWrapMode(WrapMode wrapMode) {
      switch (wrapMode) {
        case WrapMode.Loop:
          return FPAnimationCurve.WrapMode.Loop;
        case WrapMode.PingPong:
          return FPAnimationCurve.WrapMode.PingPong;
        default:
          return FPAnimationCurve.WrapMode.Clamp;
      }
    }

    private static AnimationUtility.TangentMode ConvertTangetMode(int depricatedTangentMode, bool isLeftOrRight) {
      // old to new conversion
      // Left
      // Free     0000001 -> 00000000 (TangentMode.Free)
      // Constant 0000111 -> 00000011 (TangentMode.Constant)
      // Linear   0000101 -> 00000010 (TangentMode.Linear)
      // Right
      // Free     0000001 -> 00000000 (TangentMode.Free)
      // Linear   1000001 -> 00000010 (TangentMode.Constant)
      // Constant 1100001 -> 00000011 (TangentMode.Linear)

      var shift = isLeftOrRight ? 1 : 5;

      if (((depricatedTangentMode >> shift) & 0x3) == (int)AnimationUtility.TangentMode.Linear) {
        return AnimationUtility.TangentMode.Linear;
      }
      else if (((depricatedTangentMode >> shift) & 0x3) == (int)AnimationUtility.TangentMode.Constant) {
        return AnimationUtility.TangentMode.Constant;
      }

      return AnimationUtility.TangentMode.Free;
    }
  }
}


#endregion

#region quantum_unity/Assets/Photon/Quantum/Editor/PropertyDrawers/FixedPropertyDrawer.cs
namespace Quantum.Editor {
  using System;
  using UnityEditor;
  using UnityEngine;
  using Photon.Deterministic;
  using System.Linq;

  [CustomPropertyDrawer(typeof(FP))]
  public class FPPropertyDrawer : PropertyDrawer {

    public const int DefaultPrecision = 5;

    public override void OnGUI(Rect p, SerializedProperty prop, GUIContent label) {
      using (new QuantumEditorGUI.PropertyScope(p, label, prop)) {
        DrawRaw(p, prop.FindPropertyRelativeOrThrow(nameof(FP.RawValue)), label);
      }
    }

    public static float GetRawAsFloat(SerializedProperty prop) {
      return GetRawAsFloat(prop.longValue);
    }

    public static float GetRawAsFloat(long rawValue) {
      var f = FP.FromRaw(rawValue);
      var precision = QuantumEditorSettings.InstanceFailSilently?.FPDisplayPrecision ?? DefaultPrecision;
      var v = (Single)Math.Round(f.AsFloat, precision);
      return v;
    }


    public static void DrawRaw(Rect p, SerializedProperty prop, GUIContent label, bool opposite = false) {
      if (DrawRawValueAsFloat(p, prop.longValue, label, out var raw)) {
        prop.longValue = raw;
      }
    }

    public static void DrawAs2DRotation(Rect p, SerializedProperty prop, GUIContent label) {
      DrawRawAs2DRotation(p, GetRawProperty(prop), label);
    }

    public static void DrawRawAs2DRotation(Rect p, SerializedProperty prop, GUIContent label) {
      using (new QuantumEditorGUI.PropertyScope(p, label, prop)) {
        long initialValue = prop.longValue;
#if !QUANTUM_XY
        initialValue = -initialValue;
#endif
        if (DrawRawValueAsFloat(p, initialValue, label, out var raw)) {
#if !QUANTUM_XY
          prop.longValue = -raw;
#else
          prop.longValue = raw;
#endif
        }
      }
    }

    public static void DrawAsSlider(Rect p, SerializedProperty prop, GUIContent label, FP min, FP max) {
      DrawRawAsSlider(p, GetRawProperty(prop), label, min, max);
    }


    public static void DrawRawAsSlider(Rect p, SerializedProperty prop, GUIContent label, FP min, FP max) {
      using (new QuantumEditorGUI.PropertyScope(p, label, prop)) {
        var v = GetRawAsFloat(prop.longValue);
        EditorGUI.BeginChangeCheck();
        v = EditorGUI.Slider(p, label, v, min.AsFloat, max.AsFloat);
        if (EditorGUI.EndChangeCheck()) {
          prop.longValue = FP.FromFloat_UNSAFE(v).RawValue;
        }
        QuantumEditorGUI.Overlay(p, "(FP)");
      }
    }

    private static SerializedProperty GetRawProperty(SerializedProperty root) {
      var prop = root.Copy();
      prop.Next(true);
      Debug.Assert(prop.name == nameof(FP.RawValue));
      return prop;
    }

    private static bool DrawRawValueAsFloat(Rect p, long rawValue, GUIContent label, out long rawResult) {
      // grab value
      var v = GetRawAsFloat(rawValue);

      // edit value
      try {
        var n = label == null ? EditorGUI.FloatField(p, v) : EditorGUI.FloatField(p, label, v);
        if (n != v) {
          rawResult = FP.FromFloat_UNSAFE(n).RawValue;
          return true;
        }

        QuantumEditorGUI.Overlay(p, "(FP)");

      } catch (FormatException exn) {
        if (exn.Message != ".") {
          Debug.LogException(exn);
        }
      }

      rawResult = default;
      return false;
    }

    internal static Rect DoMultiFPProperty(Rect p, SerializedProperty prop, GUIContent label, GUIContent[] labels, string[] paths) {
      EditorGUI.BeginProperty(p, label, prop);
      try {
        int id = GUIUtility.GetControlID(_multiFieldPrefixId, FocusType.Keyboard, p);
        p = UnityInternal.EditorGUI.MultiFieldPrefixLabel(p, id, label, labels.Length);
        if (p.width > 1) {
          using (new EditorGUI.IndentLevelScope(-EditorGUI.indentLevel)) {
            float w = (p.width - (labels.Length - 1) * SpacingSubLabel) / labels.Length;
            var ph = new Rect(p) { width = w };

            for (int i = 0; i < labels.Length; ++i) {
              using (new QuantumEditorGUI.LabelWidthScope(CalcPrefixLabelWidth(labels[i]))) {
                var nested = prop.FindPropertyRelativeOrThrow(paths[i]);
#if !UNITY_2020_2_OR_NEWER
                EditorGUI.BeginProperty(ph, labels[i], nested);
                try {
#endif
                  DrawRaw(ph, nested, labels[i]);
#if !UNITY_2020_2_OR_NEWER
                } finally {
                  EditorGUI.EndProperty();
                }
#endif
              }
              ph.x += w + SpacingSubLabel;
            }
          }
        }
      } finally {
        EditorGUI.EndProperty();
      }

      return p;
    }

    private static float CalcPrefixLabelWidth(GUIContent label) {
#if UNITY_2019_3_OR_NEWER
      return EditorStyles.label.CalcSize(label).x;
#else
      return 13.0f;
#endif
    }

    private const float SpacingSubLabel = 2;
    private static readonly int _multiFieldPrefixId = "MultiFieldPrefixId".GetHashCode();

    public static void CopyRawToClipboard(SerializedProperty prop, bool logProperty = false) {
      var s = prop.FindPropertyRelativeOrThrow("RawValue").longValue.ToString();
      EditorGUIUtility.systemCopyBuffer = s;
      if (logProperty) {
        Debug.Log($"{prop.displayName}: {s}");
      }
    }
  }

  [CustomPropertyDrawer(typeof(FPVector2))]
  public class FPVector2PropertyDrawer : PropertyDrawer {

    private static GUIContent[] _labels = new[] {
      new GUIContent("X"),
      new GUIContent("Y"),
    };

    private static string[] _paths = new[] {
      "X.RawValue",
      "Y.RawValue",
    };

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label) {
      return QuantumEditorGUI.GetLinesHeightWithNarrowModeSupport(1);
    }

    public override void OnGUI(Rect p, SerializedProperty prop, GUIContent label) {
      DrawCompact(p, prop, label);
    }

    public static void DrawCompact(Rect p, SerializedProperty prop, GUIContent label) {
      FPPropertyDrawer.DoMultiFPProperty(p, prop, label, _labels, _paths);
    }

    public static void CopyRawToClipboard(SerializedProperty prop, bool logProperty = false) {
      var s = string.Join(" ", _paths.Select(p => prop.FindPropertyRelativeOrThrow(p).longValue.ToString()).ToArray());
      EditorGUIUtility.systemCopyBuffer = s;
      if (logProperty) {
        Debug.Log($"{prop.displayName}: {s}");
      }
    }
  }

  [CustomPropertyDrawer(typeof(FPVector3))]
  public class FPVector3PropertyDrawer : PropertyDrawer {

    private static GUIContent[] _labels = new[] {
      new GUIContent("X"),
      new GUIContent("Y"),
      new GUIContent("Z")
    };

    private static string[] _paths = new[] {
      "X.RawValue",
      "Y.RawValue",
      "Z.RawValue"
    };

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label) {
      return QuantumEditorGUI.GetLinesHeightWithNarrowModeSupport(1);
    }

    public override void OnGUI(Rect p, SerializedProperty prop, GUIContent label) {
      DrawCompact(p, prop, label);
    }

    public static void DrawCompact(Rect p, SerializedProperty prop, GUIContent label) {
      FPPropertyDrawer.DoMultiFPProperty(p, prop, label, _labels, _paths);
    }

    public static void CopyRawToClipboard(SerializedProperty prop, bool logProperty = false) {
      var s = string.Join(" ", _paths.Select(p => prop.FindPropertyRelativeOrThrow(p).longValue.ToString()).ToArray());
      EditorGUIUtility.systemCopyBuffer = s;
      if (logProperty) {
        Debug.Log($"{prop.displayName}: {s}");
      }
    }
  }

  public static class FPPropertyContextMenu {
    private static GUIContent MenuText = new GUIContent("Copy raw FP values to clipboard");
    private static bool LogProperty = true;

    [InitializeOnLoadMethod]
    public static void Init() {
      EditorApplication.contextualPropertyMenu += OnContextualPropertyMenu;
    }

    static void OnContextualPropertyMenu(GenericMenu menu, SerializedProperty property) {
      switch (property.type) {
        case nameof(FP): {
            var propertyCopy = property.Copy();
            menu.AddItem(MenuText, false, () => FPPropertyDrawer.CopyRawToClipboard(propertyCopy, LogProperty));
          }
          break;

        case nameof(FPVector2): {
            var propertyCopy = property.Copy();
            menu.AddItem(MenuText, false, () => FPVector2PropertyDrawer.CopyRawToClipboard(propertyCopy, LogProperty));
          }

          break;

        case nameof(FPVector3): {
            var propertyCopy = property.Copy();
            menu.AddItem(MenuText, false, () => FPVector3PropertyDrawer.CopyRawToClipboard(propertyCopy, LogProperty));
          }
          break;
      }
    }
  }
}
#endregion

#region quantum_unity/Assets/Photon/Quantum/Editor/PropertyDrawers/FixedQuaternionPropertyDrawer.cs
namespace Quantum.Editor {
  using Photon.Deterministic;
  using UnityEditor;
  using UnityEngine;

  [CustomPropertyDrawer(typeof(FPQuaternion))]
  public class FPQuaternionPropertyDrawer : PropertyDrawer {

    private static GUIContent[] _labels = new[] {
      new GUIContent("X"),
      new GUIContent("Y"),
      new GUIContent("Z"),
      new GUIContent("W"),
    };

    private static string[] _paths = new[] {
      "X.RawValue",
      "Y.RawValue",
      "Z.RawValue",
      "W.RawValue",
    };

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label) {
      return QuantumEditorGUI.GetLinesHeightWithNarrowModeSupport(1);
    }

    public override void OnGUI(Rect p, SerializedProperty prop, GUIContent label) {
      DrawCompact(p, prop, label);
    }

    public static void DrawCompact(Rect p, SerializedProperty prop, GUIContent label) {

      EditorGUI.BeginChangeCheck();
      FPPropertyDrawer.DoMultiFPProperty(p, prop, label, _labels, _paths);
      if ( EditorGUI.EndChangeCheck()) {
        var rawX = prop.FindPropertyRelativeOrThrow("X.RawValue");
        var rawY = prop.FindPropertyRelativeOrThrow("Y.RawValue");
        var rawZ = prop.FindPropertyRelativeOrThrow("Z.RawValue");
        var rawW = prop.FindPropertyRelativeOrThrow("W.RawValue");
        Normalize(rawX, rawY, rawZ, rawW);
      }
    }

    private static void Normalize(SerializedProperty rawX, SerializedProperty rawY, SerializedProperty rawZ, SerializedProperty rawW) {
      var x = FP.FromRaw(rawX.longValue).AsDouble;
      var y = FP.FromRaw(rawY.longValue).AsDouble;
      var z = FP.FromRaw(rawZ.longValue).AsDouble;
      var w = FP.FromRaw(rawW.longValue).AsDouble;

      var magnitueSqr = x * x + y * y + z * z + w * w;
      if (magnitueSqr < 0.00001) {
        x = y = z = 0;
        w = 1;
      } else {
        var m = System.Math.Sqrt(magnitueSqr);
        x /= m;
        y /= m;
        z /= m;
        w /= m;
      }

      rawX.longValue = FP.FromFloat_UNSAFE((float)x).RawValue;
      rawY.longValue = FP.FromFloat_UNSAFE((float)y).RawValue;
      rawZ.longValue = FP.FromFloat_UNSAFE((float)z).RawValue;
      rawW.longValue = FP.FromFloat_UNSAFE((float)w).RawValue;
    }
  }
}
#endregion

#region quantum_unity/Assets/Photon/Quantum/Editor/PropertyDrawers/FlatEntityPrototypeContainerDrawer.cs
namespace Quantum.Editor {

  using Quantum.Prototypes;
  using UnityEditor;
  using UnityEngine;

  [CustomPropertyDrawer(typeof(FlatEntityPrototypeContainer))]
  public class FlatEntityPrototypeContainerDrawer : PropertyDrawer {

    public override bool CanCacheInspectorGUI(SerializedProperty property) {
      return false;
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
      using (new QuantumEditorGUI.PropertyScope(position, label, property)) {
        QuantumEditorGUI.PropertyField(position, property, label, property.isExpanded);
      }
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label) {
      return QuantumEditorGUI.GetPropertyHeight(property, label, property.isExpanded);
    }
  }
}
#endregion

#region quantum_unity/Assets/Photon/Quantum/Editor/PropertyDrawers/FloatMinMaxDrawer.cs
namespace Quantum.Editor {
  using UnityEngine;
  using UnityEditor;
  using System;

  [CustomPropertyDrawer(typeof(Quantum.MinMaxSliderAttribute))]
  class MinMaxSliderDrawer : PropertyDrawer {
    const Single MIN_MAX_WIDTH = 50f;
    const Single SPACING = 1f;

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
      var spacing = SPACING * EditorGUIUtility.pixelsPerPoint;
      var min = property.FindPropertyRelative("Min");
      var minValue = min.floatValue;

      var max = property.FindPropertyRelative("Max");
      var maxValue = max.floatValue;

      var attr = (Quantum.MinMaxSliderAttribute)attribute;

      EditorGUI.PrefixLabel(position, label);

      //var p = position;
      //p.x += EditorGUIUtility.labelWidth + MIN_MAX_WIDTH + spacing;
      //p.width -= EditorGUIUtility.labelWidth + (MIN_MAX_WIDTH + spacing) * 2;

      //EditorGUI.BeginChangeCheck();
      //EditorGUI.MinMaxSlider(p, ref minValue, ref maxValue, attr.Min, attr.Max);
      //if (EditorGUI.EndChangeCheck()) {
      //  min.floatValue = minValue;
      //  max.floatValue = maxValue;
      //}

      var w = ((position.width - EditorGUIUtility.labelWidth) * 0.5f) - spacing;

      var p = position;
      p.x += EditorGUIUtility.labelWidth;
      p.width = w;
      min.floatValue = EditorGUI.FloatField(p, min.floatValue);

      QuantumEditorGUI.Overlay(p, "(Start)");

      p = position;
      p.x += p.width - w;
      p.width = w;
      max.floatValue = EditorGUI.FloatField(p, max.floatValue);

      QuantumEditorGUI.Overlay(p, "(End)");
    }
  }
}
#endregion

#region quantum_unity/Assets/Photon/Quantum/Editor/PropertyDrawers/LayerMaskDrawer.cs
namespace Quantum.Editor {
  using UnityEditor;
  using UnityEditorInternal;
  using UnityEngine;

  [CustomPropertyDrawer(typeof(Quantum.LayerMask))]
  
  public class LayerMaskDrawer : PropertyDrawer {
    public override void OnGUI(Rect p, SerializedProperty prop, GUIContent label) {
      // go into child property (raw)
      prop.Next(true);

      // draw field
      Draw(p, prop, label);
    }

    public static void Draw(Rect p, SerializedProperty prop, GUIContent label) {
      prop.intValue = InternalEditorUtility.ConcatenatedLayersMaskToLayerMask(EditorGUI.MaskField(p, label, InternalEditorUtility.LayerMaskToConcatenatedLayersMask(prop.intValue), InternalEditorUtility.layers));
    }
  }
}

#endregion

#region quantum_unity/Assets/Photon/Quantum/Editor/PropertyDrawers/LocalReferenceAttributeDrawer.cs
namespace Quantum.Editor {
  using UnityEditor;
  using UnityEngine;

  [CustomPropertyDrawer(typeof(LocalReferenceAttribute))]
  public class LocalReferenceAttributeDrawer : PropertyDrawer {

    private string lastError;
    private string lastErrorPropertyPath;

    public override void OnGUI(Rect position, SerializedProperty prop, GUIContent label) {

      EditorGUI.BeginChangeCheck();
      EditorGUI.PropertyField(position, prop, label);
      if (EditorGUI.EndChangeCheck()) {
        lastError = null;
      }

      if (lastError != null && lastErrorPropertyPath == prop.propertyPath) {
        QuantumEditorGUI.Decorate(position, lastError, MessageType.Error, hasLabel: !QuantumEditorGUI.IsNullOrEmpty(label));
      }

      var reference = prop.objectReferenceValue;
      if (reference == null)
        return;

      var target = prop.serializedObject.targetObject;

      if (target is MonoBehaviour mb) {
        if (reference is Component comp) {
          if (!AreLocal(mb, comp)) {
            prop.objectReferenceValue = null;
            prop.serializedObject.ApplyModifiedProperties();
            lastError = "Use only local references";
          }
        } else {
          lastError = "MonoBehaviour to ScriptableObject not supported yet";
        }
      } else {
        lastError = "ScriptableObject not supported yet";
      }

      if (lastError != null) {
        lastErrorPropertyPath = prop.propertyPath;
      }
    }

    public static bool AreLocal(Component a, Component b) {
      if (EditorUtility.IsPersistent(a)) {
        if (AssetDatabase.GetAssetPath(a) != AssetDatabase.GetAssetPath(b)) {
          return false;
        }
      } else {
        if (a.gameObject.scene != b.gameObject.scene) {
          return false;
        }
      }
      return true;
    }
  }
}
#endregion

#region quantum_unity/Assets/Photon/Quantum/Editor/PropertyDrawers/MultiTypeReferenceAttributeDrawer.cs
namespace Quantum.Editor {

  using System;
  using UnityEditor;
  using UnityEngine;

  [CustomPropertyDrawer(typeof(MultiTypeReferenceAttribute))]
  public class MultiTypeReferenceAttributeDrawer : PropertyDrawer {

    public override void OnGUI(Rect rect, SerializedProperty prop, GUIContent label) {
      QuantumEditorGUI.MultiTypeObjectField(rect, prop, label, Types);
    }

    public Type[] Types {
      get {
        var attrib = (MultiTypeReferenceAttribute)attribute;
        return attrib.Types;
      }
    }
  }
}
#endregion

#region quantum_unity/Assets/Photon/Quantum/Editor/PropertyDrawers/NullableFixedPropertyDrawer.cs
namespace Quantum.Editor {
  using UnityEditor;
  using UnityEngine;
  using Photon.Deterministic;

  [CustomPropertyDrawer(typeof(NullableFP))]
  [CustomPropertyDrawer(typeof(NullableFPVector2))]
  [CustomPropertyDrawer(typeof(NullableFPVector3))]
  public class NullableFPPropertyDrawer : PropertyDrawer {

    private const string HasValueName = nameof(NullableFP._hasValue);
    private const string ValueName = nameof(NullableFP._value);

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {

      EditorGUI.BeginProperty(position, label, property);

      var hasValueProperty = property.FindPropertyRelativeOrThrow(HasValueName);
      var valueProperty = property.FindPropertyRelativeOrThrow(ValueName);

      var hasValue = hasValueProperty.intValue != 0;

      var toggleRect = EditorGUI.PrefixLabel(position, GUIUtility.GetControlID(FocusType.Passive), label);
      toggleRect.width = Mathf.Min(toggleRect.width, QuantumEditorGUI.CheckboxWidth);
      toggleRect.height = EditorGUIUtility.singleLineHeight;

      using (new EditorGUI.IndentLevelScope(-EditorGUI.indentLevel)) {
        if (EditorGUI.Toggle(toggleRect, GUIContent.none, hasValue) != hasValue) {
          hasValueProperty.intValue = hasValue ? 0 : 1;
          hasValueProperty.serializedObject.ApplyModifiedProperties();
        }
      }

      if (hasValue) {
        EditorGUIUtility.labelWidth += QuantumEditorGUI.CheckboxWidth;
        EditorGUI.PropertyField(position, valueProperty, QuantumEditorGUI.WhitespaceContent);
        EditorGUIUtility.labelWidth -= QuantumEditorGUI.CheckboxWidth;
      }

      EditorGUI.EndProperty();
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label) {

      var hasValueProperty = property.FindPropertyRelativeOrThrow(HasValueName);
      var valueProperty = property.FindPropertyRelativeOrThrow(ValueName);

      if (hasValueProperty.intValue != 0) {
        return EditorGUI.GetPropertyHeight(valueProperty);
      } else {
        return EditorGUI.GetPropertyHeight(hasValueProperty);
      }

    }
  }
}
#endregion

#region quantum_unity/Assets/Photon/Quantum/Editor/PropertyDrawers/PlayerRefDrawer.cs
namespace Quantum.Editor {
  using UnityEditor;
  using UnityEngine;

  [CustomPropertyDrawer(typeof(Quantum.PlayerRef))]
  public class PlayerRefDrawer : PropertyDrawer {

    public override void OnGUI(Rect p, SerializedProperty prop, GUIContent label) {
      EditorGUI.BeginProperty(p, label, prop);
      EditorGUI.BeginChangeCheck();

      var valueProperty = prop.FindPropertyRelativeOrThrow(nameof(PlayerRef._index));
      int value = valueProperty.intValue;

      var toggleRect = EditorGUI.PrefixLabel(p, GUIUtility.GetControlID(FocusType.Passive), label);
      toggleRect.width = Mathf.Min(toggleRect.width, QuantumEditorGUI.CheckboxWidth);

      var hasValue = value > 0;

      using (new EditorGUI.IndentLevelScope(-EditorGUI.indentLevel)) {
        if (EditorGUI.Toggle(toggleRect, GUIContent.none, hasValue) != hasValue) {
          value = hasValue ? 0 : 1;
        }
      }

      if (hasValue) {
        EditorGUIUtility.labelWidth += QuantumEditorGUI.CheckboxWidth;
        value = EditorGUI.IntSlider(p, QuantumEditorGUI.WhitespaceContent, value, 1, Quantum.Input.MAX_COUNT);
        EditorGUIUtility.labelWidth -= QuantumEditorGUI.CheckboxWidth;
      }

      if (EditorGUI.EndChangeCheck()) {
        valueProperty.intValue = value;
      }
      EditorGUI.EndProperty();
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label) {
      return QuantumEditorGUI.GetLinesHeight(1);
    }
  }
}

#endregion

#region quantum_unity/Assets/Photon/Quantum/Editor/PropertyDrawers/PrototypeDrawer.cs
namespace Quantum.Editor {

  using System;
  using System.Collections.Generic;
  using System.Linq;
  using System.Reflection;
  using Quantum.Core;
  using Quantum.Prototypes;
  using UnityEditor;
  using UnityEngine;

  [CustomPropertyDrawer(typeof(StructPrototype), true)]
  [CustomPropertyDrawer(typeof(ComponentPrototype), true)]
  [CustomPropertyDrawer(typeof(PrototypeAdapter), true)]
  [CustomPropertyDrawer(typeof(UnionPrototype), true)]
  [CustomPropertyDrawer(typeof(DictionaryEntryPrototype), true)]
  public partial class PrototypeDrawer : PropertyDrawer {

    private const string UnionSelectionFieldName = "_field_used_";

    private Lazy<PrototypeInfo> _info;

    public PrototypeDrawer() {
      _info = new Lazy<PrototypeInfo>(() => {
        var fieldType = fieldInfo.FieldType.GetUnityLeafType();

        var prototypeAttribute = fieldType.GetCustomAttribute<PrototypeAttribute>();
        if (prototypeAttribute?.Type == null) {
          throw new InvalidOperationException("Only to be used with types having Prototype attribute " + fieldInfo);
        }

        return new PrototypeInfo() {
          PrototypeType = fieldType,
          PrototypedType = prototypeAttribute.Type,
          IsUnion = prototypeAttribute.Type.GetCustomAttribute<UnionAttribute>() != null,
          IsKeyValuePair = prototypeAttribute.Type.IsGenericType && prototypeAttribute.Type.GetGenericTypeDefinition() == typeof(KeyValuePair<,>)
        };
      });
    }

    private PrototypeInfo Info => _info.Value;

    public override bool CanCacheInspectorGUI(SerializedProperty property) {
      return false;
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label) {
      if (Info.PrototypedType.IsEnum) {
        return EditorGUIUtility.singleLineHeight;
      } else if (Info.IsUnion) {
        return GetUnionHeight(property, Info.PrototypeType);
      } else {
        return QuantumEditorGUI.GetPropertyHeight(property, label, true);
      }
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
      using (new QuantumEditorGUI.PropertyScope(position, label, property)) {
        if (Info.PrototypedType.IsEnum) {
          DoEnumGUI(position, property.FindPropertyRelativeOrThrow(QuantumEditorGUI.DictionaryValuePropertyName), label, Info.PrototypedType);
        } else if (Info.IsUnion) {
          DoUnionGUI(position, property, label, Info.PrototypeType);
        } else if (Info.IsKeyValuePair) {
          DoKeyValuePairGUI(position, property, label);
        } else {
          QuantumEditorGUI.PropertyField(position, property, label, property.isExpanded);
        }
      }
    }

    private static void DoKeyValuePairGUI(Rect position, SerializedProperty property, GUIContent label) {
      QuantumEditorGUI.PropertyField(position, property, label, property.isExpanded);

      if (!property.isExpanded) {
        var keyProp = property.FindPropertyRelativeOrThrow(QuantumEditorGUI.DictionaryKeyPropertyName);
        using (new EditorGUI.IndentLevelScope(-EditorGUI.indentLevel))
        using (new EditorGUI.DisabledScope(true)) {
          QuantumEditorGUI.PropertyField(position.AddXMin(EditorGUIUtility.labelWidth), keyProp, GUIContent.none, true);
        }
      }
    }

    private static void DoEnumGUI(Rect position, SerializedProperty property, GUIContent label, Type enumType) {
      long[] values;
      {
        var rawValues = Enum.GetValues(enumType);
        values = new long[rawValues.Length];
        for (int i = 0; i < rawValues.Length; ++i)
          values[i] = Convert.ToInt64(rawValues.GetValue(i));
      }

      List<int> selectedIndices = new List<int>();

      var names = Enum.GetNames(enumType);
      var underlyingType = Enum.GetUnderlyingType(enumType);
      var currentValue = property.longValue;
      var isFlags = enumType.GetCustomAttribute<FlagsAttribute>() != null;

      // find out what to show

      for (int i = 0; i < values.Length; ++i) {
        if (!isFlags) {
          if (currentValue == values[i]) {
            selectedIndices.Add(i);
            break;
          }
        } else if ((currentValue & values[i]) == values[i]) {
          selectedIndices.Add(i);
        }
      }

      string labelValue;
      if (selectedIndices.Count == 0) {
        if (isFlags && currentValue == 0) {
          labelValue = "Nothing";
        } else {
          labelValue = "";
        }
      } else if (selectedIndices.Count == 1) {
        labelValue = names[selectedIndices[0]];
      } else {
        Debug.Assert(isFlags);
        if (selectedIndices.Count == values.Length) {
          labelValue = "Everything";
        } else {
          labelValue = string.Join("|", selectedIndices.Select(x => names[x]));
        }
      }

      var r = EditorGUI.PrefixLabel(position, label);
      if (EditorGUI.DropdownButton(r, new GUIContent(labelValue), FocusType.Keyboard)) {
        if (isFlags) {
          var allOptions = new[] { "Nothing", "Everything" }.Concat(names).ToArray();
          List<int> allIndices = new List<int>();
          if (selectedIndices.Count == 0)
            allIndices.Add(0); // nothing
          else if (selectedIndices.Count == values.Length)
            allIndices.Add(1); // everything
          allIndices.AddRange(selectedIndices.Select(x => x + 2));

          UnityInternal.EditorUtility.DisplayCustomMenu(r, allOptions, allIndices.ToArray(), (userData, options, selected) => {
            if (selected == 0) {
              property.longValue = 0;
            } else if (selected == 1) {
              property.longValue = 0;
              foreach (var value in values) {
                property.longValue |= value;
              }
            } else {
              selected -= 2;
              if (selectedIndices.Contains(selected)) {
                property.longValue &= (~values[selected]);
              } else {
                property.longValue |= values[selected];
              }
            }
            property.serializedObject.ApplyModifiedProperties();
          }, null);
        } else {
          UnityInternal.EditorUtility.DisplayCustomMenu(r, names, selectedIndices.ToArray(), (userData, options, selected) => {
            if (!selectedIndices.Contains(selected)) {
              property.longValue = values[selected];
              property.serializedObject.ApplyModifiedProperties();
            }
          }, null);
        }
      }
    }

    private static SerializedProperty DoUnionFieldPopup(Rect rect, SerializedProperty unionProperty, Type type) {
      var displayName = "Field Used";
      var fieldUsedProperty = unionProperty.FindPropertyRelativeOrThrow(UnionSelectionFieldName);

      var fields = GetUnionFields(type).OrderBy(x => x.Name).ToArray();

      var values = new[] { "(None)" }.Concat(fields.Select(x => x.Name.ToUpperInvariant())).ToArray();

      var selectedIndex = Array.IndexOf(values, fieldUsedProperty.stringValue);

      // fallback to "(None)"
      if (selectedIndex < 0 && string.IsNullOrEmpty(fieldUsedProperty.stringValue)) {
        selectedIndex = 0;
      }

      EditorGUI.BeginProperty(rect, new GUIContent(displayName), fieldUsedProperty);
      EditorGUI.BeginChangeCheck();

      selectedIndex = EditorGUI.Popup(rect, displayName, selectedIndex, values);

      if (EditorGUI.EndChangeCheck()) {
        fieldUsedProperty.stringValue = selectedIndex == 0 ? "" : values[selectedIndex];
      }
      EditorGUI.EndProperty();

      return selectedIndex > 0 ? unionProperty.FindPropertyRelativeOrThrow(fields[selectedIndex - 1].Name) : null;
    }

    private static void DoUnionGUI(Rect position, SerializedProperty property, GUIContent label, Type type) {
      var rect = position.SetLineHeight();

      if (QuantumEditorGUI.PropertyField(rect, property, false)) {
        using (new EditorGUI.IndentLevelScope()) {
          rect = rect.AddLine();
          var selectedProperty = DoUnionFieldPopup(rect, property, type);
          if (selectedProperty != null) {
            QuantumEditorGUI.PropertyField(position.SetYMin(rect.yMax + EditorGUIUtility.standardVerticalSpacing), selectedProperty, true);
          }
        }
      }
    }

    private static IEnumerable<FieldInfo> GetUnionFields(Type type) {
      return type.GetFields().Where(x => x.Name != UnionSelectionFieldName);
    }

    private static float GetUnionHeight(SerializedProperty property, Type type) {
      if (!property.isExpanded) {
        return EditorGUIUtility.singleLineHeight;
      }

      var selectedProperty = GetUnionSelectedProperty(property, type);
      var height = QuantumEditorGUI.GetLinesHeightWithNarrowModeSupport(2);

      if (selectedProperty != null) {
        height += QuantumEditorGUI.GetPropertyHeight(selectedProperty) + EditorGUIUtility.standardVerticalSpacing;
      }

      return height;
    }

    private static SerializedProperty GetUnionSelectedProperty(SerializedProperty unionRoot, Type type) {
      var fieldUsedProperty = unionRoot.FindPropertyRelativeOrThrow(UnionSelectionFieldName);

      foreach (var f in GetUnionFields(type)) {
        if (f.Name.ToUpperInvariant() == fieldUsedProperty.stringValue) {
          return unionRoot.FindPropertyRelativeOrThrow(f.Name);
        }
      }

      return null;
    }

    private struct PrototypeInfo {
      public bool IsUnion;
      public bool IsKeyValuePair;
      public Type PrototypedType;
      public Type PrototypeType;
    }
  }
}
#endregion

#region quantum_unity/Assets/Photon/Quantum/Editor/PropertyDrawers/QBooleanDrawer.cs
namespace Quantum.Editor {
  using UnityEditor;
  using UnityEngine;

  [CustomPropertyDrawer(typeof(QBoolean))]
  public class QBooleanDrawer : PropertyDrawer {
    public override void OnGUI(Rect p, SerializedProperty prop, GUIContent label) {
      prop = GetValueProperty(prop);

      EditorGUI.BeginChangeCheck();
      bool value = EditorGUI.Toggle(p, label, prop.GetIntegerValue() != 0);
      if (EditorGUI.EndChangeCheck()) {
        prop.SetIntegerValue(value ? 1 : 0);
      }
    }

    public static SerializedProperty GetValueProperty(SerializedProperty root) {
      var prop = root.Copy();
      prop.Next(true);
      Debug.Assert(prop.name == nameof(QBoolean.Value));
      return prop;
    }
  }
}
#endregion

#region quantum_unity/Assets/Photon/Quantum/Editor/PropertyDrawers/QStringDrawer.cs
namespace Quantum.Editor {
  using System.Text;
  using UnityEditor;
  using UnityEngine;

  public partial class QStringDrawer : PropertyDrawer {

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {

      Encoding encoding;
      {
        var fieldType = fieldInfo.FieldType;
        if (fieldType.GetInterface($"Quantum.{nameof(IQStringUtf8)}") != null) {
          encoding = Encoding.UTF8;
        } else if (fieldType.GetInterface($"Quantum.{nameof(IQString)}") != null) {
          encoding = Encoding.Unicode;
        } else {
          throw new System.NotSupportedException($"Unknown string type: {fieldType.FullName}");
        }
      }

      var bytesProperty = property.FindPropertyRelativeOrThrow("Bytes");
      var byteCountProperty = property.FindPropertyRelativeOrThrow("ByteCount");

      Debug.Assert(bytesProperty.isFixedBuffer);

      int byteCount = Mathf.Min(byteCountProperty.intValue, bytesProperty.fixedBufferSize);

      byte[] buffer = new byte[byteCount];
      for (int i = 0; i < byteCount; ++i) {
        buffer[i] = (byte)bytesProperty.GetFixedBufferElementAtIndex(i).intValue;
      }

      var str = encoding.GetString(buffer, 0, byteCount);

      using (new QuantumEditorGUI.PropertyScope(position, label, property)) {
        position = EditorGUI.PrefixLabel(position, label);

        EditorGUI.BeginChangeCheck();

        using (new EditorGUI.IndentLevelScope(-EditorGUI.indentLevel)) {
          str = EditorGUI.TextField(position, str);
        }

        QuantumEditorGUI.Overlay(position, $"({byteCount} B)");

        if (EditorGUI.EndChangeCheck()) {
          buffer = encoding.GetBytes(str);
          byteCount = Mathf.Min(buffer.Length, bytesProperty.fixedBufferSize);
          for (int i = 0; i < byteCount; ++i) {
            bytesProperty.GetFixedBufferElementAtIndex(i).intValue = buffer[i];
          }
          byteCountProperty.intValue = byteCount;
          property.serializedObject.ApplyModifiedProperties();
        }
      }
    }
  }
}
#endregion

#region quantum_unity/Assets/Photon/Quantum/Editor/PropertyDrawers/QuantumInspectorAttributeDrawer.cs
namespace Quantum.Editor {
  using UnityEditor;
  using UnityEngine;

  [CustomPropertyDrawer(typeof(QuantumInspectorAttribute))]
  public class QuantumInspectorAttributeDrawer : PropertyDrawer {

    public override bool CanCacheInspectorGUI(SerializedProperty property) {
      return false;
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
      using (new QuantumEditorGUI.PropertyScope(position, label, property)) {
        QuantumEditorGUI.PropertyField(position, property, label, property.isExpanded);
      }
    }


    public override float GetPropertyHeight(SerializedProperty property, GUIContent label) {
      return QuantumEditorGUI.GetPropertyHeight(property, label, property.isExpanded);
    }
  }
}

#endregion

#region quantum_unity/Assets/Photon/Quantum/Editor/PropertyDrawers/QuantumPropertyAttributeDrawers.cs
namespace Quantum.Editor {
  using System;
  using Photon.Deterministic;
  using UnityEditor;
  using UnityEngine;

  [CustomPropertyDrawer(typeof(ProxyAttribute))]
  internal class LayerAttributeDrawer : PropertyDrawer {

    private class ProxyAttribute : QuantumPropertyAttributeProxyAttribute {
      public ProxyAttribute(Quantum.Inspector.LayerAttribute attribute) : base(attribute) {}
    }

    public override void OnGUI(Rect p, SerializedProperty prop, GUIContent label) {
      EditorGUI.BeginChangeCheck();

      int value;

      using (new QuantumEditorGUI.ShowMixedValueScope(prop.hasMultipleDifferentValues)) {
        value = EditorGUI.LayerField(p, label, prop.intValue);
      }

      if ( EditorGUI.EndChangeCheck()) {
        prop.intValue = value;
        prop.serializedObject.ApplyModifiedProperties();
      }
    }
  }


  [CustomPropertyDrawer(typeof(ProxyAttribute))]
  internal sealed class RangeAttributeDrawer : PropertyDrawer {

    private class ProxyAttribute : QuantumPropertyAttributeProxyAttribute {
      public ProxyAttribute(Quantum.Inspector.RangeAttribute attribute) : base(attribute) { }
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
      var range = this.GetQuantumAttribute<Quantum.Inspector.RangeAttribute>();
      if (fieldInfo.FieldType.GetUnityLeafType() == typeof(FP)) {
        FPPropertyDrawer.DrawAsSlider(position, property, label, range.Min.ToFP(), range.Max.ToFP());
      } else if (property.propertyType == SerializedPropertyType.Integer)
        EditorGUI.IntSlider(position, property, (int)range.Min, (int)range.Max, label);
      else
        EditorGUI.LabelField(position, label.text, "Use Range with FP or int.");
    }
  }

  [CustomPropertyDrawer(typeof(ProxyAttribute))]
  internal class MaxStringByteCountAttributeDrawer : PropertyDrawer {

    private class ProxyAttribute : QuantumPropertyAttributeProxyAttribute {
      public ProxyAttribute(Quantum.Inspector.MaxStringByteCountAttribute attribute) : base(attribute) { }
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {

      var attribute = this.GetQuantumAttribute<Quantum.Inspector.MaxStringByteCountAttribute>();

      using (new QuantumEditorGUI.PropertyScope(position, label, property)) {

        position = EditorGUI.PrefixLabel(position, label);

        var encoding = System.Text.Encoding.GetEncoding(attribute.Encoding);

        var byteCount = encoding.GetByteCount(property.stringValue);

        using (new EditorGUI.IndentLevelScope(-EditorGUI.indentLevel)) {
          EditorGUI.PropertyField(position, property, GUIContent.none, false);
        }

        QuantumEditorGUI.Overlay(position, $"({byteCount} B)");
        if (byteCount > attribute.ByteCount) {
          QuantumEditorGUI.Decorate(position, $"{attribute.Encoding} string max size ({attribute.ByteCount} B) exceeded: {byteCount} B", MessageType.Error);
        }
      }
    }
  }


  [CustomPropertyDrawer(typeof(ProxyAttribute))]
  public class DegreesAttributeDrawer : PropertyDrawer {

    private class ProxyAttribute : QuantumPropertyAttributeProxyAttribute {
      public ProxyAttribute(Quantum.Inspector.DegreesAttribute attribute) : base(attribute) { }
    }


    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
      var leafType = fieldInfo.FieldType.GetUnityLeafType();
      if (leafType == typeof(Photon.Deterministic.FP)) {
        // 2D-rotation is special and needs to go through inversion, if using XZ plane
        using (new QuantumEditorGUI.PropertyScope(position, label, property)) {
          FPPropertyDrawer.DrawAs2DRotation(position, property, label);
        }
      } else if (leafType == typeof(FPVector2)) {
        FPVector2PropertyDrawer.DrawCompact(position, property, label);
      } else if (leafType == typeof(FPVector3)) {
        FPVector3PropertyDrawer.DrawCompact(position, property, label);
      } else {
        throw new NotSupportedException(leafType.FullName);
      }
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label) {
      var leafType = fieldInfo.FieldType.GetUnityLeafType();
      if (leafType == typeof(Photon.Deterministic.FP)) {
        return EditorGUIUtility.singleLineHeight;
      } else if (leafType == typeof(FPVector2)) {
        return QuantumEditorGUI.GetLinesHeightWithNarrowModeSupport(1);
      } else if (leafType == typeof(FPVector3)) {
        return QuantumEditorGUI.GetLinesHeightWithNarrowModeSupport(1);
      } else {
        throw new NotSupportedException(leafType.FullName);
      }
    }
  }

  [CustomPropertyDrawer(typeof(ProxyAttribute))]
  public class ComponentTypeNameAttributeDrawer : PropertyDrawer {

    private class ProxyAttribute : QuantumPropertyAttributeProxyAttribute {
      public ProxyAttribute(Quantum.Inspector.ComponentTypeNameAttribute attribute) : base(attribute) { }
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {

      using (new QuantumEditorGUI.PropertyScopeWithPrefixLabel(position, label, property, out position)) {

        string error = null;
        GUIContent valueLabel = GUIContent.none;

        if (QuantumEditorUtility.TryGetComponentType(property.stringValue, out var type)) {
          valueLabel = new GUIContent(type.Name);
        } else {
          valueLabel = new GUIContent(property.stringValue);
          error = $"Component not found: {property.stringValue}";
        }

        Rect dropdownRect = position;

        if (type != null) {
          dropdownRect.xMin = QuantumEditorGUI.ComponentThumbnailPrefix(position, type, true).x;
        }

        if (EditorGUI.DropdownButton(dropdownRect, valueLabel, FocusType.Keyboard)) {
          QuantumEditorGUI.ShowComponentTypePicker(dropdownRect, type, t => {
            Debug.Log(property.stringValue);
            property.stringValue = t.Name;
            property.serializedObject.ApplyModifiedProperties();
          }, clear: () => {
            property.stringValue = default;
            property.serializedObject.ApplyModifiedProperties();
          });
        }

        if (error != null) {
          QuantumEditorGUI.Decorate(position, error, MessageType.Error);
        }
      }
    }
  }
}

#endregion

#region quantum_unity/Assets/Photon/Quantum/Editor/PropertyDrawers/ShapeConfigDrawer.cs
namespace Quantum.Editor {
  using UnityEditor;
  using UnityEngine;

  [CustomPropertyDrawer(typeof(Shape2DConfig))]
  [CustomPropertyDrawer(typeof(Shape2DConfig.CompoundShapeData2D))]
  [CustomPropertyDrawer(typeof(Shape3DConfig))]
  [CustomPropertyDrawer(typeof(Shape3DConfig.CompoundShapeData3D))]
  public class ShapeConfigDrawer : PropertyDrawer {

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
      using (new QuantumEditorGUI.PropertyScope(position, label, property)) {

        position = position.SetLineHeight();

        Debug.Assert(nameof(Shape2DConfig.ShapeType)               == nameof(Shape3DConfig.ShapeType));
        Debug.Assert(nameof(Shape2DConfig.IsSetFromSourceCollider) == nameof(Shape3DConfig.IsSetFromSourceCollider));
        
        var shapeType               = property.FindPropertyRelativeOrThrow(nameof(Shape2DConfig.ShapeType));
        var isSetFromSourceCollider = property.FindPropertyRelativeOrThrow(nameof(Shape2DConfig.IsSetFromSourceCollider));
        
        using (new EditorGUI.DisabledScope(disabled: isSetFromSourceCollider.boolValue)) {
          using (new QuantumEditorGUI.PropertyScope(position, label, shapeType)) {
            var p = EditorGUI.PrefixLabel(position, label);

            var error = GetShapeTypeError(shapeType.intValue, out var fatal);

            using (new EditorGUI.IndentLevelScope(-EditorGUI.indentLevel)) {
              QuantumEditorGUI.PropertyField(p, shapeType, GUIContent.none, false);
              if (!string.IsNullOrEmpty(error)) {
                QuantumEditorGUI.Decorate(p, error, fatal ? MessageType.Error : MessageType.Warning);
              }
            }
          }
        }

        position = position.AddLine();
        
        Debug.Assert(nameof(Shape2DConfig.UserTag) == nameof(Shape3DConfig.UserTag));

        EditorGUI.indentLevel++;
        foreach (var prop in property.Children()) {
          if (prop.name != nameof(Shape2DConfig.ShapeType)) {
            using (new EditorGUI.DisabledScope(disabled: isSetFromSourceCollider.boolValue && prop.name != nameof(Shape2DConfig.UserTag))) {
              position.height = QuantumEditorGUI.GetPropertyHeight(prop);
              if (position.height > 0) {
                QuantumEditorGUI.PropertyField(position, prop, null, true);

                position.y += position.height + EditorGUIUtility.standardVerticalSpacing;
              }
            }
          }
        }
        EnforcePresistentFlag(shapeType.intValue, property);
        EditorGUI.indentLevel--;
      }
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label) {
      property.isExpanded = true;
      return QuantumEditorGUI.GetPropertyHeight(property, label, true) - EditorGUIUtility.singleLineHeight - EditorGUIUtility.standardVerticalSpacing;
    }
    public override bool CanCacheInspectorGUI(SerializedProperty property) {
      return false;
    }

    private string GetShapeTypeError(int shapeType, out bool fatal) {
      if (shapeType == 0) {
        fatal = false;
        return "Please select a shape type.";
      }

      fatal = true;

      var type = fieldInfo.FieldType.GetUnityLeafType();
      if (type == typeof(Shape2DConfig.CompoundShapeData2D) && shapeType == (int)Shape2DType.Compound
        || type == typeof(Shape3DConfig.CompoundShapeData3D) && shapeType == (int)Shape3DType.Compound) {
        return "Nested compound shapes are not supported.";
      }

      if ((type == typeof(Shape3DConfig.CompoundShapeData3D) || type == typeof(Shape3DConfig))
        && (shapeType == (int)Shape3DType.Mesh || shapeType == (int)Shape3DType.Terrain)) {
        return "Shape type not supported for dynamic colliders/entities.";
      }

      return null;
    }
    
    private void EnforcePresistentFlag(int shapeType, SerializedProperty prop) {
      var type = fieldInfo.FieldType.GetUnityLeafType();
      if (type == typeof(Shape2DConfig) && shapeType == (int)Shape2DType.Compound ||
          type == typeof(Shape3DConfig) && shapeType == (int)Shape3DType.Compound) {
        var persistentProp = prop.FindPropertyRelativeOrThrow(nameof(Shape2DConfig.IsPersistent));
        if ( persistentProp.boolValue != true ) {
          persistentProp.boolValue = true;
          persistentProp.serializedObject.ApplyModifiedProperties();
        }
      }
    }
  }
}

#endregion