using System;
using Photon.Deterministic;
using Quantum;
using UnityEngine;

public class LocalInput : MonoBehaviour
{

    private void OnEnable()
    {
        QuantumCallback.Subscribe(this, (CallbackPollInput callback) => PollInput(callback));
    }

    public void PollInput(CallbackPollInput callback)
    {
        Quantum.Input i = new Quantum.Input();

        i.moveHorizontal = FP.FromFloat_UNSAFE(UnityEngine.Input.GetAxis("Horizontal"));
        i.moveVertical = FP.FromFloat_UNSAFE(UnityEngine.Input.GetAxis("Vertical"));

        i.Attack = UnityEngine.Input.GetMouseButtonDown(0);

        //Debug.Log($"i.moveHorizontal {i.moveHorizontal} / i.moveVertical {i.moveVertical}");

        callback.SetInput(i, DeterministicInputFlags.Repeatable);
    }
}
