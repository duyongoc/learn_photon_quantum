using System;
using System.Diagnostics;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Quantum {
  public class ProgressBar : IDisposable {
    float _progress;
    string _info;
#pragma warning disable CS0414 // The private field is assigned but its value is never used (#if UNITY_EDITOR)
    string _title;
    bool _isCancelable;
#pragma warning restore CS0414 // The private field is assigned but its value is never used
    Stopwatch _sw;

    public ProgressBar(string title, bool isCancelable = false, bool logStopwatch = false) {
      _title = title;
      _isCancelable = isCancelable;
      if (logStopwatch) {
        _sw = Stopwatch.StartNew();
      }
    }

    public string Info {
      set {
        DisplayStopwatch();
        _info = value;
        _progress = 0.0f;
        Display();
      }
    }

    public float Progress {
      set {
        bool hasChanged = Mathf.Abs(_progress - value) > 0.01f;
        if (!hasChanged)
          return;

        _progress = value;
        Display();
      }

      get {
        return _progress;
      }
    }

    public void Dispose() {
#if UNITY_EDITOR
      EditorUtility.ClearProgressBar();
      DisplayStopwatch();
#endif
    }

    private void Display() {
#if UNITY_EDITOR
      if (_isCancelable) {
        bool isCanceled = EditorUtility.DisplayCancelableProgressBar(_title, _info, _progress);
        if (isCanceled) {
          throw new Exception(_title + " canceled");
        }
      }
      else {
        EditorUtility.DisplayProgressBar(_title, _info, _progress);
      }
#endif
    }

    private void DisplayStopwatch() {
      if (_sw != null && !string.IsNullOrEmpty(_info)) {
        UnityEngine.Debug.LogFormat("'{0}' took {1} ms", _info, _sw.ElapsedMilliseconds);
        _sw.Reset();
        _sw.Start();
      }
    }
  }
}