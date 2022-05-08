using System;
using System.Runtime.CompilerServices;
using UnityEngine;
using UI = UnityEngine.UI;

namespace Quantum.Demo {
  public class UIDialog : UIScreen<UIDialog> {
    public UI.Text Headline;
    public UI.Text Text;

    private Action _onHideDialogAction;

    public override void OnHideScreen(bool first) {
      base.OnHideScreen(first);

      _onHideDialogAction?.Invoke();
      _onHideDialogAction = null;
    }

    public static void Show(string headline, String text, Action hideDialogAction = null) {
      if (IsScreenVisible()) {
        return;
      }

      Debug.LogFormat($"UIDialog: '{text}'");

      // set text
      Instance.Headline.text = headline;
      Instance.Text.text = text;

      // show screen
      ShowScreen();

      Instance._onHideDialogAction = hideDialogAction;
    }
  }
}