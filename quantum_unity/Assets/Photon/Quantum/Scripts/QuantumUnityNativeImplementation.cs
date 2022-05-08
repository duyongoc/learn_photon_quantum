using System;
using Photon.Deterministic;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

public sealed unsafe class QuantumUnityNativeAllocator : Photon.Deterministic.Native.Allocator {
  public sealed override void* Alloc(int count) {
    var ptr = UnsafeUtility.Malloc((long)count, 4, Allocator.Persistent);
    TrackAlloc(ptr);
    return ptr;
  }

  public sealed override void* Alloc(int count, int alignment) {
    var ptr = UnsafeUtility.Malloc(count, alignment, Allocator.Persistent);
    TrackAlloc(ptr);
    return ptr;
  }

  public sealed override void Free(void* ptr) {
    TrackFree(ptr);
    UnsafeUtility.Free(ptr, Allocator.Persistent);
  }

  protected sealed override void Clear(void* dest, int count) {
    UnsafeUtility.MemClear(dest, (long)count);
  }
}

public unsafe class QuantumUnityNativeUtility : Photon.Deterministic.Native.Utility {
  static class ObjectPinner {
    // this is technically not pinned... but w/e
    static object _pinLock = new object();
    static object _pinnedObject;
    static ulong  _pinnedHandle;

    static void VerifyHandle(Photon.Deterministic.Native.ObjectHandle handle) {
      if (handle.Identifier == 0) {
        throw new InvalidOperationException("ObjectHandle.Identifier can't be zero");
      }

      if (handle.Address != IntPtr.Zero) {
        throw new InvalidOperationException("ObjectHandle.Address has to be null");
      }
    }

    public static Photon.Deterministic.Native.ObjectHandle HandleAcquire(object obj) {
      lock (_pinLock) {
        if (_pinnedObject != null) {
          throw new InvalidOperationException($"{nameof(QuantumUnityNativeUtility)} can only pin one object at a time");
        }

        _pinnedObject = obj;
        ++_pinnedHandle;
        return new Photon.Deterministic.Native.ObjectHandle(_pinnedHandle);
      }
    }

    public static void HandleRelease(Photon.Deterministic.Native.ObjectHandle handle) {
      lock (_pinLock) {
        VerifyHandle(handle);

        if (_pinnedHandle != handle.Identifier) {
          throw new InvalidOperationException($"Tried to release handle {handle.Identifier} which does not match current handle {_pinnedHandle}");
        }

        ++_pinnedHandle;
        _pinnedObject = null;
      }
    }

    public static object GetObjectForHandle(Photon.Deterministic.Native.ObjectHandle handle) {
      lock (_pinLock) {
        VerifyHandle(handle);

        if (_pinnedHandle != handle.Identifier) {
          throw new InvalidOperationException($"Tried to get object for handle {handle.Identifier} which does not match current handle {_pinnedHandle}");
        }

        return _pinnedObject;
      }
    }
  }

  public override Photon.Deterministic.Native.ObjectHandle HandleAcquire(object obj) {
    return ObjectPinner.HandleAcquire(obj);
  }

  public override void HandleRelease(Photon.Deterministic.Native.ObjectHandle handle) {
    ObjectPinner.HandleRelease(handle);
  }

  public override object GetObjectForHandle(Photon.Deterministic.Native.ObjectHandle handle) {
    return ObjectPinner.GetObjectForHandle(handle);
  }

  public override void Clear(void* dest, int count) {
    UnsafeUtility.MemClear(dest, (long)count);
  }

  public override void Copy(void* dest, void* src, int count) {
    UnsafeUtility.MemCpy(dest, src, (long)count);
  }

  public override void Move(void* dest, void* src, int count) {
    UnsafeUtility.MemMove(dest, src, (long)count);
  }

  public override unsafe int Compare(void* ptr1, void* ptr2, int count) {
    return UnsafeUtility.MemCmp(ptr1, ptr2, count);
  }
}