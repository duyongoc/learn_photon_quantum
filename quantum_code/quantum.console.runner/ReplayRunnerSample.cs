using Photon.Deterministic;
using System;
using System.IO;
using System.Threading;

namespace Quantum {
  public class ReplayRunnerSample {

    public static bool Run(string pathToLUT,string pathToDatabaseFile, string pathToReplayFile, string pathToChecksumFile) {

      FPLut.Init(pathToLUT);

      Console.WriteLine($"Loading replay from file: '{Path.GetFileName(pathToReplayFile)}' from folder '{Path.GetDirectoryName(pathToReplayFile)}'");

      if (!File.Exists(pathToDatabaseFile)) {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"File not found: '{pathToReplayFile}'");
        Console.ForegroundColor = ConsoleColor.Gray;
        return false;
      }

      var serializer = new QuantumJsonSerializer();
      var callbackDispatcher = new CallbackDispatcher();
      var replayFile = serializer.DeserializeReplay(File.ReadAllBytes(pathToReplayFile));
      var inputProvider = new InputProvider(replayFile.DeterministicConfig);
      inputProvider.ImportFromList(replayFile.InputHistory);

      var resourceManager = new ResourceManagerStatic(serializer.DeserializeAssets(File.ReadAllBytes(pathToDatabaseFile)), SessionContainer.CreateNativeAllocator());

      var container = new SessionContainer(replayFile);
      container.StartReplay(new QuantumGame.StartParameters {
        AssetSerializer = serializer,
        CallbackDispatcher = callbackDispatcher,
        EventDispatcher = null,
        ResourceManager = resourceManager,
      }, inputProvider);

      var numberOfFrames = replayFile.Length;
      var checksumVerification = String.IsNullOrEmpty(pathToChecksumFile) ? null : new ChecksumVerification(pathToChecksumFile, callbackDispatcher);

      while (container.Session.FramePredicted == null || container.Session.FramePredicted.Number < numberOfFrames) {
        Thread.Sleep(1);
        container.Service(dt: 1.0f);

        if (Console.KeyAvailable) {
          if (Console.ReadKey().Key == ConsoleKey.Escape) {
            Console.WriteLine("Stopping replay");
            return false;
          }
        }
      }

      Console.WriteLine($"Ending replay at frame {container.Session.FramePredicted.Number}");

      checksumVerification?.Dispose();
      container.Destroy();

      resourceManager.Dispose();

      return true;
    }
  }
}
