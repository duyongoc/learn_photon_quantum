using Photon.Realtime;
using UnityEngine;

namespace Quantum.Demo {
  public abstract class UIScreen : MonoBehaviour {
    public GameObject Panel;
    public bool StartEnabled;

    public virtual bool VerifyCanShow() {
      return true;
    }

    public virtual void OnShowScreen(bool first) {

    }

    public virtual void OnHideScreen(bool first) {

    }

    public virtual void OnScreenDestroy() {

    }

    public virtual void ResetScreenToStartState(bool cascade) {
    }

    public bool IsScreenInstanceVisible() {
      return Panel.activeInHierarchy;
    }
  }

  public abstract class UIScreen<T> : UIScreen where T : UIScreen {
    static bool _firstShow;
    static bool _firstHide;

    public static T Instance { get; private set; }

    public static void DestroyScreen() {
      if (Instance) {
        // destroy screen
        Instance.OnScreenDestroy();

        // destroy
        Destroy(Instance.gameObject);

        // clear ref
        Instance = null;
      }
    }

    public static bool IsScreenVisible() {
      if (Instance) {
        return Instance.Panel.activeInHierarchy;
      }

      return false;
    }

    public void ShowScreenInstance() {
      ShowScreen();
    }

    public void HideScreenInstance() {
      HideScreen();
    }

    public void ToggleScreenInstance() {
      ToggleScreen();
    }

    public static void ShowScreen(bool condition) {
      if (condition != IsScreenVisible()) {
        if (condition) {
          ShowScreen();
        }
        else {
          HideScreen();
        }
      }
    }

    public static void ShowScreen() {
      if (Instance) {
        if (Instance.VerifyCanShow()) {
          Instance.Panel.Show();
          Instance.OnShowScreen(_firstShow);
          _firstShow = false;
        }
      }
    }

    public static void HideScreen() {
      if (Instance) {
        Instance.Panel.Hide();
        Instance.OnHideScreen(_firstHide);
        _firstHide = false;
      }
    }

    public static void ToggleScreen() {
      if (IsScreenVisible()) {
        HideScreen();
      }
      else {
        ShowScreen();
      }
    }

    public override void ResetScreenToStartState(bool cascade) {
      _firstShow = true;
      _firstHide = true;

      if (StartEnabled) {
        ShowScreen();
      }
      else {
        HideScreen();
      }

      if (cascade) {
        foreach (var screen in GetComponentsInChildren<UIScreen>()) {
          if (screen != this) {
            screen.ResetScreenToStartState(false);
          }
        }
      }
    }

    protected void Awake() {
      if (Instance) {
        // disable old instance
        Instance.gameObject.SetActive(false);

        // destroy old instance
        Destroy(Instance.gameObject);
      }

      // store instance
      Instance = (T)(object)this;

      // reset
      Instance.ResetScreenToStartState(false);
    }
  }
}