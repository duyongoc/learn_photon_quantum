using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;

#if UNITY_EDITOR

using UnityEditor;
using UnityEngine;

namespace Quantum {
  public static class SerializedObjectExtensions {

    private static readonly Regex _arrayElementRegex = new Regex(@"\.Array\.data\[\d+\]$", RegexOptions.Compiled);

    public static SerializedProperty FindPropertyOrThrow(this SerializedObject so, string propertyPath) {
      var result = so.FindProperty(propertyPath);
      if (result == null)
        throw new ArgumentOutOfRangeException($"Property not found: {propertyPath}");
      return result;
    }

    public static SerializedProperty FindPropertyRelativeOrThrow(this SerializedProperty sp, string relativePropertyPath) {
      var result = sp.FindPropertyRelative(relativePropertyPath);
      if (result == null)
        throw new ArgumentOutOfRangeException($"Property not found: {relativePropertyPath}");
      return result;
    }

    public static SerializedProperty FindPropertyRelativeToParent(this SerializedProperty property, string relativePath) {
      SerializedProperty otherProperty;

      var path = property.propertyPath;

      // array element?
      if (path[path.Length-1] == ']') {
        var match = _arrayElementRegex.Match(path);
        if (match.Success) {
          path = path.Substring(0, match.Index);
        }
      }

      var lastDotIndex = path.LastIndexOf('.');
      if (lastDotIndex < 0) {
        otherProperty = property.serializedObject.FindProperty(relativePath);
      } else {
        otherProperty = property.serializedObject.FindProperty(path.Substring(0, lastDotIndex));
        if (otherProperty != null) {
          otherProperty = otherProperty.FindPropertyRelative(relativePath);
        }
      }
      return otherProperty;
    }

    public static SerializedProperty FindPropertyRelativeToParentOrThrow(this SerializedProperty property, string relativePath) {
      var result = property.FindPropertyRelativeToParent(relativePath);
      if (result == null) {
        throw new ArgumentOutOfRangeException($"Property relative to the parent of \"{property.propertyPath}\" not found: {relativePath}");
      }
      return result;
    }

    public static float sss;

    public static Int64 GetIntegerValue(this SerializedProperty sp) {
      switch (sp.type) {
        case "int":
        case "bool": return sp.intValue;
        case "long": return sp.longValue;
        case "FP": return sp.FindPropertyRelative("RawValue").longValue;
        case "Enum": return sp.intValue;
        default:
          switch (sp.propertyType) {
            case SerializedPropertyType.ObjectReference:
              return sp.objectReferenceInstanceIDValue;
          }
          return 0;

      }
    }

    public static void SetIntegerValue(this SerializedProperty sp, long value) {
      switch (sp.type) {
        case "int":
          sp.intValue = (int)value;
          break;
        case "bool":
          sp.boolValue = value != 0;
          break;
        case "long":
          sp.longValue = value;
          break;
        case "FP":
          sp.FindPropertyRelative("RawValue").longValue = value;
          break;
        case "Enum":
          sp.intValue = (int)value;
          break;
        default:
          throw new NotSupportedException($"Type {sp.type} is not supported");
      }
    }


    public static SerializedPropertyEnumerable Children(this SerializedProperty property, bool visibleOnly = true) {
      return new SerializedPropertyEnumerable(property, visibleOnly);
    }

    public static string GetPropertyPath<T, U>(System.Linq.Expressions.Expression<Func<T, U>> propertyLambda) {
      System.Linq.Expressions.Expression expression = propertyLambda.Body;
      System.Text.StringBuilder pathBuilder = new System.Text.StringBuilder();

      for (; ;) {
        var fieldExpression = expression as System.Linq.Expressions.MemberExpression;
        if (fieldExpression?.Member is FieldInfo field) {
          if (pathBuilder.Length != 0) {
            pathBuilder.Insert(0, '.');
          }
          pathBuilder.Insert(0, field.Name);
          expression = fieldExpression.Expression;
        } else {
          if (expression is System.Linq.Expressions.ParameterExpression parameterExpression) {
            return pathBuilder.ToString();
          } else {
            throw new System.ArgumentException($"Only field expressions allowed: {expression}");
          }
        }
      }
    }


    public static SerializedProperty GetArraySizePropertyOrThrow(this SerializedProperty prop) {
      if (prop == null) {
        throw new ArgumentNullException(nameof(prop));
      }
      if (!prop.isArray) {
        throw new ArgumentException("Not an array", nameof(prop));
      }

      var copy = prop.Copy();
      if (!copy.Next(true) || !copy.Next(true)) {
        throw new InvalidOperationException();
      }

      if ( copy.propertyType != SerializedPropertyType.ArraySize ) {
        throw new InvalidOperationException();
      }

      return copy;
    }

    public struct SerializedPropertyEnumerable : IEnumerable<SerializedProperty> {
      private SerializedProperty property;
      private bool visible;

      public SerializedPropertyEnumerable(SerializedProperty property, bool visible) {
        this.property = property;
        this.visible = visible;
      }

      public SerializedPropertyEnumerator GetEnumerator() {
        return new SerializedPropertyEnumerator(property, visible);
      }

      IEnumerator<SerializedProperty> IEnumerable<SerializedProperty>.GetEnumerator() {
        return GetEnumerator();
      }

      IEnumerator IEnumerable.GetEnumerator() {
        return GetEnumerator();
      }
    }

    public struct SerializedPropertyEnumerator : IEnumerator<SerializedProperty> {
      private SerializedProperty current;
      private bool enterChildren;
      private bool visible;
      private int parentDepth;

      public SerializedPropertyEnumerator(SerializedProperty parent, bool visible) {
        current = parent.Copy();
        enterChildren = true;
        parentDepth = parent.depth;
        this.visible = visible;
      }

      public SerializedProperty Current => current;

      SerializedProperty IEnumerator<SerializedProperty>.Current => current;

      object IEnumerator.Current => current;

      public void Dispose() {
        current.Dispose();
      }

      public bool MoveNext() {
        bool entered = visible ? current.NextVisible(enterChildren) : current.Next(enterChildren);
        enterChildren = false;
        if (!entered) {
          return false;
        }
        if (current.depth <= parentDepth) {
          return false;
        }
        return true;
      }

      public void Reset() {
        throw new NotImplementedException();
      }
    }
  }

  public class SerializedPropertyPathBuilder<T> {
    public static string GetPropertyPath<U>(System.Linq.Expressions.Expression<Func<T, U>> expression) {
      return SerializedObjectExtensions.GetPropertyPath(expression);
    }
  }

  public class SerializedPropertyEqualityComparer : IEqualityComparer<SerializedProperty> {

    public static SerializedPropertyEqualityComparer Instance = new SerializedPropertyEqualityComparer();

    public bool Equals(SerializedProperty x, SerializedProperty y) {
      return SerializedProperty.DataEquals(x, y);
    }

    public int GetHashCode(SerializedProperty p) {
      
      bool enterChildren;
      bool isFirst = true;
      int hashCode = 0;
      int minDepth = p.depth + 1;

      do {

        enterChildren = false;

        switch (p.propertyType) {
          case SerializedPropertyType.Integer:          hashCode = HashCodeUtils.CombineHashCodes(hashCode, p.intValue); break;
          case SerializedPropertyType.Boolean:          hashCode = HashCodeUtils.CombineHashCodes(hashCode, p.boolValue.GetHashCode()); break;
          case SerializedPropertyType.Float:            hashCode = HashCodeUtils.CombineHashCodes(hashCode, p.floatValue.GetHashCode()); break;
          case SerializedPropertyType.String:           hashCode = HashCodeUtils.CombineHashCodes(hashCode, p.stringValue.GetHashCode()); break;
          case SerializedPropertyType.Color:            hashCode = HashCodeUtils.CombineHashCodes(hashCode, p.colorValue.GetHashCode()); break;
          case SerializedPropertyType.ObjectReference:  hashCode = HashCodeUtils.CombineHashCodes(hashCode, p.objectReferenceInstanceIDValue); break;
          case SerializedPropertyType.LayerMask:        hashCode = HashCodeUtils.CombineHashCodes(hashCode, p.intValue); break;
          case SerializedPropertyType.Enum:             hashCode = HashCodeUtils.CombineHashCodes(hashCode, p.intValue); break;
          case SerializedPropertyType.Vector2:          hashCode = HashCodeUtils.CombineHashCodes(hashCode, p.vector2Value.GetHashCode()); break;
          case SerializedPropertyType.Vector3:          hashCode = HashCodeUtils.CombineHashCodes(hashCode, p.vector3Value.GetHashCode()); break;
          case SerializedPropertyType.Vector4:          hashCode = HashCodeUtils.CombineHashCodes(hashCode, p.vector4Value.GetHashCode()); break;
          case SerializedPropertyType.Vector2Int:       hashCode = HashCodeUtils.CombineHashCodes(hashCode, p.vector2IntValue.GetHashCode()); break;
          case SerializedPropertyType.Vector3Int:       hashCode = HashCodeUtils.CombineHashCodes(hashCode, p.vector3IntValue.GetHashCode()); break;
          case SerializedPropertyType.Rect:             hashCode = HashCodeUtils.CombineHashCodes(hashCode, p.rectValue.GetHashCode()); break;
          case SerializedPropertyType.RectInt:          hashCode = HashCodeUtils.CombineHashCodes(hashCode, p.rectIntValue.GetHashCode()); break;
          case SerializedPropertyType.ArraySize:        hashCode = HashCodeUtils.CombineHashCodes(hashCode, p.intValue); break;
          case SerializedPropertyType.Character:        hashCode = HashCodeUtils.CombineHashCodes(hashCode, p.intValue.GetHashCode()); break;
          case SerializedPropertyType.AnimationCurve:   hashCode = HashCodeUtils.CombineHashCodes(hashCode, p.animationCurveValue.GetHashCode()); break;
          case SerializedPropertyType.Bounds:           hashCode = HashCodeUtils.CombineHashCodes(hashCode, p.boundsValue.GetHashCode()); break;
          case SerializedPropertyType.BoundsInt:        hashCode = HashCodeUtils.CombineHashCodes(hashCode, p.boundsIntValue.GetHashCode()); break;
          case SerializedPropertyType.ExposedReference: hashCode = HashCodeUtils.CombineHashCodes(hashCode, p.exposedReferenceValue.GetHashCode()); break;
          default: {
              enterChildren = true;
              break;
            }
        }

        if (isFirst) {
          if (!enterChildren) {
            // no traverse needed
            return hashCode;
          }

          // since property is going to be traversed, a copy needs to be made
          p = p.Copy();
          isFirst = false;
        }
      } while (p.Next(enterChildren) && p.depth >= minDepth);

      return hashCode;
    }
  }

}

#endif