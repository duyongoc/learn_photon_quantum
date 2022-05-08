using System;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
using System.Reflection;
#endif

namespace Quantum {
  [AttributeUsage(AttributeTargets.Field)]
  public class QuantumInspectorAttribute : PropertyAttribute {
  }
}