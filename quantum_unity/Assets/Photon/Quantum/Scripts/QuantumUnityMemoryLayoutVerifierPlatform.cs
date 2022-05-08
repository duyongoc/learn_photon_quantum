using System;
using System.Reflection;
using Unity.Collections.LowLevel.Unsafe;

public class QuantumUnityMemoryLayoutVerifierPlatform : Quantum.MemoryLayoutVerifier.IPlatform {
  public int FieldOffset(FieldInfo field) {
    return UnsafeUtility.GetFieldOffset(field);
  }

  public int SizeOf(Type type) {
    return UnsafeUtility.SizeOf(type);
  }

  public bool CanResolveEnumSize {
    get { return true; }
  }
}