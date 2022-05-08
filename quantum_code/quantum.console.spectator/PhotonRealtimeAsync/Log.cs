using System;
using System.Diagnostics;

namespace PhotonRealtimeAsync {
  public static class Log {
    static object sync;
    static Action<String> infoCallback;
    static Action<String> warnCallback;
    static Action<String> errorCallback;
    static Action<Exception> exnCallback;

    static public void Init(Action<String> info, Action<String> warn, Action<String> error, Action<Exception> exn) {
      sync = new object();
      infoCallback = info;
      warnCallback = warn;
      errorCallback = error;
      exnCallback = exn;
    }

    static public void InitForConsole() {
      Init(
        info => {
          Console.WriteLine(info);
        },

        warn => {
          Console.ForegroundColor = ConsoleColor.Yellow;
          Console.WriteLine(warn);
          Console.ResetColor();
        },

        error => {
          Console.ForegroundColor = ConsoleColor.Red;
          Console.WriteLine(error);
          Console.ResetColor();
        },

        exn => {
          Console.ForegroundColor = ConsoleColor.Red;
          Console.WriteLine(exn.Message);
          Console.WriteLine(exn.StackTrace);
          Console.ResetColor();
        }
      );
    }

    static public void InitForSystemDiagnostics() {
      Init(
        info => {
          System.Diagnostics.Trace.WriteLine(info);
        },

        warn => {
          System.Diagnostics.Trace.WriteLine(warn, "Warning");
        },

        error => {
          System.Diagnostics.Trace.WriteLine(error, "Error");
        },

        exn => {
          System.Diagnostics.Trace.WriteLine(exn.Message, "Error");
          System.Diagnostics.Trace.WriteLine(exn.StackTrace, "Error");
        }
      );
    }

#if UNITY_5_3_OR_NEWER
    static public void InitForUnity() {
      Init(UnityEngine.Debug.Log, UnityEngine.Debug.LogWarning, UnityEngine.Debug.LogError, UnityEngine.Debug.LogException);
    }
#endif

    [Conditional("DEBUG")]
    static public void Debug(object value) {
      Info(value);
    }
    
    [Conditional("TRACE")]
    static public void Trace(object value) {
      Info(value);
    }

    static public void Info(object value) {
      if (infoCallback != null) {
        lock (sync) {
          infoCallback(value == null ? "NULL" : value.ToString());
        }
      }
    }

    static public void Warn(object value) {
      if (warnCallback != null) {
        lock (sync) {
          warnCallback(value == null ? "NULL" : value.ToString());
        }
      }
    }

    static public void Error(object value) {
      if (errorCallback != null) {
        lock (sync) {
          errorCallback(value == null ? "NULL" : value.ToString());
        }
      }
    }

    static public void Exception(Exception exn) {
      if (exnCallback != null) {
        lock (sync) {
          exnCallback(exn);
        }
      }
    }
  }
}
