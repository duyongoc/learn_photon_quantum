//#define QUANTUM_UNITY_CALLBACKS_VERBOSE_LOG

using System;
using System.Collections;
using System.Diagnostics;
using Quantum;
using UnityEngine;
using UnityEngine.SceneManagement;
using Debug = UnityEngine.Debug;

public class QuantumCallbackHandler_UnityCallbacks : IDisposable {
  private Coroutine _coroutine;
  private Map _currentMap;
  private bool _currentSceneNeedsCleanup;

  private readonly CallbackUnitySceneLoadBegin _callbackUnitySceneLoadBegin;
  private readonly CallbackUnitySceneLoadDone _callbackUnitySceneLoadDone;
  private readonly CallbackUnitySceneUnloadBegin _callbackUnitySceneUnloadBegin;
  private readonly CallbackUnitySceneUnloadDone _callbackUnitySceneUnloadDone;

  public QuantumCallbackHandler_UnityCallbacks(QuantumGame game) {
    _callbackUnitySceneLoadBegin = new CallbackUnitySceneLoadBegin(game);
    _callbackUnitySceneLoadDone = new CallbackUnitySceneLoadDone(game);
    _callbackUnitySceneUnloadBegin = new CallbackUnitySceneUnloadBegin(game);
    _callbackUnitySceneUnloadDone = new CallbackUnitySceneUnloadDone(game);
  }

  public static IDisposable Initialize() {
    return QuantumCallback.SubscribeManual((CallbackGameStarted c) => {
      var runner = QuantumRunner.FindRunner(c.Game);
      if (runner != QuantumRunner.Default) {
        // only work for the default runner
        return;
      }

      var callbacksHost = new QuantumCallbackHandler_UnityCallbacks(c.Game);

      //callbacksHost._currentMap = runner.Game.Frames?.Verified?.Map;

      // TODO: this has a bug: disposing parent sub doesn't cancel following subscriptions
      QuantumCallback.Subscribe(runner, (CallbackGameDestroyed cc) => callbacksHost.Dispose(), runner: runner);
      QuantumCallback.Subscribe(runner, (CallbackUpdateView cc) => callbacksHost.UpdateLoading(cc.Game), runner: runner);
    });
  }

  public void Dispose() {
    QuantumCallback.UnsubscribeListener(this);

    if (_coroutine != null) {
      Log.Warn("Map loading or unloading was still in progress when destroying the game");
    }

    if (_currentMap != null && _currentSceneNeedsCleanup) {
      _coroutine = QuantumMapLoader.Instance?.StartCoroutine(UnloadScene(_currentMap.Scene));
      _currentMap = null;
    }
  }

  private static void PublishCallback<T>(T callback, string sceneName) where T : CallbackBase, ICallbackUnityScene {
    VerboseLog($"Publishing callback {typeof(T)} with {sceneName}");
    callback.SceneName = sceneName;
    QuantumCallback.Dispatcher.Publish(callback);
  }

  private IEnumerator SwitchScene(string previousSceneName, string newSceneName, bool unloadFirst) {
    if (string.IsNullOrEmpty(previousSceneName)) {
      throw new ArgumentException(nameof(previousSceneName));
    }
    if (string.IsNullOrEmpty(newSceneName)) {
      throw new ArgumentException(nameof(newSceneName));
    }

    VerboseLog($"Switching scenes from {previousSceneName} to {newSceneName} (unloadFirst: {unloadFirst})");

    try {
      LoadSceneMode loadSceneMode = LoadSceneMode.Additive;

      if (unloadFirst) {
        if (SceneManager.sceneCount == 1) {
          Debug.Assert(SceneManager.GetActiveScene().name == previousSceneName);
          VerboseLog($"Need to create a temporary scene, because {previousSceneName} is the only scene loaded.");

          SceneManager.CreateScene("QuantumTemporaryEmptyScene");
          loadSceneMode = LoadSceneMode.Single;
        }

        PublishCallback(_callbackUnitySceneUnloadBegin, previousSceneName);
        yield return SceneManager.UnloadSceneAsync(previousSceneName);
        PublishCallback(_callbackUnitySceneUnloadDone, previousSceneName);
      }

      PublishCallback(_callbackUnitySceneLoadBegin, newSceneName);
      yield return SceneManager.LoadSceneAsync(newSceneName, loadSceneMode);
      var newScene = SceneManager.GetSceneByName(newSceneName);
      if (newScene.IsValid()) {
        SceneManager.SetActiveScene(newScene);
      }
      PublishCallback(_callbackUnitySceneLoadDone, newSceneName);

      if (!unloadFirst) {
        PublishCallback(_callbackUnitySceneUnloadBegin, previousSceneName);
        yield return SceneManager.UnloadSceneAsync(previousSceneName);
        PublishCallback(_callbackUnitySceneUnloadDone, previousSceneName);
      }
    } finally {
      _coroutine = null;
    }
  }

  private System.Collections.IEnumerator LoadScene(string sceneName) {
    try {
      PublishCallback(_callbackUnitySceneLoadBegin, sceneName);
      yield return SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
      PublishCallback(_callbackUnitySceneLoadDone, sceneName);
    } finally {
      _coroutine = null;
    }
  }

  private System.Collections.IEnumerator UnloadScene(string sceneName) {
    try {
      PublishCallback(_callbackUnitySceneUnloadBegin, sceneName);
      yield return SceneManager.UnloadSceneAsync(sceneName);
      PublishCallback(_callbackUnitySceneUnloadDone, sceneName);
    } finally {
      _coroutine = null;
    }
  }

  private void UpdateLoading(QuantumGame game) {
    var loadMode = game.Configurations.Simulation.AutoLoadSceneFromMap;
    if (loadMode == SimulationConfig.AutoLoadSceneFromMapMode.Disabled) {
      return;
    }

    if (_coroutine != null) {
      return;
    }

    var map = game.Frames.Verified.Map;
    if (map == _currentMap) {
      return;
    }

    bool isNewSceneLoaded = SceneManager.GetSceneByName(map.Scene).IsValid();
    if (isNewSceneLoaded) {
      VerboseLog($"Scene {map.Scene} appears to have been loaded externally.");
      _currentMap = map;
      _currentSceneNeedsCleanup = false;
      return;
    }

    var coroHost = QuantumMapLoader.Instance;
    Debug.Assert(coroHost != null);

    string previousScene = _currentMap?.Scene ?? string.Empty;
    string newScene = map.Scene;

    _currentMap = map;
    _currentSceneNeedsCleanup = true;

    if (SceneManager.GetSceneByName(previousScene).IsValid()) {
      VerboseLog($"Previous scene \"{previousScene}\" was loaded, starting transition with mode {loadMode}");
      if (loadMode == SimulationConfig.AutoLoadSceneFromMapMode.LoadThenUnloadPreviousScene) {
        _coroutine = coroHost.StartCoroutine(SwitchScene(previousScene, newScene, unloadFirst: false));
        _currentMap = map;
      } else if (loadMode == SimulationConfig.AutoLoadSceneFromMapMode.UnloadPreviousSceneThenLoad) {
        _coroutine = coroHost.StartCoroutine(SwitchScene(previousScene, newScene, unloadFirst: true));
        _currentMap = map;
      } else {
        // legacy mode
        _coroutine = coroHost.StartCoroutine(UnloadScene(previousScene));
        _currentMap = null;
      }
    } else {
      // simply load the scene async
      VerboseLog($"Previous scene \"{previousScene}\" was not loaded.");
      _coroutine = coroHost.StartCoroutine(LoadScene(newScene));
      _currentMap = map;
    }
  }

  [Conditional("QUANTUM_UNITY_CALLBACKS_VERBOSE_LOG")]
  private static void VerboseLog(string msg) {
    Debug.LogFormat("QuantumUnityCallbacks: {0}", msg);
  }
}