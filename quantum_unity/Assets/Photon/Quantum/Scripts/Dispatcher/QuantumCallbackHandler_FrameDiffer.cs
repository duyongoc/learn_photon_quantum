using Quantum;
using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;

public static class QuantumCallbackHandler_FrameDiffer {
  public static IDisposable Initialize() { 
    if ( Application.isEditor )
      return null;

    return QuantumCallback.SubscribeManual((CallbackChecksumErrorFrameDump c) => {
      var gameRunner = QuantumRunner.FindRunner(c.Game);
      if (gameRunner == null) {
        Debug.LogError("Could not find runner for game");
        return;
      }

      var differ = QuantumFrameDiffer.Show();
      differ.State.AddEntry(gameRunner.Id, c.ActorId, c.FrameNumber, c.FrameDump);
    }); 
  }
}
