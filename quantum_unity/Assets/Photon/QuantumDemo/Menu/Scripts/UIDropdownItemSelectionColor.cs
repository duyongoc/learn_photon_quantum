using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Quantum.Demo {
  public class UIDropdownItemSelectionColor : MonoBehaviour, ISelectHandler, IDeselectHandler {
    public Color TextDefault;
    public Color TextSelected;
    public Text Text;

    void ISelectHandler.OnSelect(BaseEventData eventData) {
      Text.color = TextSelected;
    }

    public void OnDeselect(BaseEventData eventData) {
      Text.color = TextDefault;
    }
  }
}