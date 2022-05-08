using UnityEngine;

public class QuantumMapLoader : MonoBehaviour {

  private static QuantumMapLoader _instance;
  private static bool _isApplicationQuitting;

  public static QuantumMapLoader Instance {
    get {
      if (_isApplicationQuitting) {
        return null;
      }

      if (_instance == null) {
        _instance = GameObject.FindObjectOfType<QuantumMapLoader>();
      }

      if (_instance == null) {
        _instance = new GameObject("QuantumMapLoader").AddComponent<QuantumMapLoader>();
      }

      return _instance;
    }
  }

  public void Awake() {
    DontDestroyOnLoad(this);
  }

  public void OnApplicationQuit() {
    _isApplicationQuitting = true;
  }
}