using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;

namespace Quantum {
  public class ChecksumVerification : IDisposable {
    private Dictionary<int, ChecksumFile.ChecksumEntry> _checksums;
    private Quantum.CallbackDispatcher _gameCallbacks;
    private bool _verbose;

    public ChecksumVerification(string pathToChecksumFile, Quantum.CallbackDispatcher callbacks, bool verbose = false) {
      _checksums = JsonConvert.DeserializeObject<ChecksumFile>(File.ReadAllText(pathToChecksumFile), ReplayJsonSerializerSettings.GetSettings()).ToDictionary();
      _gameCallbacks = callbacks;
      _gameCallbacks.Subscribe(this, (CallbackSimulateFinished callback) => OnSimulateFinished(callback.Game, callback.Frame));
      _verbose = verbose;
    }

    private void OnSimulateFinished(QuantumGame game, Frame frame) {
      if (frame != null) {
        var f = frame.Number;
        var cs = ChecksumFileHelper.UlongToLong(frame.CalculateChecksum());

        if (_checksums != null) {

          if (_checksums.ContainsKey(f)) {
            Console.Write($"{f,6} {cs,25} ");
            if (cs != _checksums[f].ChecksumAsLong) {
              Console.ForegroundColor = ConsoleColor.Red;
              Console.Write("(failed)");
            } else {
              Console.ForegroundColor = ConsoleColor.Green;
              Console.Write("(verified)");
            }
            Console.Write("\n");
          } else if (_verbose) {
            Console.Write($"{f,6} {cs,25} ");
            Console.Write("(skipped)");
          }
          Console.ForegroundColor = ConsoleColor.Gray;
        }
      }
    }

    public void Dispose() {
      if (_gameCallbacks != null) {
        _gameCallbacks.UnsubscribeListener(this);
        _gameCallbacks = null;
      }
    }
  }
}
