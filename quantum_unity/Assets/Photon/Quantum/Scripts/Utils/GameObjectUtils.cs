using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Quantum {
  public static class GameObjectUtils {
    public static void Show(this GameObject[] gameObjects) {
      if (gameObjects != null) {
        for (int i = 0; i < gameObjects.Length; ++i) {
          gameObjects[i].SetActive(true);
        }
      }
    }

    public static void Hide(this GameObject[] gameObjects) {
      if (gameObjects != null) {
        for (int i = 0; i < gameObjects.Length; ++i) {
          gameObjects[i].SetActive(false);
        }
      }
    }

    public static void Show(this GameObject gameObject) {
      if (gameObject && !gameObject.activeSelf) {
        gameObject.SetActive(true);
      }
    }

    public static void Hide(this GameObject gameObject) {
      if (gameObject && gameObject.activeSelf) {
        gameObject.SetActive(false);
      }
    }

    public static bool Toggle(this GameObject gameObject) {
      if (gameObject) {
        return gameObject.Toggle(!gameObject.activeSelf);
      }

      return false;
    }

    public static bool Toggle(this GameObject gameObject, bool state) {
      if (gameObject) {
        if (gameObject.activeSelf != state) {
          gameObject.SetActive(state);
        }

        return state;
      }

      return false;
    }

    public static bool Toggle(this Component component, bool state) {
      if (component) {
        return component.gameObject.Toggle(state);
      }

      return false;
    }

    public static void Show(this Component component) {
      if (component) {
        component.gameObject.Show();
      }
    }

    public static void Show(this UnityEngine.UI.Image component, Sprite sprite) {
      if (component) {
        component.sprite = sprite;
        component.gameObject.SetActive(true);
      }
    }

    public static void Hide(this Component component) {
      if (component) {
        component.gameObject.Hide();
      }
    }


    public static void Show<T>(this T[] gameObjects) where T : Component {
      if (gameObjects != null) {
        for (int i = 0; i < gameObjects.Length; ++i) {
          if (gameObjects[i].gameObject.activeSelf == false) {
            gameObjects[i].gameObject.SetActive(true);
          }
        }
      }
    }

    public static void Hide<T>(this T[] gameObjects) where T : Component {
      if (gameObjects != null) {
        for (int i = 0; i < gameObjects.Length; ++i) {
          if (gameObjects[i].gameObject.activeSelf) {
            gameObjects[i].gameObject.SetActive(false);
          }
        }
      }
    }
  }
}