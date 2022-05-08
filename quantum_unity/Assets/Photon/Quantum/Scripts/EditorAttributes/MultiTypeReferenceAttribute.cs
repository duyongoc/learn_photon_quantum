using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
using System.Reflection;
#endif

namespace Quantum {
  [AttributeUsage(AttributeTargets.Field)]
  public class MultiTypeReferenceAttribute : PropertyAttribute {
    public MultiTypeReferenceAttribute(params Type[] types) {
      Types = types;
    }

    public readonly Type[] Types;
  }
}