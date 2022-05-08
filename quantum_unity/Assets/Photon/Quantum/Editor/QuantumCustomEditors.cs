

#region quantum_unity/Assets/Photon/Quantum/Editor/CustomEditors/AssetBaseEditor.cs
// Disable this class completely by setting this define in the Unity player settings
#if !DISABLE_QUANTUM_ASSET_INSPECTOR

namespace Quantum.Editor {
  using System;
  using UnityEditor;
  using UnityEngine;

  [CustomEditor(typeof(AssetBase), true)]
  [CanEditMultipleObjects]
  public class AssetBaseEditor : UnityEditor.Editor {

    private static readonly GUIContent _label = new GUIContent(AssetBase.DefaultAssetObjectPropertyPath);

    public override void OnInspectorGUI() {
      if (!QuantumEditorSettings.Instance.UseQuantumAssetInspector) {
        // Soft deactivate the Quantum asset editor
        base.OnInspectorGUI();
        return;
      }

      AssetBase target = (AssetBase)base.target;

      using (new QuantumEditorGUI.CustomEditorScope(serializedObject)) {
        try {
          // call on-gui before
          target.OnInspectorGUIBefore(serializedObject);
        } catch (System.Exception exn) {
          UnityEngine.Debug.LogException(exn);
        }
        
        // This draws all fields except the "Settings" properties.
        QuantumEditorGUI.Inspector(serializedObject, filters: new[] { AssetBase.DefaultAssetObjectPropertyPath }, drawScript: false);

        // Retrieve name of the nested Quantum asset class.
        var headline = "Quantum Asset Inspector";
        try {
          headline = ObjectNames.NicifyVariableName(target.AssetObject.GetType().Name);
        } catch (Exception) { }

        using (new QuantumEditorGUI.BoxScope(headline, serializedObject)) {
          var property = serializedObject.FindPropertyOrThrow(target.AssetObjectPropertyPath);
          QuantumEditorGUI.Inspector(property, label: _label);
        }

        try {
          // call on-gui after
          ((AssetBase)target).OnInspectorGUIAfter(serializedObject);
        } catch (System.Exception exn) {
          UnityEngine.Debug.LogException(exn);
        }
      }
    }
  }
}

#endif
#endregion

#region quantum_unity/Assets/Photon/Quantum/Editor/CustomEditors/BinaryDataAssetEditor.cs
namespace Quantum.Editor {
  using System;
  using System.IO;
  using System.Text;
  using UnityEditor;
  using UnityEngine;

  [CustomEditor(typeof(BinaryDataAsset), true)]
  [CanEditMultipleObjects]
  public class BinaryDataAssetEditor : AssetBaseEditor {

    class PropertyPaths : SerializedPropertyPathBuilder<BinaryDataAsset> {
      public static readonly string Settings        = GetPropertyPath(asset => asset.Settings);
      public static readonly string SourceTextAsset = GetPropertyPath(asset => asset.SourceTextAsset);
      public static readonly string IsCompressed    = GetPropertyPath(asset => asset.Settings.IsCompressed);
      public static readonly string Data            = GetPropertyPath(asset => asset.Settings.Data);
    };


    const int MaxLength = 2048;

    private Lazy<GUIStyle> _textStyle = new Lazy<GUIStyle>(() => {
      return new GUIStyle(EditorStyles.textArea) {
        wordWrap = true
      };
    });

    public override void OnInspectorGUI() {

      if (!QuantumEditorSettings.Instance.UseQuantumAssetInspector) {
        base.OnInspectorGUI();
        return;
      }

      QuantumEditorGUI.Inspector(serializedObject, new string[] { PropertyPaths.Settings, PropertyPaths.SourceTextAsset });

      var headline = "Quantum Asset Inspector";
      try {
        headline = ObjectNames.NicifyVariableName(nameof(BinaryData));
      } catch { }

      using (new QuantumEditorGUI.BoxScope(headline)) {

        var sourceProp = serializedObject.FindPropertyOrThrow(PropertyPaths.SourceTextAsset);
        var compressedProp = serializedObject.FindPropertyOrThrow(PropertyPaths.IsCompressed);
        var dataProp = serializedObject.FindPropertyOrThrow(PropertyPaths.Data);

        EditorGUI.BeginChangeCheck();
        EditorGUILayout.PropertyField(sourceProp);
        if (EditorGUI.EndChangeCheck()) {
          if (sourceProp.objectReferenceValue != null) {
            compressedProp.boolValue = false;
            dataProp.arraySize = 0;
          }
          serializedObject.ApplyModifiedProperties();
        }

        EditorGUILayout.Space();

        using (new EditorGUI.DisabledScope(sourceProp.objectReferenceValue && !sourceProp.hasMultipleDifferentValues)) { 
          using (new EditorGUILayout.HorizontalScope()) {
            using (new EditorGUI.DisabledScope(compressedProp.boolValue && !compressedProp.hasMultipleDifferentValues)) {
              if (GUILayout.Button("Compress")) {
                foreach (BinaryDataAsset asset in targets) {
                  Undo.RecordObject(asset, "Compressing");
                  asset.SetData(asset.Settings.Data, true);
                  EditorUtility.SetDirty(asset);
                }
              }
            }

            using (new EditorGUI.DisabledScope(!compressedProp.boolValue && !compressedProp.hasMultipleDifferentValues)) {
              if (GUILayout.Button("Decompress")) {
                using (var stream = new MemoryStream()) {
                  foreach (BinaryDataAsset asset in targets) {
                    asset.Store(stream);
                    Undo.RecordObject(asset, "Decompressing");
                    asset.SetData(stream.ToArray(), false);
                    EditorUtility.SetDirty(asset);
                  }
                }
              }
            }
            using (new EditorGUI.DisabledScope(targets.Length > 1)) {
              if (GUILayout.Button("Export")) {
                var asset = (BinaryDataAsset)target;
                string path = EditorUtility.SaveFilePanel("Export BinaryData", string.Empty, asset.name, "bytes");
                if (!string.IsNullOrEmpty(path)) {
                  using (var stream = System.IO.File.Create(path)) {
                    asset.Store(stream);
                  }
                }
              }
              if (GUILayout.Button("Import")) {
                var asset = (BinaryDataAsset)target;
                string path = EditorUtility.OpenFilePanel("Import BinaryData", string.Empty, "bytes");
                if (!string.IsNullOrEmpty(path)) {
                  var bytes = System.IO.File.ReadAllBytes(path);
                  Undo.RecordObject(asset, "Loading");
                  asset.SetData(bytes, false);
                  EditorUtility.SetDirty(asset);
                }
              }
            }
          }


          EditorGUI.showMixedValue = targets.Length > 1;

          long size = dataProp.arraySize;
          if (sourceProp.objectReferenceValue) {
            var path = AssetDatabase.GetAssetPath(sourceProp.objectReferenceValue);
            if (!string.IsNullOrEmpty(path) && File.Exists(path)) {
              size = new FileInfo(path).Length;
            }
          }

          EditorGUILayout.LongField("Size", size);

          if (!serializedObject.isEditingMultipleObjects) {
            dataProp.isExpanded = EditorGUILayout.Foldout(dataProp.isExpanded, "Contents");
            if (dataProp.isExpanded) {
              var asset = (BinaryDataAsset)target;

              var data = asset?.Settings?.Data;

              StringBuilder sb = new StringBuilder();
              for (int i = 0; i < data.Length; ++i) {
                sb.AppendFormat("{0:x2} ", data[i]);
                if (i >= MaxLength) {
                  sb.AppendLine("...\n\n<... etc...>");
                  break;
                }
              }

              EditorGUILayout.TextArea(sb.ToString(), _textStyle.Value);
            }
          }
        }
      }

      EditorGUILayout.HelpBox("Hint: you can select multiple BinaryDataAssets to perform Compression/Decomprocession for multiple assets.", MessageType.Info);    
    }
  }
}
#endregion

#region quantum_unity/Assets/Photon/Quantum/Editor/CustomEditors/DeterministicSessionConfigAssetEditor.cs
namespace Quantum.Editor {
  using System;
  using UnityEditor;
  using UnityEngine;

  [CustomEditor(typeof(DeterministicSessionConfigAsset))]
  public class DeterministicSessionConfigAssetEditor : UnityEditor.Editor {
    const String PREFS_KEY = "$SHOW_QUANTUM_CONFIG_HELP$";

    public override void OnInspectorGUI() {
      var asset = target as DeterministicSessionConfigAsset;
      if (asset) {
        OnInspectorGUI(asset);
      }
    }

    void OnInspectorGUI(DeterministicSessionConfigAsset asset) {
      using (new QuantumEditorGUI.BoxScope("Deterministic Session Config")) {

        EditorGUI.BeginChangeCheck();

        EditorPrefs.SetBool(PREFS_KEY, EditorGUILayout.Toggle("Show Help Info", EditorPrefs.GetBool(PREFS_KEY, true)));

        using (new QuantumEditorGUI.SectionScope("Simulation")) {

          asset.Config.UpdateFPS = Math.Max(1, EditorGUILayout.IntField("Simulation Rate", asset.Config.UpdateFPS));
          HelpBox("How many ticks per second Quantum should execute.");

          asset.Config.LockstepSimulation = EditorGUILayout.Toggle("Lockstep", asset.Config.LockstepSimulation);
          HelpBox("Runs the quantum simulation in lockstep mode, where no rollbacks are performed. It is recommended to set Input Offset Min to at least 10.");

          EditorGUI.BeginDisabledGroup(asset.Config.LockstepSimulation);
          asset.Config.RollbackWindow = Math.Max(1, EditorGUILayout.IntField("Rollback Window", asset.Config.RollbackWindow));
          HelpBox("How many frames are kept in the local ring buffer on each client. Controls how much Quantum can predict into the future. Not used in lockstep mode.");
          EditorGUI.EndDisabledGroup();

          asset.Config.ChecksumInterval = Math.Max(0, EditorGUILayout.IntField("Checksum Interval", asset.Config.ChecksumInterval));
          HelpBox("How often we should send checksums of the frame state to the server for verification (useful during development, set to zero for release). Defined in frames.");

          EditorGUI.BeginDisabledGroup(asset.Config.ChecksumInterval == 0);
          asset.Config.ChecksumCrossPlatformDeterminism = EditorGUILayout.Toggle("Checksum Crossplatform Determinism (Slow)", asset.Config.ChecksumCrossPlatformDeterminism);
          HelpBox("This allows quantums frame checksumming to be deterministic across different runtime platforms, however it comes with quite a cost and should only be used during debugging.");
          EditorGUI.EndDisabledGroup();
        }

        using (new QuantumEditorGUI.SectionScope("Input")) {

          asset.Config.AggressiveSendMode = EditorGUILayout.Toggle("Aggressive Send", asset.Config.AggressiveSendMode);
          HelpBox("If the server should skip buffering and perform aggressive input sends, only suitable for games with <= 4 players.");

          asset.Config.InputDelayMin = Math.Max(0, EditorGUILayout.IntField("Offset Min", asset.Config.InputDelayMin));
          HelpBox("The minimum input offset a player can have");

          asset.Config.InputDelayMax = Math.Max(asset.Config.InputDelayMin + 1, EditorGUILayout.IntField("Offset Max", asset.Config.InputDelayMax));
          HelpBox("The maximum input offset a player can have");

          asset.Config.InputDelayPingStart = Math.Max(0, EditorGUILayout.IntField("Offset Ping Start", asset.Config.InputDelayPingStart));
          HelpBox("At what ping value that Quantum starts applying input offset. Defined in millseconds.");

          //asset.Config.InputPacking = Math.Max(1, EditorGUILayout.IntField("Send Rate", asset.Config.InputPacking));
          //HelpBox("How often Quantum sends input to the server. 1 = Every frame, 2 = Every other frame, etc.");

          asset.Config.InputRedundancy = Math.Max(1, EditorGUILayout.IntField("Send Redundancy", asset.Config.InputRedundancy));
          HelpBox("How many packets each input generated by the client is sent in");

          asset.Config.InputRepeatMaxDistance = Math.Max(0, EditorGUILayout.IntField("Repeat Max Distance", asset.Config.InputRepeatMaxDistance));
          HelpBox("How many frames Quantum will scan for repeatable inputs. 5 = Scan five frames forward and backwards, 10 = Scan ten frames, etc.");

          asset.Config.InputHardTolerance = Math.Max(-10, EditorGUILayout.IntField("Hard Tolerance", asset.Config.InputHardTolerance));
          HelpBox("How many frames the server will wait until it expires a frame and replaces all non-received inputs with repeated inputs or null's and sends it out to all players.");

          asset.Config.MinOffsetCorrectionDiff = Math.Max(1, EditorGUILayout.IntField("Offset Correction Limit", asset.Config.MinOffsetCorrectionDiff));
          HelpBox("How many frames the current local input delay must diff to the current requested offset for Quantum to update the local input offset. Defined in frames.");

          asset.Config.InputFixedSizeEnabled = EditorGUILayout.Toggle("Fixed Size", asset.Config.InputFixedSizeEnabled);
          HelpBox("If the input data has a fixed byte length, enabling this saves bandwidth.");

        }

        using (new QuantumEditorGUI.SectionScope("Time")) {

          asset.Config.TimeCorrectionRate = Math.Max(0, EditorGUILayout.IntField("Correction Send Rate", asset.Config.TimeCorrectionRate));
          HelpBox("How many times per second the server will send out time correction packages to make sure every clients time is synchronized.");

          asset.Config.MinTimeCorrectionFrames = Math.Max(0, EditorGUILayout.IntField("Correction Frames Limit", asset.Config.MinTimeCorrectionFrames));
          HelpBox("How much the local client time must differ with the server time when a time correction package is received for the client to adjust it's local clock. Defined in frames.");

          asset.Config.SessionStartTimeout = Mathf.Clamp(EditorGUILayout.IntField("Room Wait Time (seconds)", asset.Config.SessionStartTimeout), 0, 30);
          HelpBox("How long the Quantum server will wait for the room to become full until it forces a start of the Quantum session. Defined in seconds.");

          asset.Config.TimeScaleMin = Mathf.Clamp(EditorGUILayout.IntField("Time Scale Minimum (%)", asset.Config.TimeScaleMin), 10, 100);
          HelpBox("The smallest timescale that can be applied by the server. Defined in percent.");

          asset.Config.TimeScalePingMin = Mathf.Clamp(EditorGUILayout.IntField("Time Scale Ping Start (ms)", asset.Config.TimeScalePingMin), 0, 1000);
          HelpBox("The ping value that the server will start lowering the time scale towards 'Time Scale Minimum'. Defined in milliseconds.");

          asset.Config.TimeScalePingMax = Mathf.Clamp(EditorGUILayout.IntField("Time Scale Ping End (ms)", asset.Config.TimeScalePingMax), asset.Config.TimeScalePingMin + 1, 1000);
          HelpBox("The ping value that the server will reach the 'Time Scale Minimum' value at, i.e. be at its slowest setting. Defined in milliseconds.");

        }
      }

      if (EditorGUI.EndChangeCheck()) {
        EditorUtility.SetDirty(asset);
      }
    }

    void WarnBox(String format, params System.Object[] args) {
      if (EditorPrefs.GetBool(PREFS_KEY, true)) {
        EditorGUILayout.HelpBox(String.Format(format, args), MessageType.Warning);
        EditorGUILayout.Space();
      }
    }

    void WarnBoxAlways(String format, params System.Object[] args) {
      EditorGUILayout.HelpBox(String.Format(format, args), MessageType.Warning);
      EditorGUILayout.Space();
    }

    void HelpBox(String format, params System.Object[] args) {
      if (EditorPrefs.GetBool(PREFS_KEY, true)) {
        EditorGUILayout.HelpBox(String.Format(format, args), MessageType.Info);
        EditorGUILayout.Space();
      }
    }
  }

}
#endregion

#region quantum_unity/Assets/Photon/Quantum/Editor/CustomEditors/EntityComponentBaseEditor.cs
namespace Quantum.Editor {
  using System;
  using System.Diagnostics;
  using UnityEditor;
  using UnityEngine;

  [CustomEditor(typeof(EntityComponentBase), true)]
  [CanEditMultipleObjects]
  public class EntityComponentBaseEditor : UnityEditor.Editor {

    void OnEnable() {
      if (!QuantumEditorSettings.Instance.UseQuantumAssetInspector) {
        return;
      }

      if (QuantumEditorSettings.Instance.EntityComponentInspectorMode == QuantumEntityComponentInspectorMode.InlineInEntityPrototypeAndHideMonoBehaviours) {
        UnityInternal.Editor.InternalSetHidden(this, true);
      }
    }


    public override void OnInspectorGUI() {
      if (!QuantumEditorSettings.Instance.UseQuantumAssetInspector) {
        base.OnInspectorGUI();
        return;
      }

      using (new QuantumEditorGUI.CustomEditorScope(serializedObject)) {
        if (QuantumEditorSettings.Instance.EntityComponentInspectorMode != QuantumEntityComponentInspectorMode.ShowMonoBehaviours) {
          bool comparisonPopup = false;
          var trace = new StackFrame(1);
          if (trace?.GetMethod()?.DeclaringType.Name.EndsWith("ComparisonViewPopup") == true) {
            comparisonPopup = true;
          }
          if (!comparisonPopup)
            return;
        }

        using (new EditorGUI.DisabledScope(Application.isPlaying)) {
#pragma warning disable CS0618 // Type or member is obsolete
          DrawFields(serializedObject);
#pragma warning restore CS0618 // Type or member is obsolete
          ((EntityComponentBase)target).OnInspectorGUI(serializedObject, QuantumEditorGUI.ProxyInstance);
        }
      }
    }

    [Obsolete("Use EntityComponentBase.OnInspectorGUI instead")]
    protected virtual void DrawFields(SerializedObject so) {
    }
  }
}


#endregion

#region quantum_unity/Assets/Photon/Quantum/Editor/CustomEditors/EntityPrototypeAssetEditor.cs
namespace Quantum.Editor {
  using System.Linq;
  using UnityEditor;
  using UnityEngine;

  [CustomEditor(typeof(EntityPrototypeAsset), false)]
  [CanEditMultipleObjects]
  public class EntityPrototypeAssetEditor : NestedAssetBaseEditor {
    [MenuItem("Assets/Create/Quantum/EntityPrototype", priority = EditorDefines.AssetMenuPriorityStart + 5 * 26)]
    public static void CreateMenuItem() => NestedAssetBaseEditor.CreateNewAssetMenuItem<EntityPrototypeAsset>();
  }

  [CustomPropertyDrawer(typeof(AssetRefEntityPrototype))]
  public class EntityPrototypeLinkPropertyDrawer : PropertyDrawer {
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) => NestedAssetBaseEditor.AssetLinkOnGUI<EntityPrototypeAsset>(position, property, label);
  }
}

#endregion

#region quantum_unity/Assets/Photon/Quantum/Editor/CustomEditors/EntityPrototypeEditor.cs
namespace Quantum.Editor {
  using System;
  using System.Collections.Generic;
  using System.Linq;
  using System.Reflection;
  using Photon.Deterministic;
  using UnityEditor;
  using UnityEngine;

  using EntityView = global::EntityView;
  using EntityPrototype = global::EntityPrototype;


  [CustomEditor(typeof(EntityPrototype), false)]
  [CanEditMultipleObjects]
  public class EntityPrototypeEditor : UnityEditor.Editor {

    private static readonly HashSet<Type> excludedComponents = new HashSet<Type>(new[] {
      typeof(EntityComponentTransform2D),
      typeof(EntityComponentTransform2DVertical),
      typeof(EntityComponentTransform3D),
      typeof(EntityComponentPhysicsCollider2D),
      typeof(EntityComponentPhysicsBody2D),
      typeof(EntityComponentPhysicsCollider3D),
      typeof(EntityComponentPhysicsBody3D),
      typeof(EntityComponentNavMeshPathfinder),
      typeof(EntityComponentNavMeshSteeringAgent),
      typeof(EntityComponentNavMeshAvoidanceAgent),
      typeof(EntityComponentView),
    });

    private static readonly Type[] componentPrototypeTypes = AppDomain.CurrentDomain.GetAssemblies()
      .SelectMany( x => x.GetTypes())
      .Where(x => !x.IsAbstract)
      .Where(x => x.BaseType?.IsSubclassOf(typeof(EntityComponentBase)) == true)
      .Where(x => x.GetCustomAttribute<ObsoleteAttribute>() == null)
      .Where(x => !excludedComponents.Contains(x))
      .ToArray();

    private static readonly GUIContent[] transformPopupOptions = new[] {
      new GUIContent("2D"),
      new GUIContent("3D"),
      new GUIContent("None"),
    };

    

    private static Lazy<Skin> _skin = new Lazy<Skin>(() => new Skin());

    private static Skin skin => _skin.Value;

    private static readonly int[] transformPopupValues = new[] {
      (int)EntityPrototypeTransformMode.Transform2D,
      (int)EntityPrototypeTransformMode.Transform3D,
      (int)EntityPrototypeTransformMode.None
    };

    private class Skin {
      public readonly int titlebarHeight = 20;
      public readonly int imageIconSize = 16;
      public readonly int imagePadding = EditorStyles.foldout.padding.left + 1;
      public readonly GUIStyle boldFoldout = new GUIStyle(EditorStyles.foldout) {
        fontStyle = FontStyle.Bold,
        padding = new RectOffset(EditorStyles.foldout.padding.left + 16 + 2, 0, 2, 0),
      };
      public readonly GUIStyle inspectorTitlebar = new GUIStyle("IN Title") {
        alignment = TextAnchor.MiddleLeft
      };
      public readonly string cross = "\u2715";
      public readonly float buttonHeight = EditorGUIUtility.singleLineHeight;
      public readonly float buttonWidth = 19.0f;

      public Color inspectorTitlebarBackground =>
        EditorGUIUtility.isProSkin ? new Color32(64, 64, 64, 255) : new Color32(222, 222, 222, 255);
    }

    private static readonly Lazy<GUIStyle> watermarkStyle = new Lazy<GUIStyle>(() => {
      var result = new GUIStyle(EditorStyles.miniLabel);
      result.alignment = TextAnchor.MiddleRight;
      result.contentOffset = new Vector2(-2, 0);
      Color c = result.normal.textColor;
      c.a = 0.6f;
      result.normal.textColor = c;
      return result;
    });

    private static readonly GUIContent physicsCollider2D = new GUIContent(nameof(Quantum.PhysicsCollider2D));
    private static readonly GUIContent physicsCollider3D = new GUIContent(nameof(Quantum.PhysicsCollider3D));
    private static readonly GUIContent physicsBody2D = new GUIContent(nameof(Quantum.PhysicsBody2D));
    private static readonly GUIContent physicsBody3D = new GUIContent(nameof(Quantum.PhysicsBody3D));
    private static readonly GUIContent navMeshPathfinder = new GUIContent(nameof(Quantum.NavMeshPathfinder));
    private static readonly GUIContent navMeshSteeringAgent = new GUIContent(nameof(Quantum.NavMeshSteeringAgent));
    private static readonly GUIContent navMeshAvoidanceAgent = new GUIContent(nameof(Quantum.NavMeshAvoidanceAgent));
    private static readonly GUIContent navMeshAvoidanceObstacle = new GUIContent(nameof(Quantum.NavMeshAvoidanceObstacle));

    public override void OnInspectorGUI() {
      if (!QuantumEditorSettings.Instance.UseQuantumAssetInspector) {
        base.OnInspectorGUI();
        return;
      }

      var target = (EntityPrototype)this.target;
      QuantumEditorGUI.ScriptPropertyField(serializedObject);

      if (AssetDatabase.IsMainAsset(target.gameObject)) {
        if (NestedAssetBaseEditor.GetNested(target, out EntityPrototypeAsset asset)) {
          using (new QuantumEditorGUI.BoxScope(nameof(EntityPrototypeAsset))) {
            QuantumEditorGUI.Inspector(new SerializedObject(asset), asset.AssetObjectPropertyPath, new[] { asset.GetAssetPropertyPath(nameof(Quantum.EntityPrototype.Container)) });
          }
        }
      }


      if (Application.isPlaying) {
        EditorGUILayout.HelpBox("Prototypes are only used for entity instantiation. To inspect an actual entity check its EntityView.", MessageType.Info);
      }

      using (new EditorGUI.DisabledScope(Application.isPlaying)) {

        // draw enum popup manually, because this way we can reorder and not follow naming rules
        EntityPrototypeTransformMode? transformMode;
        {
          var prop = serializedObject.FindPropertyOrThrow(nameof(target.TransformMode));
          var rect = EditorGUILayout.GetControlRect();
          var label = new GUIContent("Transform");

          using (new QuantumEditorGUI.PropertyScope(rect, label, prop)) {
            EditorGUI.BeginChangeCheck();
            var value = EditorGUI.IntPopup(rect, label, prop.intValue, transformPopupOptions, transformPopupValues);
            if (EditorGUI.EndChangeCheck()) {
              prop.intValue = value;
              prop.serializedObject.ApplyModifiedProperties();
            }
            transformMode = prop.hasMultipleDifferentValues ? null : (EntityPrototypeTransformMode?)value;
          }
        }

        bool is2D = transformMode == EntityPrototypeTransformMode.Transform2D;
        bool is3D = transformMode == EntityPrototypeTransformMode.Transform3D;

        EditorGUI.BeginChangeCheck();

        try {

          var verticalProperty = serializedObject.FindPropertyOrThrow(nameof(target.Transform2DVertical));
          QuantumEditorGUI.PropertyField(verticalProperty, new GUIContent("Transform2DVertical"), true);

          // EntityPrototypes is space optimised and shares some 2D/3D settings; because of that some
          // extra miles need to be taken to draw things in the right context
          var physicsProperty = serializedObject.FindPropertyOrThrow(nameof(target.PhysicsCollider));
          QuantumEditorGUI.Inspector(physicsProperty, label: is3D ? physicsCollider3D : physicsCollider2D, skipRoot: false, callback: (p, field, type) => {
            if (type == typeof(Component)) {
              if (is3D) {
                QuantumEditorGUI.MultiTypeObjectField(p, new GUIContent(p.displayName), typeof(BoxCollider), typeof(SphereCollider));
              }
              if (is2D) {
                QuantumEditorGUI.MultiTypeObjectField(p, new GUIContent(p.displayName), typeof(BoxCollider), typeof(SphereCollider), typeof(BoxCollider2D), typeof(CircleCollider2D));
              }
              return true;
            }

            if (!is2D && type == typeof(Shape2DConfig) || !is3D && type == typeof(Shape3DConfig)) {
              p.isExpanded = false;
              return true;
            }
            return false;
          });

          var physicsBodyProperty = serializedObject.FindPropertyOrThrow(nameof(target.PhysicsBody));
          QuantumEditorGUI.Inspector(physicsBodyProperty, skipRoot: false, label: is3D ? physicsBody3D : physicsBody2D, callback: (p, field, type) => {
            if (is2D) {
              if (type == typeof(FPVector3) || type == typeof(PhysicsBody3D.ConfigFlags) || type == typeof(RotationFreezeFlags)) {
                p.isExpanded = false;
                return true;
              }
            }

            if (is3D) {
              if (type == typeof(FPVector2) || type == typeof(PhysicsBody2D.ConfigFlags)) {
                p.isExpanded = false;
                return true;
              }
            }

            return false;
          });

          // NavMeshes can be pointed to in 3 ways: scene reference, asset ref and scene name
          var navmeshConfigGuid = 0L;
          var navMeshPathfinderProperty = serializedObject.FindPropertyOrThrow(nameof(target.NavMeshPathfinder));
          QuantumEditorGUI.Inspector(navMeshPathfinderProperty, skipRoot: false, label: navMeshPathfinder, callback: (p, field, type) => {
            if (type == typeof(AssetRefNavMeshAgentConfig)) {
              var valueProperty = p.FindPropertyRelativeOrThrow(AssetRefDrawer.RawValuePath);
              navmeshConfigGuid = valueProperty.longValue;
            }
            if (type == typeof(EntityPrototype.NavMeshSpec)) {
              HandleNavMeshSpec(EditorGUILayout.GetControlRect(), p, new GUIContent(p.displayName));
              p.isExpanded = false;
              return true;
            }
            return false;
          });

          var navMeshSteeringAgentProperty = serializedObject.FindPropertyOrThrow(nameof(target.NavMeshSteeringAgent));
          QuantumEditorGUI.Inspector(navMeshSteeringAgentProperty, skipRoot: false, label: navMeshSteeringAgent, callback: (p, field, type) => {
            if (type == typeof(AssetRefNavMeshAgentConfig)) {
              var valueProperty = p.FindPropertyRelativeOrThrow(AssetRefDrawer.RawValuePath);
              if (valueProperty.longValue == 0) {
                valueProperty.longValue = navmeshConfigGuid;
              }
            }
            return false;
          });

          var navMeshAvoidaceAgent = serializedObject.FindPropertyOrThrow(nameof(target.NavMeshAvoidanceAgent));
          QuantumEditorGUI.Inspector(navMeshAvoidaceAgent, skipRoot: false, label: navMeshAvoidanceAgent, callback: (p, field, type) => {
            if (type == typeof(AssetRefNavMeshAgentConfig)) {
              var valueProperty = p.FindPropertyRelativeOrThrow(AssetRefDrawer.RawValuePath);
              if (valueProperty.longValue == 0) {
                valueProperty.longValue = navmeshConfigGuid;
              }
            }
            return false;
          });

          // View can be either taken from same GameObject or fallback to asset ref
          {
            var viewProperty = serializedObject.FindPropertyOrThrow(nameof(target.View));
            var hasView = target.GetComponent<global::EntityView>() != null;
            var rect = EditorGUILayout.GetControlRect(true);
            var label = new GUIContent(viewProperty.displayName);

            using (new QuantumEditorGUI.PropertyScope(rect, label, viewProperty)) {
              rect = EditorGUI.PrefixLabel(rect, label);
              using (new EditorGUI.IndentLevelScope(-EditorGUI.indentLevel)) {
                if (hasView) {
                  EditorGUI.LabelField(rect, "Self");
                } else {
                  EditorGUI.PropertyField(rect, viewProperty, GUIContent.none);
                }
              }
            }
          }

          // add new component dropdown
          if (QuantumEditorSettings.Instance.EntityComponentInspectorMode == QuantumEntityComponentInspectorMode.ShowMonoBehaviours) {
            using (new EditorGUILayout.HorizontalScope()) {
              var existingComponentPrototypes = targets.OfType<EntityPrototype>().SelectMany(x => x.GetComponents<EntityComponentBase>())
                .Select(x => x.GetType())
                .Distinct()
                .ToList();

              var availableComponents = componentPrototypeTypes
                .Where(x => !existingComponentPrototypes.Contains(x))
                .ToList();

              using (new EditorGUI.DisabledScope(availableComponents.Count == 0)) {
                GUIStyle style = EditorStyles.miniPullDown;
                var content = new GUIContent("Add Entity Component");
                var rect = EditorGUI.IndentedRect(GUILayoutUtility.GetRect(content, style));
                if (EditorGUI.DropdownButton(rect, content, FocusType.Keyboard, style)) {
                  EditorUtility.DisplayCustomMenu(rect, availableComponents.Select(x => new GUIContent(x.Name)).ToArray(), -1,
                    (userData, opts, selected) => {
                      foreach (EntityPrototype t in targets) {
                        Undo.AddComponent(t.gameObject, availableComponents[selected]);
                      }
                      Repaint();
                    }, null);
                }
              }
            }
          }
        } finally {
          if (EditorGUI.EndChangeCheck()) {
            serializedObject.ApplyModifiedProperties();
          }
        }
      }

      try {
        target.PreSerialize();
      } catch (System.Exception ex) {
        EditorGUILayout.HelpBox(ex.Message, MessageType.Error);
      }

      target.CheckComponentDuplicates(msg => {
        EditorGUILayout.HelpBox(msg, MessageType.Warning);
      });

      if (QuantumEditorSettings.Instance.EntityComponentInspectorMode != QuantumEntityComponentInspectorMode.ShowMonoBehaviours) {
        
        using (new EditorGUILayout.VerticalScope(GUI.skin.box)) {
          var components = target.GetComponents<EntityComponentBase>()
            .Where(x => x != null);

          { 
            var labelRect = EditorGUILayout.GetControlRect(true);
            EditorGUI.LabelField(labelRect, "Entity Components", EditorStyles.boldLabel);

            var buttonRect = labelRect.AddX(labelRect.width).AddX(-skin.buttonWidth).SetWidth(skin.buttonWidth);
            if (GUI.Button(buttonRect, "+", EditorStyles.miniButton)) {

              var existingComponentPrototypes = components
                .Select(x => x.GetType())
                .ToList();

              var availableComponents = componentPrototypeTypes
                .Where(x => !existingComponentPrototypes.Contains(x))
                .ToList();

              EditorUtility.DisplayCustomMenu(buttonRect, availableComponents.Select(x => new GUIContent(EntityComponentBase.UnityComponentTypeToQuantumComponentType(x).Name)).ToArray(), -1,
                  (userData, opts, selected) => {
                    Undo.AddComponent(target.gameObject, availableComponents[selected]);
                    Repaint();
                  }, null);
            }
          }

          using (new EditorGUI.IndentLevelScope()) {
            foreach (var c in components) {

              var so = new SerializedObject(c);
              var sp = so.GetIterator();

              var rect = GUILayoutUtility.GetRect(GUIContent.none, skin.inspectorTitlebar);
              sp.isExpanded = EditorGUI.InspectorTitlebar(rect, sp.isExpanded, c, true);

              // draw over the default label, as it contains useless noise
              Rect textRect = new Rect(rect.x + 35, rect.y, rect.width - 100, rect.height);
              if (Event.current.type == EventType.Repaint) {

                using (new QuantumEditorGUI.ColorScope(skin.inspectorTitlebarBackground)) {
                  var texRect = textRect;
                  texRect.y += 2;
                  texRect.height -= 2;
                  GUI.DrawTextureWithTexCoords(texRect, Texture2D.whiteTexture, new Rect(0.5f, 0.5f, 0.0f, 0.0f), false);
                }

                skin.inspectorTitlebar.Draw(textRect, c.ComponentType.Name, false, false, false, false);
              }

              if (sp.isExpanded) {
                c.OnInspectorGUI(so, QuantumEditorGUI.ProxyInstance);
              }
            }
          }
        }
      }
    }

    private static void HandleNavMeshSpec(Rect position, SerializedProperty property, GUIContent label) {
      var referenceProp = property.FindPropertyRelativeOrThrow("Reference");
      var assetProp = property.FindPropertyRelativeOrThrow("Asset");
      var nameProp = property.FindPropertyRelativeOrThrow("Name");

      using (new QuantumEditorGUI.PropertyScope(position, label, property)) {
        var rect = EditorGUI.PrefixLabel(position, label);
        using (new EditorGUI.IndentLevelScope(-EditorGUI.indentLevel)) {
          if (referenceProp.objectReferenceValue != null) {
            EditorGUI.PropertyField(rect, referenceProp, GUIContent.none);
          } else if (assetProp.FindPropertyRelativeOrThrow("Id.Value").longValue > 0) {
            EditorGUI.PropertyField(rect, assetProp, GUIContent.none);
          } else if (!string.IsNullOrEmpty(nameProp.stringValue)) {
            EditorGUI.PropertyField(rect, nameProp, GUIContent.none);
            GUI.Label(rect, "(NavMesh name)", watermarkStyle.Value);
          } else {
            rect.width /= 3;
            EditorGUI.PropertyField(rect, referenceProp, GUIContent.none);
            EditorGUI.PropertyField(rect.AddX(rect.width), assetProp, GUIContent.none);
            EditorGUI.PropertyField(rect.AddX(2 * rect.width), nameProp, GUIContent.none);
            GUI.Label(rect.AddX(2 * rect.width), "(NavMesh name)", watermarkStyle.Value);
          }
        }
      }
    }

    private static bool DoesTypeRequireComponent(Type obj, Type requirement) {
      return Attribute.GetCustomAttributes(obj, typeof(RequireComponent)).OfType<RequireComponent>()
             .Any(rc => rc.m_Type0.IsAssignableFrom(requirement));
    }

    internal static IEnumerable<Component> GetDependentComponents(GameObject go, Type t) {
      return go.GetComponents<Component>().Where(c => DoesTypeRequireComponent(c.GetType(), t));
    }
  }
}
#endregion

#region quantum_unity/Assets/Photon/Quantum/Editor/CustomEditors/EntityViewAssetEditor.cs
namespace Quantum.Editor {

  using UnityEditor;
  using UnityEngine;

  [CustomEditor(typeof(EntityViewAsset), false)]
  [CanEditMultipleObjects]
  public class EntityViewAssetEditor : NestedAssetBaseEditor {
    [MenuItem("Assets/Create/Quantum/EntityView", priority = EditorDefines.AssetMenuPriorityStart + 5 * 26)]
    public static void CreateMenuItem() => NestedAssetBaseEditor.CreateNewAssetMenuItem<EntityViewAsset>();
  }

  [CustomPropertyDrawer(typeof(AssetRefEntityView))]
  public class EntityViewLinkPropertyDrawer : PropertyDrawer {
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) => NestedAssetBaseEditor.AssetLinkOnGUI<EntityViewAsset>(position, property, label);
  }
}

#endregion

#region quantum_unity/Assets/Photon/Quantum/Editor/CustomEditors/EntityViewEditor.cs
namespace Quantum.Editor {
  using UnityEditor;
  using UnityEngine;
#if UNITY_2021_2_OR_NEWER
  using UnityEditor.SceneManagement;
#else
  using UnityEditor.Experimental.SceneManagement;
#endif

  using EntityView = global::EntityView;
  using EntityPrototype = global::EntityPrototype;

  [CustomEditor(typeof(EntityView), true)]
  [CanEditMultipleObjects]
  public class EntityViewEditor : UnityEditor.Editor {

    private QuantumSimulationObjectInspectorState _inspectorState = new QuantumSimulationObjectInspectorState();
    private QuantumSimulationObjectInspector _inspector = new QuantumSimulationObjectInspector();
    private bool _foldout;

    public override unsafe void OnInspectorGUI() {

      if (!QuantumEditorSettings.Instance.UseQuantumAssetInspector) {
        base.OnInspectorGUI();
        return;
      }

      var target = (EntityView)base.target;
      using (new QuantumEditorGUI.CustomEditorScope(serializedObject)) {

        if (!serializedObject.isEditingMultipleObjects) {
          if (!EditorApplication.isPlaying) {
            bool isOnScene = target.gameObject.scene.IsValid() && PrefabStageUtility.GetPrefabStage(target.gameObject) == null;

            if (isOnScene) {
              bool hasPrototype = target.gameObject.GetComponent<EntityPrototype>();
              if (!hasPrototype) {
                using (new EditorGUILayout.VerticalScope(GUI.skin.box)) {
                  EditorGUILayout.HelpBox($"This {nameof(EntityView)} will never be bound to any Entity. Add {nameof(EntityPrototype)} and bake map data.", MessageType.Warning);
                  if (GUILayout.Button("Fix")) {
                    Undo.AddComponent<EntityPrototype>(target.gameObject);
                  }
                }
              }
            }
          }

          if (AssetDatabase.IsMainAsset(target.gameObject)) {
            if (NestedAssetBaseEditor.GetNested(target, out EntityViewAsset asset)) {
              using (new QuantumEditorGUI.BoxScope(nameof(EntityViewAsset))) {
                QuantumEditorGUI.Inspector(new SerializedObject(asset), asset.AssetObjectPropertyPath);
              }
            }
          }
        }

        QuantumEditorGUI.Inspector(serializedObject, drawScript: false);

        if (QuantumRunner.Default == null)
          return;

        if (!serializedObject.isEditingMultipleObjects) {
          using (new EditorGUILayout.HorizontalScope()) {
            EditorGUILayout.PrefixLabel("Quantum Entity Id");
            EditorGUILayout.SelectableLabel(target.EntityRef.ToString(), EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
          }


          _foldout = EditorGUILayout.Foldout(_foldout, "Quantum Entity Root");
          if (_foldout) {
            using (new GUILayout.VerticalScope(GUI.skin.box)) {
              _inspectorState.FromEntity(QuantumRunner.Default.Game.Frames.Predicted, target.EntityRef);
              using (new EditorGUI.IndentLevelScope(-EditorGUI.indentLevel)) {
                _inspector.DoGUILayout(_inspectorState, false);
              }
            }
          }
        }
      }
    }
  }
}
#endregion

#region quantum_unity/Assets/Photon/Quantum/Editor/CustomEditors/MapAssetEditor.cs
namespace Quantum.Editor {
  using Photon.Deterministic;
  using UnityEditor;

  [CustomEditor(typeof(MapAsset), true)]
  [CanEditMultipleObjects]
  public class MapAssetEditor : AssetBaseEditor {
    class PropertyPaths : SerializedPropertyPathBuilder<MapAsset> {
      public static readonly string Settings    = GetPropertyPath(asset => asset.Settings);
      public static readonly string MapEntities = GetPropertyPath(asset => asset.Settings.MapEntities);
      public static readonly string Prototypes  = GetPropertyPath(asset => asset.Prototypes);
    };
    

    public override void OnInspectorGUI() {
      if (!QuantumEditorSettings.Instance.UseQuantumAssetInspector) {
        // Soft deactivate the Quantum asset editor
        base.OnInspectorGUI();
      }
      else {
        QuantumEditorGUI.Inspector(serializedObject, new string[] { PropertyPaths.Settings, PropertyPaths.Prototypes });
        using (new QuantumEditorGUI.BoxScope("Map")) {
          // This draws all fields except the "Settings" and script.
          QuantumEditorGUI.Inspector(serializedObject.FindPropertyOrThrow(PropertyPaths.Settings), filters: new [] { PropertyPaths.MapEntities }, skipRoot: true);
          QuantumEditorGUI.Inspector(serializedObject.FindPropertyOrThrow(PropertyPaths.Prototypes), skipRoot: false);
        }
      }

      foreach (MapAsset data in targets) {
        if (data.Settings.BucketsCount < 1) {
          data.Settings.BucketsCount = 1;
        }

        if (data.Settings.BucketsSubdivisions < 1) {
          data.Settings.BucketsSubdivisions = 1;
        }

        if (data.Settings.TriangleMeshCellSize < 2) {
          data.Settings.TriangleMeshCellSize = 2;
        }

        if ((data.Settings.TriangleMeshCellSize & 1) == 1) {
          data.Settings.TriangleMeshCellSize += 1;
        }

        if (data.Settings.WorldSize < 4) {
          data.Settings.WorldSize = 4;
        } else if (data.Settings.WorldSize > FP.UseableMax / 2) {
          if (
            (data.Settings.BucketingAxis == PhysicsCommon.BucketAxis.X && data.Settings.SortingAxis == PhysicsCommon.SortAxis.X) ||
            (data.Settings.BucketingAxis == PhysicsCommon.BucketAxis.Y && data.Settings.SortingAxis == PhysicsCommon.SortAxis.Y)) {
            data.Settings.WorldSize = (FP.UseableMax / 2).AsInt;
          } else if (data.Settings.WorldSize > FP.UseableMax) {
            data.Settings.WorldSize = FP.UseableMax.AsInt;
          }
        }

        if (data.Settings.GridSizeX < 2) {
          data.Settings.GridSizeX = 2;
        }

        if (data.Settings.GridSizeY < 2) {
          data.Settings.GridSizeY = 2;
        }

        if ((data.Settings.GridSizeX & 1) == 1) {
          data.Settings.GridSizeX += 1;
        }

        if ((data.Settings.GridSizeY & 1) == 1) {
          data.Settings.GridSizeY += 1;
        }

        if (data.Settings.GridNodeSize < 2) {
          data.Settings.GridNodeSize = 2;
        }

        if ((data.Settings.GridNodeSize & 1) == 1) {
          data.Settings.GridNodeSize += 1;
        }
      }
    }
  }
}
#endregion

#region quantum_unity/Assets/Photon/Quantum/Editor/CustomEditors/MapDataEditor.cs
namespace Quantum.Editor {
  using UnityEditor;
  using UnityEngine;

  [CustomEditor(typeof(MapData), true)]
  public class MapDataEditor : UnityEditor.Editor {
    public override void OnInspectorGUI() {
      var data = target as MapData;
      if (data != null) {
        // Never move the map center
        data.transform.position = Vector3.zero;

        using (new QuantumEditorGUI.CustomEditorScope(serializedObject)) {
          using (new QuantumEditorGUI.BoxScope("Map Data")) {
            QuantumEditorGUI.Inspector(serializedObject, drawScript: false);

            if (data.Asset) {
              using (new EditorGUI.DisabledGroupScope(EditorApplication.isPlayingOrWillChangePlaymode)) {
                using (new QuantumEditorGUI.BackgroundColorScope(Color.green)) {

                  var buttonHeight = GUILayout.Height(EditorGUIUtility.singleLineHeight * 1.5f);

                  if (GUILayout.Button("Bake Map Only", buttonHeight)) {
                    HandleBakeButton(data, QuantumMapDataBakeFlags.BakeMapData | QuantumMapDataBakeFlags.GenerateAssetDB);
                  }
                  if (GUILayout.Button("Bake Map Prototypes", buttonHeight)) {
                    HandleBakeButton(data, QuantumMapDataBakeFlags.BakeMapPrototypes);
                  }
                  if (GUILayout.Button("Bake All", buttonHeight)) {
                    HandleBakeButton(data, data.BakeAllMode);
                  }
                }

                using (var checkScope = new EditorGUI.ChangeCheckScope()) {
                  data.BakeAllMode = (QuantumMapDataBakeFlags)EditorGUILayout.EnumFlagsField("Bake All Mode", data.BakeAllMode);
                  if (checkScope.changed) {
                    EditorUtility.SetDirty(data);
                  }
                }
              }
            }
          }

          if (data.Asset) {
            // Draw map asset inspector
            if (QuantumEditorSettings.Instance.UseQuantumAssetInspector) {
              var objectEditor = CreateEditor(data.Asset, typeof(MapAssetEditor));
              objectEditor.OnInspectorGUI();
            } else {
              CreateEditor(data.Asset).DrawDefaultInspector();
            }
          }
        }
      }
    }

    void HandleBakeButton(MapData data, QuantumMapDataBakeFlags bakeFlags) {
      Undo.RecordObject(target, "Bake Map: " + bakeFlags);

      QuantumAutoBaker.BakeMap(data, bakeFlags);

      GUIUtility.ExitGUI();
    }
  }
}
#endregion

#region quantum_unity/Assets/Photon/Quantum/Editor/CustomEditors/MapNavMeshDefinitionEditor.cs
#if !QUANTUM_DISABLE_AI
namespace Quantum.Editor {
  using UnityEngine;
  using UnityEditor;
  using System.Linq;
  using System.Collections.Generic;
  using UnityEditor.SceneManagement;
  using Photon.Deterministic;

  [CustomEditor(typeof(MapNavMeshDefinition))]
  public partial class MapNavMeshDefinitionEditor : UnityEditor.Editor {
    public static bool Editing;
    public List<string> SelectedVertices = new List<string>();

    static bool _navMeshSurfaceTypeSearched;
    static System.Type _navMeshSurfaceType;

    static System.Type NavMeshSurfaceType {
      get {
        // TypeUtils.FindType can be quite slow
        if (_navMeshSurfaceTypeSearched == false) {
          _navMeshSurfaceTypeSearched = true;
          _navMeshSurfaceType = TypeUtils.FindType("NavMeshSurface");
        }

        return _navMeshSurfaceType;
      }
    }

    static string NewId() {
      return System.Guid.NewGuid().ToString();
    }

    public override void OnInspectorGUI() {
      var data = target as MapNavMeshDefinition;

      using (new QuantumEditorGUI.CustomEditorScope(serializedObject))
      using (new QuantumEditorGUI.BoxScope("Map NavMesh Definition")) {

        EditorGUILayout.HelpBox("If you are only importing the Unity navmesh switch to MapNavMeshUnity.", MessageType.Info);

        if (data) {
#if UNITY_2018_2_OR_NEWER
#pragma warning disable 612, 618
          if (PrefabUtility.GetCorrespondingObjectFromSource(data) == null && PrefabUtility.GetPrefabObject(data) != null) {
#pragma warning restore 612, 618      
#else
        if (PrefabUtility.GetPrefabParent(data) == null && PrefabUtility.GetPrefabObject(data) != null) {
#endif
            EditorGUILayout.HelpBox("The NavMesh Editor does not work on prefabs.", MessageType.Info);
            EditorGUILayout.Space();
            return;
          }

          if (data.Triangles == null) {
            data.Triangles = new MapNavMeshTriangle[0];
            Save();
          }

          if (data.Vertices == null) {
            data.Vertices = new MapNavMeshVertex[0];
            Save();
          }

          var backgroundColorRect = EditorGUILayout.BeginVertical();

          using (new QuantumEditorGUI.SectionScope("Import Unity NavMesh")) {

            if (Editing) GUI.enabled = false;

            using (new QuantumEditorGUI.BackgroundColorScope(Color.green)) {
              if (GUILayout.Button("Bake Unity Navmesh", GUILayout.Height(EditorGUIUtility.singleLineHeight * 2))) {
                MapNavMeshEditor.BakeUnityNavmesh(data.gameObject);
              }

              if (GUILayout.Button("Import from Unity", GUILayout.Height(EditorGUIUtility.singleLineHeight * 2))) {
                Undo.RegisterCompleteObjectUndo(data, "MapNavMeshDefinitionEditor - ImportFromUnity");
                SelectedVertices.Clear();
                MapNavMeshEditor.UpdateDefaultMinAgentRadius();
                ImportFromUnity(data);

                Save();
              }
            }

            using (var changed = new EditorGUI.ChangeCheckScope()) {
              data.WeldIdenticalVertices = EditorGUILayout.Toggle(new GUIContent("Weld Identical Vertices", "The Unity NavMesh is a collection of non-connected triangles, this option is very important and combines shared vertices."), data.WeldIdenticalVertices);
              if (data.WeldIdenticalVertices) {
                data.WeldVertexEpsilon = EditorGUILayout.FloatField(new GUIContent("Weld Vertices Epsilon", "Don't make the epsilon too small, vertices to fuse are missed, also don't make the value too big as it will deform your navmesh."), data.WeldVertexEpsilon);
                data.WeldVertexEpsilon = Mathf.Max(data.WeldVertexEpsilon, Mathf.Epsilon);
              }

              data.DelaunayTriangulation = EditorGUILayout.Toggle(new GUIContent("Delaunay Triangulation", "Post processes imported Unity navmesh with a Delaunay triangulation to reduce long triangles."), data.DelaunayTriangulation);
              if (data.DelaunayTriangulation) {
                data.DelaunayTriangulationRestrictToPlanes = EditorGUILayout.Toggle(new GUIContent("Delaunay Triangulation Restrict To Planes", "In 3D the triangulation can deform the navmesh on slopes, check this option to restrict the triangulation to triangles that lie in the same plane."), data.DelaunayTriangulationRestrictToPlanes);
              }

              data.FixTrianglesOnEdges = EditorGUILayout.Toggle(new GUIContent("Fix Triangles On Edges", "Sometimes vertices will lie on other triangle edges, this will lead to unwanted borders being detected, this option splits those vertices."), data.FixTrianglesOnEdges);
              if (data.FixTrianglesOnEdges) {
                data.FixTrianglesOnEdgesEpsilon = EditorGUILayout.FloatField(new GUIContent("Fix Triangles On Edges Epsilon", "Larger scaled navmeshes may require to increase this value (e.g. 0.001) when false-positive borders are detected."), data.FixTrianglesOnEdgesEpsilon);
                data.FixTrianglesOnEdgesEpsilon = Mathf.Max(data.FixTrianglesOnEdgesEpsilon, float.Epsilon);
              }

              data.ImportRegions = EditorGUILayout.Toggle(new GUIContent("Import Regions", "Toggle the Quantum region import."), data.ImportRegions);
              if (data.ImportRegions) {
                data.RegionDetectionMargin = EditorGUILayout.FloatField(new GUIContent("Region Detection Margin", "The artificial margin is necessary because the Unity NavMesh does not fit the source size very well. The value is added to the navmesh area and checked against all Quantum Region scripts to select the correct region id."), data.RegionDetectionMargin);
                data.RegionDetectionMargin = Mathf.Max(data.RegionDetectionMargin, 0.0f);
                EditorGUILayout.LabelField(new GUIContent("Convert Unity Areas To Quantum Region:", "Select what Unity NavMesh areas are used to generated Quantum regions. At least one must be selected."));
                EditorGUI.indentLevel++;
                var names = new List<string>(GameObjectUtility.GetNavMeshAreaNames());
                if (data.RegionAreaIds == null) {
                  data.RegionAreaIds = new List<int>();
                }

                for (int i = 0; i < data.RegionAreaIds.Count; i++) {
                  var areaId = data.RegionAreaIds[i];
                  var areaName = GameObjectUtility.GetNavMeshAreaNames().FirstOrDefault(name => GameObjectUtility.GetNavMeshAreaFromName(name) == areaId);
                  if (string.IsNullOrEmpty(areaName)) {
                    areaName = "missing Unity NavMesh area";
                  }

                  if (!EditorGUILayout.Toggle(areaName, true)) {
                    data.RegionAreaIds.Remove(areaId);
                  }
                  else {
                    names.Remove(areaName);
                  }
                }

                if (names.Count > 0) {
                  var newName = EditorGUILayout.Popup("Add New Area", -1, names.ToArray());
                  if (newName >= 0) {
                    var areaId = GameObjectUtility.GetNavMeshAreaFromName(names[newName]);
                    data.RegionAreaIds.Add(areaId);
                  }
                }

                EditorGUI.indentLevel--;
              }

              if (changed.changed) {
                Save();
              }
            }

            if (NavMeshSurfaceType != null) {
              serializedObject.Update();
              QuantumEditorGUI.PropertyField(serializedObject.FindProperty("NavMeshSurfaces"), true);
              serializedObject.ApplyModifiedProperties();
            }

            serializedObject.Update();
            QuantumEditorGUI.PropertyField(serializedObject.FindProperty("ClosestTriangleCalculation"), new GUIContent("Closest Triangle Calculation", "SpiralOut will be faster but fallback triangles can be null."));
            serializedObject.ApplyModifiedProperties();

            if (data.ClosestTriangleCalculation == MapNavMesh.FindClosestTriangleCalculation.SpiralOut) {
              serializedObject.Update();
              QuantumEditorGUI.PropertyField(serializedObject.FindProperty("ClosestTriangleCalculationDepth"), new GUIContent("Closest Triangle Calculation Depth", "Number of cells to search triangles in neighbors."));
              serializedObject.ApplyModifiedProperties();
            }

#if QUANTUM_XY
          serializedObject.Update();
          QuantumEditorGUI.PropertyField(serializedObject.FindProperty("EnableQuantum_XY"), new GUIContent("Enable Quantum_XY", "Activate this and the navmesh baking will flip Y and Z to support navmeshes generated in the XY plane."));
          serializedObject.ApplyModifiedProperties();
#endif

            GUI.enabled = true;
          }

          using (new EditorGUI.DisabledScope(true)) {
            serializedObject.Update();
            QuantumEditorGUI.PropertyField(serializedObject.FindProperty("AgentRadius"), new GUIContent("Imported Agent Radius", "This radius is overwritten during import. See Unity Navigation Tab/Bake/AgentRadius."));
            serializedObject.ApplyModifiedProperties();

            if (data.ImportRegions && data.Regions != null && data.Regions.Count > 0) {
              QuantumEditorGUI.PropertyField(serializedObject.FindProperty("Regions"), new GUIContent("Imported Regions", "Imported regions, cannot be overwritten and is reset on during every import."), true);
            }
          }

          using (new QuantumEditorGUI.SectionScope("NavMesh Debug")) {

            EditorGUI.BeginChangeCheck();

            var editorSettings = QuantumEditorSettings.Instance;
            editorSettings.NavMeshDefaultColor            = EditorGUILayout.ColorField("NavMesh Default Color", editorSettings.NavMeshDefaultColor);
            editorSettings.NavMeshRegionColor             = EditorGUILayout.ColorField("NavMesh Region Color",  editorSettings.NavMeshRegionColor);
            editorSettings.DrawNavMeshDefinitionAlways    = EditorGUILayout.Toggle("Toggle Always Draw",      editorSettings.DrawNavMeshDefinitionAlways);
            editorSettings.DrawNavMeshDefinitionMesh      = EditorGUILayout.Toggle("Toggle Draw Mesh",        editorSettings.DrawNavMeshDefinitionMesh);
            editorSettings.DrawNavMeshDefinitionOptimized = EditorGUILayout.Toggle("Toggle Optimized Gizmos", editorSettings.DrawNavMeshDefinitionOptimized);

            if (EditorGUI.EndChangeCheck()) {
              SceneView.RepaintAll();
            }

            if (GUILayout.Button("Export Test Mesh", EditorStyles.miniButton)) {
              Mesh m = data.CreateMesh();
              m.name = "NavMesh";

              AssetDatabase.CreateAsset(m, "Assets/NavMesh.asset");
              AssetDatabase.SaveAssets();
              AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
            }
          }

          using (new QuantumEditorGUI.SectionScope("Edit Quantum NavMesh")) {

            using (new QuantumEditorGUI.BackgroundColorScope(Editing ? Color.red : Color.green)) {
              if (GUILayout.Button(Editing ? "Stop Editing NavMesh" : "Start Editing NavMesh", GUILayout.Height(EditorGUIUtility.singleLineHeight * 2))) {
                Editing = !Editing;
                Save();
                EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
                if (Editing && QuantumEditorSettings.Instance.DrawNavMeshDefinitionOptimized)
                  Debug.LogWarning("Manual NavMesh editing and optimized gizmo drawing (QuantumEditorSettings.DrawNavMeshDefinitionOptimized) does not work together. Deactivate it.");
                if (!Editing)
                  data.InvalidateGizmoMesh();
              }
            }

            if (Editing) {
              if (GUILayout.Button("Add Vertex", EditorStyles.miniButton)) {
                AddVertex(data);
              }

              if (GUILayout.Button("Delete Vertices", EditorStyles.miniButton)) {
                DeleteVertices(data);
              }


              if (SelectedVertices.Count == 1) {
                if (GUILayout.Button("Duplicate Vertex", EditorStyles.miniButton)) {
                  DuplicateVertex(data);
                }
              }

              if (SelectedVertices.Count == 2) {
                if (GUILayout.Button("Insert Vertex + Create Triangle", EditorStyles.miniButton)) {
                  InsertVertexAndCreateTriangle(data);
                }
              }

              if (SelectedVertices.Count == 3) {
                if (GUILayout.Button("Create Triangle", EditorStyles.miniButton)) {
                  CreateTriangle(data);
                }
              }

              if (GUILayout.Button("Duplicate And Flip", EditorStyles.miniButton)) {

                Undo.RegisterCompleteObjectUndo(data, "MapNavMeshDefinitionEditor - Duplicate And Flip");

                var idMap = new Dictionary<string, string>();

                foreach (var k in data.Vertices.ToList()) {
                  var v = k;

                  v.Id         = System.Guid.NewGuid().ToString();
                  v.Position.x = -v.Position.x;
                  v.Position.y = -v.Position.y;
                  v.Position.z = -v.Position.z;

                  idMap.Add(k.Id, v.Id);

                  ArrayUtility.Add(ref data.Vertices, v);
                }

                foreach (var k in data.Triangles.ToList()) {
                  var t = k;

                  t.Id           = System.Guid.NewGuid().ToString();
                  t.VertexIds    = new string[3];
                  t.VertexIds[0] = idMap[k.VertexIds[0]];
                  t.VertexIds[1] = idMap[k.VertexIds[1]];
                  t.VertexIds[2] = idMap[k.VertexIds[2]];

                  ArrayUtility.Add(ref data.Triangles, t);
                }
              }

              if (SelectedVertices.Count == 1) {
                var v = data.GetVertex(SelectedVertices.First());

                EditorGUI.BeginChangeCheck();

                v.Position = EditorGUILayout.Vector3Field("", v.Position);

                if (EditorGUI.EndChangeCheck()) {
                  data.Vertices[data.GetVertexIndex(SelectedVertices.First())] = v;
                }
              }
            }
          }

          using (new QuantumEditorGUI.SectionScope("Quantum NavMesh Information")) {
            EditorGUILayout.HelpBox(string.Format("Vertices: {0}\r\nTriangles: {1}", data.Vertices.Length, data.Triangles.Length), MessageType.Info);
          }

          EditorGUILayout.EndVertical();

          if (Editing) {
            backgroundColorRect = new Rect(backgroundColorRect.x - 13, backgroundColorRect.y - 17, EditorGUIUtility.currentViewWidth, backgroundColorRect.height + 20);
            EditorGUI.DrawRect(backgroundColorRect, new Color(0.5f, 0.0f, 0.0f, 0.1f));
          }
        }
        else {
          Editing = false;
        }
      }
    }

    void Save() {
      EditorUtility.SetDirty(target);
    }

    void OnDisable() {
      try {
        if (Editing && target) {
          Selection.activeGameObject = (target as MapNavMeshDefinition).gameObject;
        }
      }
      catch (System.Exception) { }
    }

    void DuplicateVertex(MapNavMeshDefinition data) {
      if (SelectedVertices.Count == 1) {
        var id = NewId();

        ArrayUtility.Add(ref data.Vertices, new MapNavMeshVertex {
          Id = id,
          Position = data.GetVertex(SelectedVertices.First()).Position.RoundToInt()
        });

        Save();

        SelectedVertices.Clear();
        SelectedVertices.Add(id);
      }
    }

    void AddVertex(MapNavMeshDefinition data) {
      Undo.RegisterCompleteObjectUndo(new UnityEngine.Object[] { data, this }, "MapNavMeshDefinitionEditor - AddVertex");
      ArrayUtility.Add(ref data.Vertices, new MapNavMeshVertex {
        Id = NewId(),
        Position = new Vector3()
      });

      Save();
    }

    void DeleteVertices(MapNavMeshDefinition data) {
      if (SelectedVertices.Count > 0) {
        Undo.RegisterCompleteObjectUndo(new UnityEngine.Object[] { data, this }, "MapNavMeshDefinitionEditor - DeleteVertices");
        data.Vertices = data.Vertices.Where(x => SelectedVertices.Contains(x.Id) == false).ToArray();
        data.Triangles = data.Triangles.Where(x => x.VertexIds.Any(y => SelectedVertices.Contains(y)) == false).ToArray();

        Save();

        SelectedVertices.Clear();
      }
    }

    void CreateTriangle(MapNavMeshDefinition data) {
      if (SelectedVertices.Count() == 3) {
        Undo.RegisterCompleteObjectUndo(new UnityEngine.Object[] { data, this }, "MapNavMeshDefinitionEditor - CreateTriangle");
        ArrayUtility.Add(ref data.Triangles, new MapNavMeshTriangle {
          Id = NewId(),
          VertexIds = SelectedVertices.ToArray(),
          Area = 0,
          RegionId = null,
          Cost = FP._1
        });

        Save();
      }
    }

    void InsertVertexAndCreateTriangle(MapNavMeshDefinition data) {
      if (SelectedVertices.Count() == 2) {
        Undo.RegisterCompleteObjectUndo(new UnityEngine.Object[] { data, this }, "MapNavMeshDefinitionEditor - InsertVertexAndCreateTriangle");
        var id = NewId();
        ArrayUtility.Add(ref data.Vertices, new MapNavMeshVertex {
          Id = id,
          Position =
            Vector3.Lerp(
              data.GetVertex(SelectedVertices.First()).Position,
              data.GetVertex(SelectedVertices.Last()).Position,
              0.5f
            ).RoundToInt()
        });

        SelectedVertices.Add(id);

        CreateTriangle(data);

        SelectedVertices.Clear();
        SelectedVertices.Add(id);

        Save();
      }
    }

    void OnSceneGUI() {
      Tools.current = Tool.None;

      var data = target as MapNavMeshDefinition;

      if (Editing && data) {
        Selection.activeGameObject = (target as MapNavMeshDefinition).gameObject;
      }
      else {
        return;
      }

      if (data) {
        if (Event.current.type == EventType.KeyDown) {
          switch (Event.current.keyCode) {
            case KeyCode.Escape:
              SelectedVertices.Clear();
              break;

            case KeyCode.T:
              switch (SelectedVertices.Count()) {
                case 2:
                  InsertVertexAndCreateTriangle(data);
                  break;

                case 3:
                  CreateTriangle(data);
                  break;

                default:
                  Debug.LogError("Must select 2 or 3 vertices to use 'T' command");
                  break;
              }
              break;

            case KeyCode.X:
              Undo.RegisterCompleteObjectUndo(this, "MapNavMeshDefinitionEditor - Changed selection");
              var select = new HashSet<string>();
              foreach (var tri in data.Triangles) {
                foreach (var v in SelectedVertices) {
                  if (System.Array.IndexOf(tri.VertexIds, v) >= 0) {
                    select.Add(tri.VertexIds[0]);
                    select.Add(tri.VertexIds[1]);
                    select.Add(tri.VertexIds[2]);
                    break;
                  }
                }
              }

              foreach (var v in select) {
                SelectedVertices.Add(v);
              }
              break;

            case KeyCode.Backspace:
              DeleteVertices(data);
              break;

            case KeyCode.F:
              var pos = Vector3.zero;
              foreach (var v in SelectedVertices) pos += data.GetVertex(v).Position;
              pos /= SelectedVertices.Count;
              SceneView.lastActiveSceneView.pivot = pos;
              break;
          }
        }

        // Eat the default focus event. See case KeyCode.F.
        if (Event.current.type == EventType.ExecuteCommand) {
          if (Event.current.commandName == "FrameSelected") {
            Event.current.commandName = "";
            Event.current.Use();
          }
        }

        foreach (var v in data.Vertices) {
          var p = data.transform.TransformPoint(v.Position);
          var r = Quaternion.LookRotation((p - SceneView.currentDrawingSceneView.camera.transform.position).normalized);
          var s = 0f;

          if (SelectedVertices.Contains(v.Id)) {
            s = 0.2f;
            Handles.color = Color.green;
          }
          else {
            s = 0.1f;
            Handles.color = Color.white;
          }

          var handleSize = 0.5f * HandleUtility.GetHandleSize(p);
          if (Handles.Button(p, r, s * handleSize, s * handleSize, Handles.DotHandleCap)) {

            if (Event.current.shift) {
              if (SelectedVertices.Contains(v.Id)) {
                Undo.RegisterCompleteObjectUndo(this, "MapNavMeshDefinitionEditor - Changed selection");
                SelectedVertices.Remove(v.Id);
              }
              else {
                Undo.RegisterCompleteObjectUndo(this, "MapNavMeshDefinitionEditor - Changed selection");
                SelectedVertices.Add(v.Id);
              }
            }
            else {
              Undo.RegisterCompleteObjectUndo(this, "MapNavMeshDefinitionEditor - Changed selection");
              SelectedVertices.Clear();
              SelectedVertices.Add(v.Id);
            }

            Repaint();
          }
        }

        if (SelectedVertices.Count > 0) {
          var center = Vector3.zero;
          var positions = data.Vertices.Where(x => SelectedVertices.Contains(x.Id)).Select(x => data.transform.TransformPoint(x.Position));

          foreach (var p in positions) {
            center += p;
          }

          center /= positions.Count();

          var movedCenter = Handles.DoPositionHandle(center, Quaternion.identity);
          if (movedCenter != center) {
            var m = movedCenter - center;

#if QUANTUM_XY
          m.z = 0;
#else
            m.y = 0;
#endif

            Undo.RegisterCompleteObjectUndo(data, "MapNavMeshDefinitionEditor - Moved vertex");

            foreach (var selected in SelectedVertices) {
              var index = data.GetVertexIndex(selected);
              if (index >= 0) {
                data.Vertices[index].Position += m;
              }
            }
          }
        }
      }
    }
  }
}
#endif

#endregion

#region quantum_unity/Assets/Photon/Quantum/Editor/CustomEditors/MapNavMeshDefinitionEditor.Import.cs
#if !QUANTUM_DISABLE_AI
namespace Quantum.Editor {
  using System;
  using System.Collections.Generic;
  using System.Linq;
  using UnityEngine;

  public partial class MapNavMeshDefinitionEditor {

    [Obsolete("Use MapNavMeshEditor.BakeUnityNavmesh()")]
    public static bool BakeUnityNavmesh(MapNavMeshDefinition data) {
      return MapNavMeshEditor.BakeUnityNavmesh(data.gameObject);
    }

    [Obsolete("Use MapNavMesh.FindSmallestAgentRadius()")]
    public static float FindSmallestAgentRadius(MapNavMeshDefinition data) {
      return MapNavMesh.FindSmallestAgentRadius(data.NavMeshSurfaces).AsFloat;
    }

    public static void ImportFromUnity(MapNavMeshDefinition data) {
      var scene = data.gameObject.scene;
      Debug.Assert(scene.IsValid());

      data.InvalidateGizmoMesh();

      var importSettings = MapNavMeshDefinition.CreateImportSettings(data);
      // Get the agent radius that the navmesh was generated with. Use the smallest one from surfaces.
      data.AgentRadius = importSettings.MinAgentRadius;

      // If NavMeshSurface installed, this will deactivate non-linked surfaces 
      // to make the CalculateTriangulation work only with the selected Unity navmesh.
      List<GameObject> deactivatedObjects = new List<GameObject>();
      if (data.NavMeshSurfaces != null && data.NavMeshSurfaces.Length > 0) {
        if (NavMeshSurfaceType != null) {
          var surfaces = MapDataBaker.FindLocalObjects(scene, NavMeshSurfaceType);
          foreach (MonoBehaviour surface in surfaces) {
            if (data.NavMeshSurfaces.Contains(surface.gameObject) == false) {
              surface.gameObject.SetActive(false);
              deactivatedObjects.Add(surface.gameObject);
            }
          }
        }
      }

      try {
        var bakeData = MapNavMesh.ImportFromUnity(scene, importSettings, data.name);
        data.Links = bakeData.Links;
        data.Regions = bakeData.Regions;
        data.Triangles = bakeData.Triangles;
        data.Vertices = bakeData.Vertices;
      } catch (Exception e) {
        Log.Exception(e);
        throw e;
      } finally {
        foreach (var go in deactivatedObjects) {
          go.SetActive(true);
        }
      }
    }
  }
}
#endif

#endregion

#region quantum_unity/Assets/Photon/Quantum/Editor/CustomEditors/MapNavMeshEditor.cs
namespace Quantum.Editor {
  using System.Collections.Generic;
  using System.Linq;
  using UnityEditor;
  using UnityEngine;

  public static class MapNavMeshEditor {

    public static void UpdateDefaultMinAgentRadius() {
      // This can not be called by reflection, hence we need to set this by this ugly way.
      var settingsObject = new SerializedObject(UnityEditor.AI.NavMeshBuilder.navMeshSettingsObject);
      var radiusProperty = settingsObject.FindProperty("m_BuildSettings.agentRadius");
      MapNavMesh.DefaultMinAgentRadius = radiusProperty.floatValue;
    }

    public static bool BakeUnityNavmesh(GameObject go) {
      if (MapNavMesh.NavMeshSurfaceType == null) {
        // Is NavMesh Surfaces is not installed the global navmesh baking is triggered
        UnityEditor.AI.NavMeshBuilder.BuildNavMesh();
        return true;
      }
      else {
        // Collect surfaces
        List<GameObject> surfaces = new List<GameObject>();

        // Go through MapNavMeshDefinition scripts
        var navmeshDefinitions = new List<MapNavMeshDefinition>();
        go.GetComponents(navmeshDefinitions);
        go.GetComponentsInChildren(navmeshDefinitions);
        foreach (var unityNavmesh in navmeshDefinitions) {
          surfaces.AddRange(unityNavmesh.NavMeshSurfaces);
        }

        // Go through MapNavMeshUnity scripts
        var unityNavmeshes = new List<MapNavMeshUnity>();
        go.GetComponents(unityNavmeshes);
        go.GetComponentsInChildren(unityNavmeshes);
        foreach (var unityNavmesh in unityNavmeshes) {
          surfaces.AddRange(unityNavmesh.NavMeshSurfaces);
        }

        // Execute bake on each surface
        foreach (var gameObject in surfaces) {
          var navMeshSurface = gameObject.GetComponent(MapNavMesh.NavMeshSurfaceType);
          var buildNavMeshMethod = MapNavMesh.NavMeshSurfaceType.GetMethod("BuildNavMesh");
          buildNavMeshMethod.Invoke(navMeshSurface, null);
        }
      }

      return false;
    }

    public static bool ClearUnityNavmesh(GameObject go) {
      if (MapNavMesh.NavMeshSurfaceType == null) {
        // Is NavMesh Surfaces is not installed the global navmesh baking is triggered
        UnityEditor.AI.NavMeshBuilder.BuildNavMesh();
        return true;
      } else {
        // Collect surfaces
        List<GameObject> surfaces = new List<GameObject>();

        // Go through MapNavMeshDefinition scripts
        var navmeshDefinitions = new List<MapNavMeshDefinition>();
        go.GetComponents(navmeshDefinitions);
        go.GetComponentsInChildren(navmeshDefinitions);
        foreach (var unityNavmesh in navmeshDefinitions) {
          surfaces.AddRange(unityNavmesh.NavMeshSurfaces);
        }

        // Go through MapNavMeshUnity scripts
        var unityNavmeshes = new List<MapNavMeshUnity>();
        go.GetComponents(unityNavmeshes);
        go.GetComponentsInChildren(unityNavmeshes);
        foreach (var unityNavmesh in unityNavmeshes) {
          surfaces.AddRange(unityNavmesh.NavMeshSurfaces);
        }

        foreach (var gameObject in surfaces) {
          //NavMeshAssetManagerType.instance.ClearSurfaces()
          var navMeshSurface = gameObject.GetComponent(MapNavMesh.NavMeshSurfaceType);
          MapNavMesh.NavMeshSurfaceType.GetMethod("RemoveData").Invoke(navMeshSurface, null);
          var so = new SerializedObject(navMeshSurface);
          var navMeshDataProperty = so.FindProperty("m_NavMeshData");
          navMeshDataProperty.objectReferenceValue = null;
          so.ApplyModifiedPropertiesWithoutUndo();
        }
      }

      return false;
    }

    #region Unity Editors

    [CustomEditor(typeof(MapNavMeshUnity))]
    public partial class MapNavMeshUnityEditor : UnityEditor.Editor {
      public override void OnInspectorGUI() {

        var data = ((MapNavMeshUnity)target).Settings;

#if QUANTUM_XY
        var filteredSettings = new List<string>() { "Settings.RegionAreaIds" };
#else 
        var filteredSettings = new List<string>() { "Settings.RegionAreaIds", "Settings.EnableQuantum_XY" };
#endif

        if (data.WeldIdenticalVertices == false) filteredSettings.Add("Settings.WeldVertexEpsilon");
        if (data.FixTrianglesOnEdges == false) filteredSettings.Add("Settings.FixTrianglesOnEdgesEpsilon");
        if (data.ImportRegions == false) filteredSettings.Add("Settings.RegionDetectionMargin");
        if (data.ClosestTriangleCalculation == MapNavMesh.FindClosestTriangleCalculation.BruteForce) filteredSettings.Add("Settings.ClosestTriangleCalculationDepth");

        using (new QuantumEditorGUI.BoxScope("Import Settings")) {
          if (MapNavMesh.NavMeshSurfaceType != null) {
            QuantumEditorGUI.Inspector(serializedObject, "NavMeshSurfaces", skipRoot: false);
          }

          QuantumEditorGUI.Inspector(serializedObject, "Settings", filteredSettings.ToArray());

          if (data.WeldIdenticalVertices) {
            data.WeldVertexEpsilon = Mathf.Max(data.WeldVertexEpsilon, float.Epsilon);
          }

          if (data.FixTrianglesOnEdges) {
            data.FixTrianglesOnEdgesEpsilon = Mathf.Max(data.FixTrianglesOnEdgesEpsilon, float.Epsilon);
          }

          if (data.ImportRegions) {

            EditorGUILayout.LabelField(new GUIContent("Convert Unity Areas To Quantum Region:", "Select what Unity NavMesh areas are used to generated Quantum toggleable regions. At least one must be selected if import regions is enabled. Walkable region is not possible to be selected as it is converted to the default (non-toggleable) part of the navmesh."));
            EditorGUI.indentLevel++;
            var names = new List<string>(GameObjectUtility.GetNavMeshAreaNames());
            if (data.RegionAreaIds == null) {
              data.RegionAreaIds = new List<int>();
            }

            for (int i = 0; i < data.RegionAreaIds.Count; i++) {
              var areaId = data.RegionAreaIds[i];
              var areaName = GameObjectUtility.GetNavMeshAreaNames().FirstOrDefault(name => GameObjectUtility.GetNavMeshAreaFromName(name) == areaId);
              if (string.IsNullOrEmpty(areaName)) {
                areaName = "missing Unity NavMesh area";
              }

              if (!EditorGUILayout.Toggle(areaName, true)) {
                data.RegionAreaIds.Remove(areaId);
              }
              else {
                names.Remove(areaName);
              }
            }

            if (names.Count > 0) {
              var newName = EditorGUILayout.Popup("Add New Area", -1, names.ToArray());
              if (newName >= 0) {
                var areaId = GameObjectUtility.GetNavMeshAreaFromName(names[newName]);
                data.RegionAreaIds.Add(areaId);
              }
            }

            EditorGUI.indentLevel--;
          }
        }

        // This is confusing, it only triggers the unity baking not the Quantum baking
        //using (new QuantumEditorGUI.BackgroundColorScope(Color.green)) {
        //  if (GUILayout.Button("Run Unity Navmesh Baker", GUILayout.Height(EditorGUIUtility.singleLineHeight * 2))) {
        //    BakeUnityNavmesh(((MapNavMeshUnity)target).gameObject);
        //  }
        //}
      }
    }

    #endregion
  }
}
#endregion

#region quantum_unity/Assets/Photon/Quantum/Editor/CustomEditors/MapNavMeshRegionEditor.cs
namespace Quantum.Editor {
  using UnityEditor;
  using UnityEngine;

  [CustomEditor(typeof(MapNavMeshRegion))]
  public class MapNavMeshRegionEditor : UnityEditor.Editor {
    static bool _navMeshModifierSearched;
    static System.Type navMeshModifierType;

    System.Type NavMeshModifierType {
      get {
        // TypeUtils.FindType can be quite slow
        if (_navMeshModifierSearched == false) {
          _navMeshModifierSearched = true;
          navMeshModifierType = TypeUtils.FindType("NavMeshModifier");
        }
        return navMeshModifierType;
      }
    }

    public override void OnInspectorGUI() {
      var data = (MapNavMeshRegion)target;

      using (var change = new EditorGUI.ChangeCheckScope()) {
        data.Id = EditorGUILayout.TextField("Id", data.Id);
        data.CastRegion = (MapNavMeshRegion.RegionCastType)EditorGUILayout.EnumPopup("Cast Region", data.CastRegion);

        if (data.CastRegion == MapNavMeshRegion.RegionCastType.CastRegion) {
          using (new GUILayout.HorizontalScope()) {
            using (new EditorGUI.DisabledScope(data.OverwriteCost == false)) {
              EditorGUI.BeginChangeCheck();
              EditorGUILayout.PropertyField(serializedObject.FindPropertyOrThrow("Cost"), new GUIContent("Cost"));
              if (EditorGUI.EndChangeCheck()) {
                serializedObject.ApplyModifiedProperties();
              }
            }
            data.OverwriteCost = EditorGUILayout.Toggle("Overwrite", data.OverwriteCost);
          }
        }

        if (change.changed) {
          EditorUtility.SetDirty(target);
        }
      }

      if (string.IsNullOrEmpty(data.Id)) {
        EditorGUILayout.HelpBox("Id is not set", MessageType.Error);
      }
      else if (data.Id == "Default") {
        EditorGUILayout.HelpBox("'Default' is not allowed", MessageType.Error);
      }

      if (data.CastRegion == MapNavMeshRegion.RegionCastType.CastRegion) {

        QuantumEditorGUI.Header("NavMesh Editor Helper");

        if (data.gameObject.GetComponent<MeshRenderer>() == null) {
          EditorGUILayout.HelpBox("MapNavMeshRegion requires a MeshRenderer to be able to cast a region onto the navmesh", MessageType.Error);
        }

        using (var change = new EditorGUI.ChangeCheckScope()) {
          var currentFlags = GameObjectUtility.GetStaticEditorFlags(data.gameObject);
          var currentNavigationStatic = (currentFlags & StaticEditorFlags.NavigationStatic) == StaticEditorFlags.NavigationStatic;
          var newNavigationStatic = EditorGUILayout.Toggle("Toggle Static Flag", currentNavigationStatic);
          if (currentNavigationStatic != newNavigationStatic) {
            if (newNavigationStatic)
              GameObjectUtility.SetStaticEditorFlags(data.gameObject, currentFlags | StaticEditorFlags.NavigationStatic);
            else
              GameObjectUtility.SetStaticEditorFlags(data.gameObject, currentFlags & ~StaticEditorFlags.NavigationStatic);
          }

          int unityAreaId = 0;
          if (NavMeshModifierType != null) {
            var modifier = data.GetComponent(NavMeshModifierType) ?? data.GetComponentInParent(NavMeshModifierType);
            if (modifier != null) {
              EditorGUILayout.ObjectField("NavMesh Modifier GameObject", modifier.gameObject, typeof(GameObject), true);
              using (new EditorGUI.DisabledGroupScope(true)) {
                EditorGUILayout.Toggle("Override Area", (bool)NavMeshModifierType.GetField("m_OverrideArea", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(modifier));
                unityAreaId = (int)NavMeshModifierType.GetField("m_Area", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(modifier);
                var map = GameObjectUtility.GetNavMeshAreaNames();
                var currentIndex = GetNavMeshAreaIndex(unityAreaId);
                var newIndex = EditorGUILayout.Popup("Area", currentIndex, map);
                if (currentIndex != newIndex) {
                  unityAreaId = GameObjectUtility.GetNavMeshAreaFromName(map[newIndex]);
                  GameObjectUtility.SetNavMeshArea(data.gameObject, unityAreaId);
                }
              }
            }
            else {
              EditorGUILayout.LabelField("The NavMesh Area is defined by the NavMeshModifier script. No such script found in parents.");
            }
          }
          else {
            unityAreaId = GameObjectUtility.GetNavMeshArea(data.gameObject);

            var map = GameObjectUtility.GetNavMeshAreaNames();
            var currentIndex = GetNavMeshAreaIndex(unityAreaId);
            var newIndex = EditorGUILayout.Popup("Unity NavMesh Area", currentIndex, map);
            if (currentIndex != newIndex) {
              unityAreaId = GameObjectUtility.GetNavMeshAreaFromName(map[newIndex]);
              GameObjectUtility.SetNavMeshArea(data.gameObject, unityAreaId);
            }

            if (newIndex < 3 || newIndex >= map.Length) {
              EditorGUILayout.HelpBox("Unity NavMesh Area not valid", MessageType.Error);
            }

            if (newNavigationStatic == false) {
              EditorGUILayout.HelpBox("Unity Navigation Static has to be enabled", MessageType.Error);
            }
          }

          if (data.CastRegion == MapNavMeshRegion.RegionCastType.CastRegion && data.OverwriteCost == false) {
            data.Cost = UnityEngine.AI.NavMesh.GetAreaCost(unityAreaId).ToFP();
          }

          if (change.changed) {
            EditorUtility.SetDirty(target);
          }
        }
      }
    }

    private static int GetNavMeshAreaIndex(int areaId) {
      var map = GameObjectUtility.GetNavMeshAreaNames();
      var index = 0;
      for (index = 0; index < map.Length;) {
        if (GameObjectUtility.GetNavMeshAreaFromName(map[index]) == areaId)
          break;
        index++;
      }

      return index;
    }
  }
}

#endregion

#region quantum_unity/Assets/Photon/Quantum/Editor/CustomEditors/NavMeshAgentConfigAssetEditor.cs
namespace Quantum.Editor {

  using Photon.Deterministic;
  using UnityEditor;

  [CustomEditor(typeof(NavMeshAgentConfigAsset))]
  [CanEditMultipleObjects]
  public class NavMeshAgentConfigAssetEditor : AssetBaseEditor {

    public override void OnInspectorGUI() {
      base.OnInspectorGUI();

      foreach (NavMeshAgentConfigAsset data in targets) {
        if (data.Settings.StoppingDistance < Navigation.Constants.MinStoppingDistance) {
          data.Settings.StoppingDistance = Navigation.Constants.MinStoppingDistance;
        }

        if (data.Settings.CachedWaypointCount < 3) {
          data.Settings.CachedWaypointCount = 3;
        }

        if (data.Settings.CachedWaypointCount > 255) {
          data.Settings.CachedWaypointCount = 255;
        }
      }
    }
  }
}

#endregion

#region quantum_unity/Assets/Photon/Quantum/Editor/CustomEditors/NestedAssetBaseEditor.cs
namespace Quantum.Editor {
  using System;
  using System.Linq;
  using UnityEditor;
  using UnityEngine;
  using Quantum;
  using System.IO;
  using System.Collections.Generic;

  public abstract class NestedAssetBaseEditor : AssetBaseEditor {
    public override void OnInspectorGUI() {
      base.OnInspectorGUI();

      if ( serializedObject.isEditingMultipleObjects ) {
        return;
      }

      var assetObj = (AssetBase)target;
      var asset = (IQuantumPrefabNestedAsset)target;

      if (asset.Parent == null) {
        EditorGUILayout.HelpBox($"Needs to an asset nested in a prefab.", MessageType.Error);
      }

      using (new EditorGUI.DisabledScope(asset.Parent == null)) {
        if (GUILayout.Button("Select Prefab")) {
          Selection.activeObject = asset.Parent;
        }
      }

      bool canDelete = asset.Parent == null;
      if (asset.Parent != null) {
        string assetPath = AssetDatabase.GetAssetPath(assetObj);
        if (QuantumEditorSettings.Instance.AssetSearchPaths.Any(x => assetPath.StartsWith(x))) {
          // a part of quantum db
        } else {
          canDelete = true;
        }
      }

      using (new EditorGUI.DisabledScope(!canDelete)) {
        if (GUILayout.Button("Delete Nested Object")) {
          Selection.activeObject = null;
          UnityEngine.Object.DestroyImmediate(assetObj, true);
          AssetDatabase.SaveAssets();
        }
      }

      EditorGUILayout.Space();

      EditorGUILayout.LabelField("Experimental", EditorStyles.boldLabel);
      EditorGUILayout.HelpBox(
        "Assets nested in prefabs can be baked to a standalone assets that do not reference the prefab directly. " +
        "This can be useful, as in that case Quantum simulation does not have to wait for heavy resources like textures " +
        "and meshes to load.\n" +
        "The standalone asset is kept in sync with the prefab automatically, except for Address/Asset Bundles settings. If these " +
        "change, reimport the standalone asset and update its Address/Asset Bundle settings.", MessageType.Info);

      if (HasPrefabAsset(asset)) {
        if (GUILayout.Button("Delete Standalone Asset")) {
          RemovePrefabAsset(asset);
        }
      } else {
        if (GUILayout.Button("Create Standalone Asset")) {
          CreatePrefabAsset(asset);
        }
      }
    }

    public static string GetName(AssetBase asset, UnityEngine.Object parent) {
      return parent.name + asset.AssetObject.GetType().Name;
    }

    public static IQuantumPrefabNestedAsset GetNested(Component parent, System.Type assetType) {
      ThrowIfNotNestedAsset(assetType, parent);

      var parentPath = AssetDatabase.GetAssetPath(parent.gameObject);

      if (string.IsNullOrEmpty(parentPath) || !AssetDatabase.IsMainAsset(parent.gameObject)) {
        throw new System.InvalidOperationException($"{parent} is not a main asset");
      }

      var subAssets = AssetDatabase.LoadAllAssetsAtPath(parentPath).Where(x => x?.GetType() == assetType).ToList();
      Debug.Assert(subAssets.Count <= 1, $"More than 1 asset of type {assetType} on {parent}, clean it up manually");

      return subAssets.Count == 0 ? null : (IQuantumPrefabNestedAsset)subAssets[0];
    }

    private static void ThrowIfNotNestedAsset(Type type, Component parent) {
      if (type == null)
        throw new ArgumentNullException(nameof(type));
      if (parent == null)
        throw new ArgumentNullException(nameof(parent));

      if (!type.IsSubclassOf(typeof(AssetBase))) {
        throw new InvalidOperationException($"Type {type} is not a subclass of {nameof(AssetBase)}");
      }

      if (type.GetInterface(typeof(IQuantumPrefabNestedAsset).FullName) == null) {
        throw new InvalidOperationException($"Type {type} does not implement {nameof(IQuantumPrefabNestedAsset)}");
      }

      var genericInterface = type.GetInterfaces()
        .Where(x => x.IsConstructedGenericType)
        .Where(x => x.GetGenericTypeDefinition() == typeof(IQuantumPrefabNestedAsset<>))
        .SingleOrDefault();

      if (genericInterface == null) {
        throw new InvalidOperationException($"Type {type} does not implement {nameof(IQuantumPrefabNestedAsset)}<>");
      }

      var expectedParentComponent = genericInterface.GetGenericArguments()[0];
      if (expectedParentComponent != parent.GetType() && !parent.GetType().IsSubclassOf(expectedParentComponent)) {
        throw new InvalidOperationException($"Parent's type ({parent.GetType()}) is not equal nor is a subclass of {expectedParentComponent}");
      }
    }



    public static bool EnsureExists(Component parent, System.Type assetType, out IQuantumPrefabNestedAsset result, bool save = true) {
      ThrowIfNotNestedAsset(assetType, parent);

      var parentPath = AssetDatabase.GetAssetPath(parent.gameObject);

      if (string.IsNullOrEmpty(parentPath) || !AssetDatabase.IsMainAsset(parent.gameObject)) {
        throw new System.InvalidOperationException($"{parent} is not a main asset");
      }

      var subAsset = GetNested(parent, assetType);
      bool isDirty = false;

      AssetBase assetObj;

      if (subAsset != null) {
        assetObj = (AssetBase)subAsset;
        result = subAsset;
        Debug.Assert(result != null);
      } else {
        assetObj = (AssetBase)ScriptableObject.CreateInstance(assetType);
        AssetDatabase.AddObjectToAsset(assetObj, parentPath);
        result = (IQuantumPrefabNestedAsset)assetObj;
        isDirty = true;
      }

      if (assetObj == null) {
        throw new InvalidOperationException($"Failed to create an instance of {assetType}");
      }

      string targetName = GetName(assetObj, parent);
      if (assetObj.name != targetName) {
        assetObj.name = targetName;
        isDirty = true;
      }

      if (result.Parent != parent) {
        var so = new SerializedObject(assetObj);
        var parentProperty = so.FindProperty("Parent");
        if (parentProperty == null) {
          throw new InvalidOperationException("Nested assets are expected to have \"Parent\" field");
        }

        parentProperty.objectReferenceValue = parent;
        so.ApplyModifiedPropertiesWithoutUndo();
        if (parentProperty.objectReferenceValue != parent)
          throw new InvalidOperationException($"Unable to set property Parent. Is the type convertible from {parent.GetType()}?");
        isDirty = true;
      }

      if (isDirty) {
        EditorUtility.SetDirty(assetObj);
        EditorUtility.SetDirty(parent.gameObject);
      }

      if (isDirty && save) {
        AssetDatabase.SaveAssets();
      }

      return isDirty;
    }

    public static void ClearParent(AssetBase asset) {
      using (var so = new SerializedObject(asset)) {
        so.FindPropertyOrThrow("Parent").objectReferenceValue = null;
        so.ApplyModifiedPropertiesWithoutUndo();
      }
    }

    public static bool EnsureExists<T, SubType>(T parent, out SubType result, bool save = true)
      where T : Component
      where SubType : IQuantumPrefabNestedAsset<T> {
      var flag = EnsureExists(parent, typeof(SubType), out IQuantumPrefabNestedAsset temp, save);
      result = (SubType)temp;
      return flag;
    }

    public static bool GetNested<T, SubType>(T parent, out SubType result)
      where T : Component
      where SubType : IQuantumPrefabNestedAsset<T> {
      result = (SubType)GetNested(parent, typeof(SubType));
      return result != null;
    }

    public static Type GetHostType(Type nestedAssetType) {

      var interfaceTypes = nestedAssetType.GetInterfaces();
      foreach (var t in interfaceTypes) {
        if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(IQuantumPrefabNestedAsset<>)) {
          return t.GetGenericArguments()[0];
        }
      }

      throw new InvalidOperationException();
    }

    public static NestedAssetType CreateWithParentPrefab<NestedAssetType>(string path)
      where NestedAssetType : AssetBase, IQuantumPrefabNestedAsset {
      var name = System.IO.Path.GetFileNameWithoutExtension(path);
      var componentType = GetHostType(typeof(NestedAssetType));

      var go = new GameObject(name, componentType);
      try {
        var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
        if (!prefab)
          return null;
        return AssetDatabase.LoadAssetAtPath<NestedAssetType>(AssetDatabase.GetAssetPath(prefab));
      } finally {
        UnityEngine.Object.DestroyImmediate(go);
        AssetDatabase.SaveAssets();
      }
    }

    public static void CreateNewAssetMenuItem<NestedAssetType>() where NestedAssetType : AssetBase, IQuantumPrefabNestedAsset {
      var activeDirectory = AssetDatabase.GetAssetPath(Selection.activeObject);

      if (string.IsNullOrEmpty(activeDirectory)) {
        return;
      }

      if (!System.IO.Directory.Exists(activeDirectory)) {
        activeDirectory = System.IO.Path.GetDirectoryName(activeDirectory);
      }

      var targetPath = AssetDatabase.GenerateUniqueAssetPath($"{activeDirectory}/{typeof(NestedAssetType).Name}.prefab");
      CreateWithParentPrefab<NestedAssetType>(targetPath);
    }

    public static void AssetLinkOnGUI<NestedAssetType>(Rect position, SerializedProperty property, GUIContent label) where NestedAssetType : AssetBase, IQuantumPrefabNestedAsset {
      AssetRefDrawer.DrawAssetRefSelector(position, property, label, typeof(NestedAssetType), createAssetCallback: () => {
        var assetPath = string.Format($"{QuantumEditorSettings.Instance.DefaultAssetSearchPath}/{nameof(NestedAssetType)}.prefab");
        return CreateWithParentPrefab<NestedAssetType>(assetPath);
      });
    }

    private static void CreatePrefabAsset(IQuantumPrefabNestedAsset asset) {
      var unityAsset = (AssetBase)asset;

      var guid = AssetDatabaseExt.GetAssetGuidOrThrow(unityAsset);
      var path = AssetDatabaseExt.GetAssetPathOrThrow(guid);

      var qprefabPath = QuantumPrefabAssetImporter.GetPath(path);
      Debug.Assert(!File.Exists(qprefabPath));

      File.WriteAllText(qprefabPath, guid);
      InvalidePrefabAssets();
      AssetDatabase.ImportAsset(qprefabPath);
    }

    private static bool RemovePrefabAsset(IQuantumPrefabNestedAsset asset) {
      var unityAsset = (AssetBase)asset;

      var guid = AssetDatabaseExt.GetAssetGuidOrThrow(unityAsset);

      if (PrefabAssets.TryGetValue(guid, out var surrogateGuid)) {
        var path = AssetDatabaseExt.GetAssetPathOrThrow(surrogateGuid);
        InvalidePrefabAssets();
        return AssetDatabase.DeleteAsset(path);
      } else {
        return false;
      }
    }

    internal static bool HasPrefabAsset(IQuantumPrefabNestedAsset asset) {
      var guid = AssetDatabaseExt.GetAssetGuidOrThrow((AssetBase)asset);
      return PrefabAssets.ContainsKey(guid);
    }

    private static void InvalidePrefabAssets() {
      _prefabGuidToPrefabAssetGuid = null;
    }

    private static Dictionary<string, string> PrefabAssets {
      get {
        if (_prefabGuidToPrefabAssetGuid == null) {
          _prefabGuidToPrefabAssetGuid = new Dictionary<string, string>();
          foreach (var guid in AssetDatabase.FindAssets($"t:{nameof(QuantumPrefabAsset)}")) {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            if (File.Exists(path)) {
              var prefabGuid = File.ReadAllText(path);
              _prefabGuidToPrefabAssetGuid.Add(prefabGuid, guid);
            }
          }
          EditorApplication.projectChanged += () => {
            InvalidePrefabAssets();
          };
        }
        return _prefabGuidToPrefabAssetGuid;
      }
    }


    private static Dictionary<string, string> _prefabGuidToPrefabAssetGuid;
  }
}
#endregion

#region quantum_unity/Assets/Photon/Quantum/Editor/CustomEditors/PhotonServerSettingsEditor.cs
namespace Quantum.Editor {
  using System;
  using System.Linq;
  using Photon.Realtime;
  using UnityEditor;
  using UnityEngine;

  [CustomEditor(typeof(PhotonServerSettings), false)]
  public class PhotonServerSettingsEditor : UnityEditor.Editor {
    private static bool HasVoice;
    private static bool HasChat;
    private static bool HasCheckedForPlugins;

    public override void OnInspectorGUI() {
      if (HasCheckedForPlugins) {
        HasCheckedForPlugins = false;
        HasVoice = Type.GetType("Photon.Voice.VoiceClient, PhotonVoice.API") != null;
        HasChat = Type.GetType("Photon.Voice.ChatClient, PhotonChat.API") != null;
        foreach (var assemblyName in QuantumEditorSettings.Instance.SearchAssemblies) {
          HasVoice |= Type.GetType($"Photon.Voice.VoiceClient, {assemblyName}") != null;
          HasChat |= Type.GetType($"Photon.Voice.ChatClient, {assemblyName}") != null;
        }
      }

      var settings = (PhotonServerSettings)target;

      using (new QuantumEditorGUI.CustomEditorScope(serializedObject))
      using (new QuantumEditorGUI.BoxScope("Photon Server Settings")) {

        using (new QuantumEditorGUI.SectionScope("App Settings")) {
          SerializedProperty serializedProperty = this.serializedObject.FindProperty("AppSettings");
          BuildAppIdField(serializedProperty.FindPropertyRelative("AppIdRealtime"), new GUIContent("AppId", "The Photon Quantum AppId (internally stored as AppIdRealtime)"));
          if (HasChat) BuildAppIdField(serializedProperty.FindPropertyRelative("AppIdChat"));
          if (HasVoice) BuildAppIdField(serializedProperty.FindPropertyRelative("AppIdVoice"));

          QuantumEditorGUI.Inspector(serializedObject, "AppSettings", new string[] { "AppSettings.AppIdChat", "AppSettings.AppIdFusion", "AppSettings.AppIdVoice", "AppSettings.AppIdRealtime" }, skipRoot: false);
        }

        using (new QuantumEditorGUI.SectionScope("Custom Settings")) {
          using (var checkScope = new EditorGUI.ChangeCheckScope()) {
            settings.PlayerTtlInSeconds = EditorGUILayout.IntField("PlayerTTL In Seconds", settings.PlayerTtlInSeconds);
            settings.EmptyRoomTtlInSeconds = EditorGUILayout.IntField("EmptyRoomTTL In Seconds", settings.EmptyRoomTtlInSeconds);
            if (checkScope.changed) {
              EditorUtility.SetDirty(settings);
            }
          }
        }

        using (new QuantumEditorGUI.SectionScope("Development Utils")) {

          DisplayBestRegionPreference(settings.AppSettings);

          using (new EditorGUILayout.HorizontalScope()) {
            EditorGUILayout.PrefixLabel("Configure App Settings");
            if (GUILayout.Button("Cloud", EditorStyles.miniButton)) {
              SetSettingsToCloud(settings.AppSettings);
              EditorUtility.SetDirty(settings);
            }
            if (GUILayout.Button("Local Master Server", EditorStyles.miniButton)) {
              SetSettingsToLocalServer(settings.AppSettings);
              EditorUtility.SetDirty(settings);
            }
          }
        }
      }
    }

    private void DisplayBestRegionPreference(AppSettings appSettings) {
      EditorGUILayout.BeginHorizontal();
      EditorGUILayout.PrefixLabel(new GUIContent("Best Region Preference", "Clears the Best Region of the editor.\n.Best region is used if Fixed Region is empty."));

      var bestRegionSummaryInPreferences = PlayerPrefs.GetString(QuantumLoadBalancingClient.BestRegionSummaryKey);

      var prefLabel = "n/a";
      if (!string.IsNullOrEmpty(bestRegionSummaryInPreferences)) {
        var regionsPrefsList = bestRegionSummaryInPreferences.Split(';');
        if (regionsPrefsList.Length > 1 && !string.IsNullOrEmpty(regionsPrefsList[0])) {
          prefLabel = $"'{regionsPrefsList[0]}' ping:{regionsPrefsList[1]}ms ";
        }
      }

      GUILayout.Label(prefLabel, GUILayout.ExpandWidth(false));

      if (GUILayout.Button("Reset", EditorStyles.miniButton)) {
        PlayerPrefs.SetString(QuantumLoadBalancingClient.BestRegionSummaryKey, String.Empty);
      }

      if (GUILayout.Button("Edit WhiteList", EditorStyles.miniButton)) {
        Application.OpenURL("https://dashboard.photonengine.com/en-US/App/RegionsWhitelistEdit/" + appSettings.AppIdRealtime);
      }

      EditorGUILayout.EndHorizontal();
    }

    private void BuildAppIdField(SerializedProperty property, GUIContent overwritePropertyLabel = null) {
      EditorGUILayout.BeginHorizontal();
      using (var checkScope = new EditorGUI.ChangeCheckScope()) {
        if (overwritePropertyLabel == null) {
          EditorGUILayout.PropertyField(property);
        }
        else {
          EditorGUILayout.PropertyField(property, overwritePropertyLabel);
        }
        
        if (checkScope.changed) {
          property.serializedObject.ApplyModifiedProperties();
        }
      }
      var appId = property.stringValue;
      var url = "https://dashboard.photonengine.com/en-US/PublicCloud";
      if (!string.IsNullOrEmpty(appId)) {
        url = $"https://dashboard.photonengine.com/en-US/App/Manage/{appId}";
      }
      if (GUILayout.Button("Dashboard", EditorStyles.miniButton, GUILayout.Width(70))) {
        Application.OpenURL(url);
      }
      EditorGUILayout.EndHorizontal();
    }

    public static void SetSettingsToCloud(AppSettings appSettings) {
      appSettings.Server = string.Empty;
      appSettings.UseNameServer = true;
      appSettings.Port = 0;
    }

    public static void SetSettingsToLocalServer(AppSettings appSettings) {
      try {
        var ip = System.Net.Dns.GetHostEntry(System.Net.Dns.GetHostName())
                       .AddressList
                       .First(x => x.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                       .ToString();

        appSettings.Server = ip;
        appSettings.UseNameServer = false;
        if (appSettings.Port == 0) {
          appSettings.Port = 5055;
        }
      } catch (Exception e) {
        Debug.LogWarning("Cannot set local server address, sorry.");
        Debug.LogException(e);
      }
    }
  }
}


#endregion

#region quantum_unity/Assets/Photon/Quantum/Editor/CustomEditors/QuantumEditorSettingsEditor.cs
namespace Quantum.Editor {
  using UnityEditor;
  using UnityEngine;
  using System.Linq;
  using UnityEditorInternal;
  using System;

  [CustomEditor(typeof(QuantumEditorSettings), true)]
  public class QuantumEditorSettingsEditor : UnityEditor.Editor {

    public const string PhotonQuantumAsmDefGuid       = "f6fa0c2f8b9a9f64897d3351666f3d66";
    public const string PhotonQuantumEditorAsmDefGuid = "3dc2666f1394e2c48a30ea492efb1717";
    public const string AddressablesAsmdefGuid        = "9e24947de15b9834991c9d8411ea37cf";
    public const string ResourceManagerAsmdefGuid     = "84651a3751eca9349aac36a66bba901b";
    public const string AddressablesEditorAsmdefGuid  = "69448af7b92c7f342b298e06a37122aa";

    public override void OnInspectorGUI() {

      using (new QuantumEditorGUI.CustomEditorScope(serializedObject)) {
        using (new QuantumEditorGUI.BoxScope("Quantum Editor Settings")) {
          QuantumEditorGUI.Inspector(serializedObject, drawScript: false);

          EditorGUILayout.Space();
          EditorGUILayout.LabelField("Build Features (Current Platform Only)", EditorStyles.boldLabel);

          DrawScriptingDefineToggle(new GUIContent("Use XY as 2D Plane"), "QUANTUM_XY");

          EditorGUI.BeginChangeCheck();
          DrawScriptingDefineToggle(new GUIContent("Remote Profiler"), "QUANTUM_REMOTE_PROFILER");
          if (EditorGUI.EndChangeCheck()) {
            // need to reimport the libs, at least in 2018.4; otherwise there'd be compile errors
            var dlls = AssetDatabase.FindAssets("LiteNetLib")
              .Select(x => AssetDatabase.GUIDToAssetPath(x))
              .Where(x => string.Equals(System.IO.Path.GetFileName(x), "LiteNetLib.dll", System.StringComparison.OrdinalIgnoreCase))
              .ToList();

            foreach (var dll in dlls) {
              AssetDatabase.ImportAsset(dll);
            }
          }

          {
            var content = new GUIContent("Addressables", "Enables Quantum AssetDB to use addressable assets. You need to have the Addressable package installed.");
            EditorGUI.BeginChangeCheck();
            bool value = DrawScriptingDefineToggle(content, "QUANTUM_ADDRESSABLES", allPlatforms: true);
            if (EditorGUI.EndChangeCheck()) {
              SetQuantumAssemblyDefinitionsAddressablesReferences(value);
            }
          }

          var hasAddressables = AssetDatabaseExt.HasScriptingDefineSymbol(BuildPipeline.GetBuildTargetGroup(EditorUserBuildSettings.activeBuildTarget), "QUANTUM_ADDRESSABLES");
          using (new EditorGUI.DisabledScope(hasAddressables == false)) {
            DrawScriptingDefineToggle(
              new GUIContent(
                "Addressables: Use WaitForCompletion",
                "Enables Quantum to use AsyncOperationHandle.WaitForCompletion when loading assets, getting rid of the need to preload addressable assets. Requires Addressables 1.17+."),
              "QUANTUM_ADDRESSABLES_USE_WAIT_FOR_COMPLETION");
          }


          DrawScriptingDefineToggle(new GUIContent("Disable 2D Physics Module"), "QUANTUM_DISABLE_PHYSICS2D");
          DrawScriptingDefineToggle(new GUIContent("Disable 3D Physics Module"), "QUANTUM_DISABLE_PHYSICS3D");
          DrawScriptingDefineToggle(new GUIContent("Disable AI Module"), "QUANTUM_DISABLE_AI");
          DrawScriptingDefineToggle(new GUIContent("Disable Terrain Module"), "QUANTUM_DISABLE_TERRAIN");
        }

        var settings = target as QuantumEditorSettings;

        // Replace slashes, trim start and end
        for (int i = 0; i < settings.AssetSearchPaths.Length; i++) {
          settings.AssetSearchPaths[i] = PathUtils.MakeSane(settings.AssetSearchPaths[i]);
        }

        settings.QuantumSolutionPath = Quantum.PathUtils.MakeSane(settings.QuantumSolutionPath);

        if (!System.IO.File.Exists(settings.QuantumSolutionPath))
          EditorGUILayout.HelpBox("Quantum solution file not found at '" + settings.QuantumSolutionPath + "'", MessageType.Error);

        if (!settings.QuantumSolutionPath.EndsWith(".sln"))
          EditorGUILayout.HelpBox("Quantum solution file path has to end with .sln", MessageType.Error);
      }
    }


    private static bool DrawScriptingDefineToggle(GUIContent label, string define, bool allPlatforms = false) {
      bool? hasDefine;
      if (allPlatforms) {
        hasDefine = AssetDatabaseExt.HasScriptingDefineSymbol(define);
      } else {
        hasDefine = AssetDatabaseExt.HasScriptingDefineSymbol(BuildPipeline.GetBuildTargetGroup(EditorUserBuildSettings.activeBuildTarget), define);
      }

      EditorGUI.BeginChangeCheck();
      EditorGUI.showMixedValue = hasDefine == null;
      bool value = EditorGUILayout.Toggle(label, hasDefine == true);
      EditorGUI.showMixedValue = false;
      if (EditorGUI.EndChangeCheck()) {
        if (allPlatforms) {
          AssetDatabaseExt.UpdateScriptingDefineSymbol(define, value);
        } else {
          AssetDatabaseExt.UpdateScriptingDefineSymbol(BuildPipeline.GetBuildTargetGroup(EditorUserBuildSettings.activeBuildTarget), define, value);
        }
      }
      return value;
    }

    public static void SetQuantumAssemblyDefinitionsAddressablesReferences(bool value) {
      UpdateAssemblyReferences(PhotonQuantumAsmDefGuid, value, 
        (AddressablesAsmdefGuid,        "Unity.Addressables"),
        (ResourceManagerAsmdefGuid,     "Unity.ResourceManager")
      );

      UpdateAssemblyReferences(PhotonQuantumEditorAsmDefGuid, value,
        (AddressablesAsmdefGuid,        "Unity.Addressables"),
        (AddressablesEditorAsmdefGuid,  "Unity.Addressables.Editor")
      );
    }

    private static void UpdateAssemblyReferences(string assemblyGuid, bool enabled, params ValueTuple<string, string>[] references) {
      var asmdef = AssetDatabase.LoadAssetAtPath<AssemblyDefinitionAsset>(AssetDatabase.GUIDToAssetPath(assemblyGuid));
      if ( asmdef == null) {
        throw new ArgumentException($"Unable to load asmdef with guid: {assemblyGuid}", nameof(assemblyGuid));
      }

      if (enabled) {
        asmdef.UpdateReferences(references, null);
      } else {
        asmdef.UpdateReferences(null, references);
      }
    }
  }
}
#endregion

#region quantum_unity/Assets/Photon/Quantum/Editor/CustomEditors/QuantumInstantReplayDemoEditor.cs
namespace Quantum.Editor {
  using UnityEditor;

  [CustomEditor(typeof(QuantumInstantReplayDemo))]
  public class QuantumInstantReplayDemoEditor : UnityEditor.Editor {

    private new QuantumInstantReplayDemo target => (QuantumInstantReplayDemo)base.target;

    public override void OnInspectorGUI() {
      base.DrawDefaultInspector();
      EditorGUILayout.HelpBox($"Use QuantumRunner.StartParameters.InstantReplaySettings to define the maximum replay length and the snapshot sampling rate.", MessageType.Info);
    }

    public override bool RequiresConstantRepaint() {
      return true;
    }
  }
}
#endregion

#region quantum_unity/Assets/Photon/Quantum/Editor/CustomEditors/QuantumRunnerLocalReplayEditor.cs
namespace Quantum.Editor {

  using System.IO;
  using UnityEditor;
  using UnityEngine;

  [CustomEditor(typeof(QuantumRunnerLocalReplay))]
  public class QuantumRunnerLocalReplayEditor : UnityEditor.Editor {

    public override void OnInspectorGUI() {
      var data = (QuantumRunnerLocalReplay)target;

      var oldReplayFile = data.ReplayFile;

      if (DrawDefaultInspector() && oldReplayFile != data.ReplayFile) {
        data.DatabaseFile = null;
        data.ChecksumFile = null;
        data.DatabasePath = string.Empty;

        if (data.ReplayFile != null && data.DatabaseFile == null) {
          var assetPath        = AssetDatabase.GetAssetPath(data.ReplayFile);
          var databaseFilepath = Path.Combine(Path.GetDirectoryName(assetPath), "db.json");
          data.DatabaseFile = AssetDatabase.LoadAssetAtPath<TextAsset>(databaseFilepath);
        }

        if (data.ReplayFile != null && data.ChecksumFile == null) {
          var assetPath        = AssetDatabase.GetAssetPath(data.ReplayFile);
          var checksumFilepath = Path.Combine(Path.GetDirectoryName(assetPath), "checksum.json");
          data.ChecksumFile = AssetDatabase.LoadAssetAtPath<TextAsset>(checksumFilepath);
        }

        if (data.DatabaseFile != null) {
          var assetPath = AssetDatabase.GetAssetPath(data.DatabaseFile);
          data.DatabasePath = Path.GetDirectoryName(assetPath);
        }
        else
          data.DatabasePath = string.Empty;
      }

      // Toggle the debug runner if on the same game object.
      var debugRunner   = data.gameObject.GetComponent<QuantumRunnerLocalDebug>();
      var debugSavegame = data.gameObject.GetComponent<QuantumRunnerLocalSavegame>();
      if (debugRunner != null) {
        GUI.backgroundColor = data.enabled ? Color.red : Color.white;
        if (GUILayout.Button(new GUIContent(data.enabled ? "Disable" : "Enable", "Enable this script and disable QuantumRunnerLocalDebug on the same game object."))) {
          debugRunner.enabled = data.enabled;
          data.enabled        = !data.enabled;
          if (debugSavegame != null)
            debugSavegame.enabled = false;
        }

        GUI.backgroundColor = Color.white;
      }
    }
  }
}
#endregion

#region quantum_unity/Assets/Photon/Quantum/Editor/CustomEditors/QuantumRunnerLocalSavegameEditor.cs
namespace Quantum.Editor {

  using System.IO;
  using UnityEditor;
  using UnityEngine;

  [CustomEditor(typeof(QuantumRunnerLocalSavegame))]
  public class QuantumRunnerLocalSavegameEditor : UnityEditor.Editor {

    public override void OnInspectorGUI() {
      var data = (QuantumRunnerLocalSavegame)target;

      var oldSavegameFile = data.SavegameFile;

      if (DrawDefaultInspector() && oldSavegameFile != data.SavegameFile) {
        data.DatabaseFile = null;
        data.DatabasePath = string.Empty;

        if (data.SavegameFile != null && data.DatabaseFile == null) {
          var assetPath        = AssetDatabase.GetAssetPath(data.SavegameFile);
          var databaseFilepath = Path.Combine(Path.GetDirectoryName(assetPath), "db.json");
          data.DatabaseFile = AssetDatabase.LoadAssetAtPath<TextAsset>(databaseFilepath);
        }

        if (data.DatabaseFile != null) {
          var assetPath = AssetDatabase.GetAssetPath(data.DatabaseFile);
          data.DatabasePath = Path.GetDirectoryName(assetPath);
        }
        else
          data.DatabasePath = string.Empty;
      }

      // Toggle the debug runner if on the same game object.
      var debugRunner = data.gameObject.GetComponent<QuantumRunnerLocalDebug>();
      var debugReplay = data.gameObject.GetComponent<QuantumRunnerLocalReplay>();
      if (debugRunner != null) {
        GUI.backgroundColor = data.enabled ? Color.red : Color.white;
        if (GUILayout.Button(new GUIContent(data.enabled ? "Disable" : "Enable", "Enable this script and disable QuantumRunnerLocalDebug on the same game object."))) {
          debugRunner.enabled = data.enabled;
          data.enabled        = !data.enabled;
          if (debugReplay != null)
            debugReplay.enabled = false;
        }

        GUI.backgroundColor = Color.white;
      }
    }
  }
}
#endregion

#region quantum_unity/Assets/Photon/Quantum/Editor/CustomEditors/QuantumStaticBoxCollider2DEditor.cs
#if !QUANTUM_DISABLE_PHYSICS2D
namespace Quantum.Editor {
  using UnityEditor;
  using UnityEngine;

  [CustomEditor(typeof(QuantumStaticBoxCollider2D))]
  public class QuantumStaticBoxCollider2DEditor : UnityEditor.Editor {
    private static readonly System.Type[] SupportedSourceColliderTypes = {typeof(BoxCollider2D), typeof(BoxCollider)};

    public override void OnInspectorGUI() {
      using (new QuantumEditorGUI.CustomEditorScope(serializedObject)) {
        QuantumEditorGUI.Inspector(serializedObject, drawScript: false, callback: (p, field, type) => {
          if (type == typeof(Component)) {
            QuantumEditorGUI.MultiTypeObjectField(p, new GUIContent(p.displayName), SupportedSourceColliderTypes);
            return true;
          }

          if (p.name == nameof(QuantumStaticBoxCollider2D.Settings.Trigger)) {
            var sourceCollider = serializedObject.FindProperty(nameof(QuantumStaticBoxCollider2D.SourceCollider));
            using (new EditorGUI.DisabledScope(sourceCollider?.objectReferenceValue != null)) {
              QuantumEditorGUI.PropertyField(p);
            }

            return true;
          }

          return false;
        });
      }

      if (Application.isPlaying == false) {
        ((QuantumStaticBoxCollider2D)target).UpdateFromSourceCollider();
      }
    }
  }
}
#endif
#endregion

#region quantum_unity/Assets/Photon/Quantum/Editor/CustomEditors/QuantumStaticBoxCollider3DEditor.cs
#if !QUANTUM_DISABLE_PHYSICS3D
namespace Quantum.Editor {
using UnityEditor;
  using UnityEngine;

  [CustomEditor(typeof(QuantumStaticBoxCollider3D))]
  public class QuantumStaticBoxCollider3DEditor : UnityEditor.Editor {
    public override void OnInspectorGUI() {
      using (new QuantumEditorGUI.CustomEditorScope(serializedObject)) {
        QuantumEditorGUI.Inspector(serializedObject, drawScript: false, callback: (p, field, type) => {
          if (p.name == nameof(QuantumStaticBoxCollider3D.Settings.Trigger)) {
            var sourceCollider = serializedObject.FindProperty(nameof(QuantumStaticBoxCollider3D.SourceCollider));
            using (new EditorGUI.DisabledScope(sourceCollider?.objectReferenceValue != null)) {
              QuantumEditorGUI.PropertyField(p);
            }

            return true;
          }

          return false;
        });
      }

      if (Application.isPlaying == false) {
        ((QuantumStaticBoxCollider3D)target).UpdateFromSourceCollider();
      }
    }
  }
}
#endif
#endregion

#region quantum_unity/Assets/Photon/Quantum/Editor/CustomEditors/QuantumStaticCircleCollider2DEditor.cs
#if !QUANTUM_DISABLE_PHYSICS2D
namespace Quantum.Editor {
using UnityEditor;
  using UnityEngine;

  [CustomEditor(typeof(QuantumStaticCircleCollider2D))]
  public class QuantumStaticCircleCollider2DEditor : UnityEditor.Editor {
    private static readonly System.Type[] SupportedSourceColliderTypes = {typeof(CircleCollider2D), typeof(SphereCollider)};

    public override void OnInspectorGUI() {
      using (new QuantumEditorGUI.CustomEditorScope(serializedObject)) {
        QuantumEditorGUI.Inspector(serializedObject, drawScript: false, callback: (p, field, type) => {
          if (type == typeof(Component)) {
            QuantumEditorGUI.MultiTypeObjectField(p, new GUIContent(p.displayName), SupportedSourceColliderTypes);
            return true;
          }

          if (p.name == nameof(QuantumStaticCircleCollider2D.Settings.Trigger)) {
            var sourceCollider = serializedObject.FindProperty(nameof(QuantumStaticCircleCollider2D.SourceCollider));
            using (new EditorGUI.DisabledScope(sourceCollider?.objectReferenceValue != null)) {
              QuantumEditorGUI.PropertyField(p);
            }

            return true;
          }

          return false;
        });
      }

      if (Application.isPlaying == false) {
        ((QuantumStaticCircleCollider2D)target).UpdateFromSourceCollider();
      }
    }
  }
}
#endif
#endregion

#region quantum_unity/Assets/Photon/Quantum/Editor/CustomEditors/QuantumStaticSphereCollider3DEditor.cs
#if !QUANTUM_DISABLE_PHYSICS3D
namespace Quantum.Editor {
  using UnityEditor;
  using UnityEngine;

  [CustomEditor(typeof(QuantumStaticSphereCollider3D))]
  public class QuantumStaticSphereCollider3DEditor : UnityEditor.Editor {
    public override void OnInspectorGUI() {
      using (new QuantumEditorGUI.CustomEditorScope(serializedObject)) {
        QuantumEditorGUI.Inspector(serializedObject, drawScript: false, callback: (p, field, type) => {
          if (p.name == nameof(QuantumStaticSphereCollider3D.Settings.Trigger)) {
            var sourceCollider = serializedObject.FindProperty(nameof(QuantumStaticSphereCollider3D.SourceCollider));
            using (new EditorGUI.DisabledScope(sourceCollider?.objectReferenceValue != null)) {
              QuantumEditorGUI.PropertyField(p);
            }

            return true;
          }

          return false;
        });
      }

      if (Application.isPlaying == false) {
        ((QuantumStaticSphereCollider3D)target).UpdateFromSourceCollider();
      }
    }
  }
}
#endif
#endregion

#region quantum_unity/Assets/Photon/Quantum/Editor/CustomEditors/SimulationConfigAssetEditor.cs
namespace Quantum.Editor {

  using System;
  using System.Linq;
  using System.Reflection;
  using Photon.Deterministic;
  using UnityEditor;
  using UnityEngine;

  [CustomEditor(typeof(SimulationConfigAsset))]
  public class SimulationConfigAssetEditor : UnityEditor.Editor {

    class PropertyPaths : SerializedPropertyPathBuilder<SimulationConfigAsset> {
      public static readonly string Settings           = GetPropertyPath(asset => asset.Settings);
      public static readonly string Physics            = GetPropertyPath(asset => asset.Settings.Physics);
      public static readonly string Entities           = GetPropertyPath(asset => asset.Settings.Entities);
      public static readonly string Navigation         = GetPropertyPath(asset => asset.Settings.Navigation);
      public static readonly string PhysicsLayers      = GetPropertyPath(asset => asset.Settings.Physics.Layers);
      public static readonly string PhysicsLayerMatrix = GetPropertyPath(asset => asset.Settings.Physics.LayerMatrix);
    }

    public override void OnInspectorGUI() {

      var data = (SimulationConfigAsset)target;
      using (new QuantumEditorGUI.CustomEditorScope(serializedObject)) {
        using (new QuantumEditorGUI.BoxScope("SimulationConfig")) {
          QuantumEditorGUI.Inspector(serializedObject, PropertyPaths.Settings, filters: new string[] { PropertyPaths.Physics, PropertyPaths.Entities, PropertyPaths.Navigation });

          QuantumEditorGUI.Inspector(serializedObject, PropertyPaths.Entities, skipRoot: false);

          QuantumEditorGUI.Inspector(serializedObject, PropertyPaths.Physics, skipRoot: false, callback: (property, field, type) => {
            if (property.propertyPath == PropertyPaths.PhysicsLayerMatrix) {
              DrawLayersMatrix(property);
              return true;
            } else if (property.propertyPath == PropertyPaths.PhysicsLayers) {

              EditorGUILayout.Space();
              EditorGUILayout.LabelField("Layers", EditorStyles.boldLabel);
              using (new GUILayout.HorizontalScope()) {
                GUILayout.Space(EditorGUI.indentLevel * 15);
                if (GUILayout.Button("Import Layers From Unity (3D)")) {
                  data.ImportLayersFromUnity(SimulationConfigAssetHelper.PhysicsType.Physics3D);
                  EditorUtility.SetDirty(data);
                  property.serializedObject.Update();
                }

                if (GUILayout.Button("Import Layers From Unity (2D)")) {
                  data.ImportLayersFromUnity(SimulationConfigAssetHelper.PhysicsType.Physics2D);
                  EditorUtility.SetDirty(data);
                  property.serializedObject.Update();
                }
              }

              DrawLayerList(property);
              return true;
            }
            return false;
          });

          QuantumEditorGUI.Inspector(serializedObject, PropertyPaths.Navigation, skipRoot: false);
        }
      }
    }

    private void DrawLayerList(SerializedProperty property) {
      property.isExpanded = EditorGUILayout.Foldout(property.isExpanded, "Layer List", true);
      if (property.isExpanded) {

        EditorGUILayout.HelpBox("This matrix configuration is saved on the Simulation Config, but the matrix currently only shows layers that exist as Unity layers.\nThat is why the layer list below is not editable.", MessageType.Info);

        // LayerMatrixGUI won't work with custom names, changing the layer names must be done over Unity.
        using (new EditorGUI.DisabledScope(true)) {
          for (int i = 0; i < property.arraySize; ++i) {
            bool isUserLayer = i >= 8;
            var label = isUserLayer ? " Builtin Layer " : " User Layer ";
            EditorGUILayout.TextField(label, property.GetArrayElementAtIndex(i).stringValue);
          }
        }
      }
    }

    private void DrawLayersMatrix(SerializedProperty property) {
      bool show = property.isExpanded;
      UnityInternal.LayerMatrixGUI.DoGUI("Layer Matrix", ref show,
        (layerA, layerB) => {
          return (property.GetArrayElementAtIndex(layerA).intValue & (1 << layerB)) > 0;
        },
        (layerA, layerB, val) => {
          if (val) {
            property.GetArrayElementAtIndex(layerA).intValue |= (1 << layerB);
            property.GetArrayElementAtIndex(layerB).intValue |= (1 << layerA);
          } else {
            property.GetArrayElementAtIndex(layerA).intValue &= ~(1 << layerB);
            property.GetArrayElementAtIndex(layerB).intValue &= ~(1 << layerA);
          }
          property.serializedObject.ApplyModifiedProperties();
        });
      property.isExpanded = show;
    }
  }
}

#endregion

#region quantum_unity/Assets/Photon/Quantum/Editor/CustomEditors/StaticEdgeCollider2DEditor.cs
#if !QUANTUM_DISABLE_PHYSICS2D
namespace Quantum.Editor {
  using UnityEditor;
  using UnityEngine;

  [CustomEditor(typeof(QuantumStaticEdgeCollider2D))]
  public class StaticEdgeCollider2DEditor : UnityEditor.Editor {
    public static float HandlesSize = 0.075f;
    public static float DistanceToReduceHandleSize = 30.0f;

    private bool _wereToolsHidden;

    private void OnEnable() {
      _wereToolsHidden = Tools.hidden;
    }

    private void OnDisable() {
      Tools.hidden = _wereToolsHidden;
    }

    public override void OnInspectorGUI() {
      using (new QuantumEditorGUI.CustomEditorScope(serializedObject)) {
        QuantumEditorGUI.Inspector(serializedObject, drawScript: false, callback: (p, field, type) => {
          if (p.name == nameof(QuantumStaticEdgeCollider2D.Settings.Trigger)) {
            var sourceCollider = serializedObject.FindProperty(nameof(QuantumStaticEdgeCollider2D.SourceCollider));
            using (new EditorGUI.DisabledScope(sourceCollider?.objectReferenceValue != null)) {
              QuantumEditorGUI.PropertyField(p);
            }

            return true;
          }

          return false;
        });
      }

      var collider = (QuantumStaticEdgeCollider2D)target;

      EditorGUILayout.Space();

      if (collider.SourceCollider == null) {
        if (GUILayout.Button("Recenter", EditorStyles.miniButton)) {
          var center = collider.VertexA + (collider.VertexB - collider.VertexA) / 2;
          collider.VertexA -= center;
          collider.VertexB -= center;
        }
      } else if (Application.isPlaying == false) {
        collider.UpdateFromSourceCollider();
      }
    }

    public void OnSceneGUI() {
      if (EditorApplication.isPlaying)
        return;

      Tools.hidden = _wereToolsHidden;

      DrawMovementHandles((QuantumStaticEdgeCollider2D)target);
    }

    private void DrawMovementHandles(QuantumStaticEdgeCollider2D collider) {
      var handlesColor = Handles.color;
      var t = collider.transform;

      Handles.color = Color.white;
      Handles.matrix = Matrix4x4.TRS(
        t.TransformPoint(collider.PositionOffset.ToUnityVector3()),
        t.rotation * collider.RotationOffset.FlipRotation().ToUnityQuaternionDegrees(),
        t.localScale);

      { // vertex A
        var handleSize = HandlesSize * HandleUtility.GetHandleSize(collider.VertexA.ToUnityVector3());
        var cameraDistance = Vector3.Distance(SceneView.currentDrawingSceneView.camera.transform.position, collider.VertexA.ToUnityVector3());
        if (cameraDistance > DistanceToReduceHandleSize) {
          handleSize *= DistanceToReduceHandleSize / cameraDistance;
        }
        var newPosition = Handles.FreeMoveHandle(collider.VertexA.ToUnityVector3(), Quaternion.identity, handleSize, Vector3.zero, Handles.DotHandleCap);
        if (newPosition != collider.VertexA.ToUnityVector3()) {
          Undo.RegisterCompleteObjectUndo(collider, "Moving edge vertex");
          collider.VertexA = newPosition.ToFPVector2();
        }
      }
      
      { // vertex B
        var handleSize = HandlesSize * HandleUtility.GetHandleSize(collider.VertexB.ToUnityVector3());
        var cameraDistance = Vector3.Distance(SceneView.currentDrawingSceneView.camera.transform.position, collider.VertexB.ToUnityVector3());
        if (cameraDistance > DistanceToReduceHandleSize) {
          handleSize *= DistanceToReduceHandleSize / cameraDistance;
        }
        var newPosition = Handles.FreeMoveHandle(collider.VertexB.ToUnityVector3(), Quaternion.identity, handleSize, Vector3.zero, Handles.DotHandleCap);
        if (newPosition != collider.VertexB.ToUnityVector3()) {
          Undo.RegisterCompleteObjectUndo(collider, "Moving edge vertex");
          collider.VertexB = newPosition.ToFPVector2();
        }
      }
      
      Handles.color = handlesColor;
      Handles.matrix = Matrix4x4.identity;
    }
  }
}
#endif
#endregion

#region quantum_unity/Assets/Photon/Quantum/Editor/CustomEditors/StaticPolygonCollider2DEditor.cs
#if !QUANTUM_DISABLE_PHYSICS2D
namespace Quantum.Editor {

  using Photon.Deterministic;
  using System.Collections.Generic;
  using UnityEditor;
  using UnityEngine;

  [CustomEditor(typeof(QuantumStaticPolygonCollider2D))]
  public class StaticPolygonCollider2DEditor : UnityEditor.Editor {

    public static float ButtonOffset = 0.050f;
    public static float HandlesSize = 0.075f;
    public static float DistanceToReduceHandleSize = 30.0f;

    private bool _wereToolsHidden;

    private void OnEnable() {
      _wereToolsHidden = Tools.hidden;
    }

    private void OnDisable() {
      Tools.hidden = _wereToolsHidden;
    }

    public override void OnInspectorGUI() {
      using (new QuantumEditorGUI.CustomEditorScope(serializedObject)) {
        QuantumEditorGUI.Inspector(serializedObject, drawScript: false, callback: (p, field, type) => {
          if (p.name == nameof(QuantumStaticPolygonCollider2D.Settings.Trigger)) {
            var sourceCollider = serializedObject.FindProperty(nameof(QuantumStaticPolygonCollider2D.SourceCollider));
            using (new EditorGUI.DisabledScope(sourceCollider?.objectReferenceValue != null)) {
              QuantumEditorGUI.PropertyField(p);
            }
            return true;
          }
          
          return false;
        });
      }

      var collider = (QuantumStaticPolygonCollider2D)target;

      EditorGUILayout.HelpBox("Press shift to activate add buttons.\nPress control to activate remove buttons.\nSet static variables like `ButtonOffset` to fine-tune the sizing to your need.", MessageType.Info);
      EditorGUILayout.Space();

      if (GUILayout.Button("Recenter", EditorStyles.miniButton))
        collider.Vertices = FPVector2.RecenterPolygon(collider.Vertices);

      if (Application.isPlaying == false && collider.SourceCollider != null) {
        collider.UpdateFromSourceCollider(updateVertices: GUILayout.Button("Update Vertices from Source", EditorStyles.miniButton));
      }
    }

    public void OnSceneGUI() {

      if (EditorApplication.isPlaying)
        return;

      var collider = (QuantumStaticPolygonCollider2D)base.target;

      Tools.hidden = _wereToolsHidden;

      if (Event.current.shift || Event.current.control) {
        Tools.hidden = true;
        DrawAddAndRemoveButtons(collider, Event.current.shift, Event.current.control);
      }
      else {
        DrawMovementHandles(collider);
        DrawMakeCCWButton(collider);
      }
    }

    private void AddVertex(QuantumStaticPolygonCollider2D collider, int index, FPVector2 position) {
      var newVertices = new List<FPVector2>(collider.Vertices);
      newVertices.Insert(index, position);
      Undo.RegisterCompleteObjectUndo(collider, "Adding polygon vertex");
      collider.Vertices = newVertices.ToArray();
    }

    private void RemoveVertex(QuantumStaticPolygonCollider2D collider, int index) {
      var newVertices = new List<FPVector2>(collider.Vertices);
      newVertices.RemoveAt(index);
      Undo.RegisterCompleteObjectUndo(collider, "Removing polygon vertex");
      collider.Vertices = newVertices.ToArray();
    }

    private void DrawMovementHandles(QuantumStaticPolygonCollider2D collider) {
      var isCW = FPVector2.IsClockWise(collider.Vertices);
      var handlesColor = Handles.color;
      var t = collider.transform;

      Handles.color = isCW ? Color.red : Color.white;
      Handles.matrix = Matrix4x4.TRS(
        t.TransformPoint(collider.PositionOffset.ToUnityVector3()),
        t.rotation * collider.RotationOffset.FlipRotation().ToUnityQuaternionDegrees(),
        t.localScale);

      for (int i = 0; i < collider.Vertices.Length; i++) {
        var handleSize = HandlesSize * HandleUtility.GetHandleSize(collider.Vertices[i].ToUnityVector3());
        var cameraDistance = Vector3.Distance(SceneView.currentDrawingSceneView.camera.transform.position, collider.Vertices[i].ToUnityVector3());
        if (cameraDistance > DistanceToReduceHandleSize) {
          handleSize = handleSize * (DistanceToReduceHandleSize / (cameraDistance));
        }
        var newPosition = Handles.FreeMoveHandle(collider.Vertices[i].ToUnityVector3(), Quaternion.identity, handleSize, Vector3.zero, Handles.DotHandleCap);
        if (newPosition != collider.Vertices[i].ToUnityVector3()) {
          Undo.RegisterCompleteObjectUndo(collider, "Moving polygon vertex");
          collider.Vertices[i] = newPosition.ToFPVector2();
        }
      }

      Handles.color = handlesColor;
      Handles.matrix = Matrix4x4.identity;
    }

    private void DrawMakeCCWButton(QuantumStaticPolygonCollider2D collider) {
      if (FPVector2.IsPolygonConvex(collider.Vertices) && FPVector2.IsClockWise(collider.Vertices)) {
        var center = FPVector2.CalculatePolygonCentroid(collider.Vertices);
        var view = SceneView.currentDrawingSceneView;
        var screenPos = view.camera.WorldToScreenPoint(collider.transform.position + center.ToUnityVector3() + collider.PositionOffset.ToUnityVector3());
        var size = GUI.skin.label.CalcSize(new GUIContent(" Make CCW "));
        Handles.BeginGUI();
        if (GUI.Button(new Rect(screenPos.x - size.x * 0.5f, view.position.height - screenPos.y - size.y, size.x, size.y), "Make CCW")) {
          Undo.RegisterCompleteObjectUndo(collider, "Making polygon CCW");
          FPVector2.MakeCounterClockWise(collider.Vertices);
        }
        Handles.EndGUI();
      }
    } 

    private void DrawAddAndRemoveButtons(QuantumStaticPolygonCollider2D collider, bool drawAddButton, bool drawRemoveButton) {
      var handlesColor = Handles.color;
      var t = collider.transform;
      Handles.matrix = Matrix4x4.TRS(t.TransformPoint(collider.PositionOffset.ToUnityVector3()), 
                                     t.rotation * collider.RotationOffset.FlipRotation().ToUnityQuaternionDegrees(), 
                                     t.localScale);

      for (int i = 0; i < collider.Vertices.Length; i++) {
        var facePosition_FP = (collider.Vertices[i] + collider.Vertices[(i + 1) % collider.Vertices.Length]) * FP._0_50;

        var handleSize     = HandlesSize * HandleUtility.GetHandleSize(collider.Vertices[i].ToUnityVector3());
        var cameraDistance = Vector3.Distance(SceneView.currentDrawingSceneView.camera.transform.position, collider.Vertices[i].ToUnityVector3());
        if (cameraDistance > DistanceToReduceHandleSize) {
          handleSize *= (DistanceToReduceHandleSize / (cameraDistance));
        }

        if (drawRemoveButton) {
          if (collider.Vertices.Length > 3) {

            Handles.color = Color.red;
            if (Handles.Button(collider.Vertices[i].ToUnityVector3(), Quaternion.identity, handleSize, handleSize, Handles.DotHandleCap)) {
              RemoveVertex(collider, i);
              return;
            }
          }
        }

        if (drawAddButton) {
          Handles.color = Color.green;
          if (Handles.Button(facePosition_FP.ToUnityVector3(), Quaternion.identity, handleSize, handleSize, Handles.DotHandleCap)) {
            AddVertex(collider, i + 1, facePosition_FP);
            return;
          }
        }
      }

      Handles.color  = handlesColor;
      Handles.matrix = Matrix4x4.identity;
    }
  }
}
#endif
#endregion

#region quantum_unity/Assets/Photon/Quantum/Editor/CustomEditors/TerrainDataEditor.cs
namespace Quantum.Editor {

  using UnityEditor;
  using UnityEngine;

  [CustomEditor(typeof(QuantumStaticTerrainCollider3D), true)]
  public class TerrainDataEditor : UnityEditor.Editor {
    public override void OnInspectorGUI() {
      base.DrawDefaultInspector();

      var data = target as QuantumStaticTerrainCollider3D;
      if (data) {

        if (data.Asset) {
          EditorGUI.BeginDisabledGroup(EditorApplication.isPlayingOrWillChangePlaymode);

          if (GUILayout.Button("Bake Terrain Data", EditorStyles.miniButton)) {
            Debug.Log("Baking Terrain Data");
            data.Bake();
            EditorUtility.SetDirty(data.Asset);
            data.Asset.Loaded();
            AssetDatabase.Refresh();
          }

          

          EditorGUI.EndDisabledGroup();
        }

        OnInspectorGUI(data);

        QuantumEditorGUI.Header("Experimental");
        data.SmoothSphereMeshCollisions = EditorGUI.Toggle(EditorGUILayout.GetControlRect(), "Smooth Sphere Mesh Collisions", data.SmoothSphereMeshCollisions);
      }
    }

    void OnInspectorGUI(QuantumStaticTerrainCollider3D data) {
      //data.transform.position = Vector3.zero;

      if (data.Asset) {
        EditorGUILayout.Separator();
        EditorGUILayout.LabelField("Asset Settings", EditorStyles.boldLabel);

        var asset = new SerializedObject(data.Asset);
        var property = asset.GetIterator();

        // enter first child
        property.Next(true);

        while (property.Next(false)) {
          if (property.name.StartsWith("m_")) {
            continue;
          }

          EditorGUILayout.PropertyField(property, true);
        }

        asset.ApplyModifiedProperties();
      }
    }
  }
}
#endregion