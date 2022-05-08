using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using UnityEngine;

namespace Quantum {
  public class QuantumUnityJsonSerializer : Quantum.JsonAssetSerializerBase
  {
    protected override object FromJson(string json, Type type)
    {
      return JsonUtility.FromJson(json, type);
    }

    protected override string ToJson(object obj)
    {
      return JsonUtility.ToJson(obj, IsPrettyPrintEnabled);
    }
  }
}