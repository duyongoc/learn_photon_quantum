#if QUANTUM_STALL_WATCHER_ENABLED

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using UnityEngine;


namespace Quantum {

  public class QuantumStallWatcher : MonoBehaviour {

    public const QuantumStallWatcherCrashType DefaultPlayerCrashType =
#if UNITY_STANDALONE_WIN
      QuantumStallWatcherCrashType.DivideByZero;
#elif UNITY_STANDALONE_OSX
      QuantumStallWatcherCrashType.DivideByZero;
#elif UNITY_ANDROID
      QuantumStallWatcherCrashType.AccessViolation;
#elif UNITY_IOS
      QuantumStallWatcherCrashType.Abort;
#else
      QuantumStallWatcherCrashType.AccessViolation;
#endif

    public float Timeout = 10.0f;

    [Tooltip("How to crash if stalling in the Editor")]
    public QuantumStallWatcherCrashType EditorCrashType = QuantumStallWatcherCrashType.DivideByZero;

    [Tooltip("How to crash if stalling in the Player. Which crash types produce crash dump is platform-specific.")]
    public QuantumStallWatcherCrashType PlayerCrashType = DefaultPlayerCrashType;

    public new bool DontDestroyOnLoad = false;


    [Space]
    [InspectorButton("Editor_RestoreDefaultCrashType", "Reset Crash Type To The Target Platform's Default")]
    public bool Button_StartInstantReplay;

    private Worker _worker;
    private bool _started;

    private void Awake() {
      if (DontDestroyOnLoad) {
        DontDestroyOnLoad(gameObject);
      }
    }

    private void Start() {
      _started = true;
      OnEnable();
    }

    private void Update() {
      _worker.NotifyUpdate();
    }

    private void OnEnable() {
      if (!_started) {
        return;
      }
      _worker = new Worker(checked((int)(Timeout * 1000)), Application.isEditor ? EditorCrashType : PlayerCrashType);
    }

    private void OnDisable() {
      _worker.Dispose();
      _worker = null;
    }

    private static class Native {
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
      const string LibCName = "msvcrt.dll";
#else
      const string LibCName = "libc";
#endif

      [StructLayout(LayoutKind.Sequential)]
      public struct div_t {
        public int quot;
        public int rem;
      }

      [DllImport(LibCName, EntryPoint = "abort", CallingConvention = CallingConvention.Cdecl, SetLastError = false)]
      public static extern void LibCAbort();
      [DllImport(LibCName, EntryPoint = "memcpy", CallingConvention = CallingConvention.Cdecl, SetLastError = false)]
      public static extern IntPtr LibCMemcpy(IntPtr dest, IntPtr src, UIntPtr count);
      [DllImport(LibCName, EntryPoint = "div", CallingConvention = CallingConvention.Cdecl, SetLastError = false)]
      public static extern div_t LibCDiv(int numerator, int denominator);
    }


    private sealed class Worker : IDisposable {

      private Thread thread;
      private AutoResetEvent updateStarted = new AutoResetEvent(false);
      private AutoResetEvent shutdown = new AutoResetEvent(false);

      public Worker(int timeoutMills, QuantumStallWatcherCrashType crashType) {

        thread = new Thread(() => {

          var startedHandles = new WaitHandle[] { shutdown, updateStarted };

          for (; ; ) {
            // wait for the update to finish
            int index = WaitHandle.WaitAny(startedHandles, timeoutMills);
            if (index == 0) {
              // shutdown
              break;
            } else if (index == 1) {
              // ok
            } else {
              int crashResult = Crash(crashType);
              Debug.LogError($"Crash failed with result: {crashResult}");
              // a crash should have happened by now
              break;
            }

          }
        }) {
          Name = "QuantumStallWatcherWorker"
        };
        thread.Start();
      }

      public void NotifyUpdate() {
        updateStarted.Set();
      }

      public void Dispose() {
        shutdown.Set();
        if (thread.Join(1000) == false) {
          Debug.LogError($"Failed to join the {thread.Name}");
        }
      }

      [MethodImpl(MethodImplOptions.NoOptimization)]
      public int Crash(QuantumStallWatcherCrashType type, int zero = 0) {
        Debug.LogWarning($"Going to crash... mode: {type}");

        int result = -1;

        if (type == QuantumStallWatcherCrashType.Abort) {
          Native.LibCAbort();
          result = 0;
        } else if (type == QuantumStallWatcherCrashType.AccessViolation) {
          unsafe {
            int* data = stackalloc int[1];
            data[0] = 5;
            Native.LibCMemcpy(new IntPtr(zero), new IntPtr(data), new UIntPtr(sizeof(int)));
            result = 1;
          }
        } else if (type == QuantumStallWatcherCrashType.DivideByZero) {
          result = Native.LibCDiv(5, zero).quot;
        }

        return result;
      }

    }

    public void Editor_RestoreDefaultCrashType() {
      PlayerCrashType = DefaultPlayerCrashType;
    }
  }

  public enum QuantumStallWatcherCrashType {
    AccessViolation,
    Abort,
    DivideByZero
  }

}

#endif