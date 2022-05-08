using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Quantum.Demo {
  [RequireComponent(typeof(Dropdown))]
  public class UIDropdownToggle : MonoBehaviour, IPointerClickHandler {
    public List<int> DisabledIndices = new List<int>();

    public void OnPointerClick(PointerEventData eventData) {
      var toggles = GetComponentsInChildren<Toggle>(true);
      for (var i = 2; i < toggles.Length; i++) {
        toggles[i].interactable = !DisabledIndices.Contains(i - 2);
      }
    }
  }
}