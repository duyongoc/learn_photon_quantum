using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Quantum {
  [Serializable]
  public struct FloatMinMax {
    public Single Min;
    public Single Max;

    public FloatMinMax(Single min, Single max) {
      Min = min;
      Max = max;
    }
  }

  [AttributeUsage(AttributeTargets.Field, Inherited = true, AllowMultiple = false)]
  public class MinMaxSliderAttribute : PropertyAttribute {
    public readonly float Min;
    public readonly float Max;

    public MinMaxSliderAttribute() 
      : this(0, 1) {
    }

    public MinMaxSliderAttribute(float min, float max) {
      Min = min;
      Max = max;
    }
  }
}