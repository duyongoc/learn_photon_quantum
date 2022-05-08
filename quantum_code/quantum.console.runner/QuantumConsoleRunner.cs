using System;
using System.IO;

namespace Quantum {
  class QuantumConsoleRunner {
    static void Main(string[] args) {

      Log.InitForConsole();

      var pathToLUT = Path.GetFullPath(args[0]);
      var pathToDatabaseFile = Path.GetFullPath(args[1]);
      var pathToReplayFile = Path.GetFullPath(args[2]);
      var pathToChecksumFile = args.Length > 3 ? Path.GetFullPath(args[3]) : null;
      var maxIterations = args.Length > 4 ? long.Parse(args[4]) : 1;

      // Demonstration of a sample runner. Please duplicate the ReplayRunnerSample class to modify, because it may get overwritten in the future.
      long iteration = 0;
      while (iteration < maxIterations && ReplayRunnerSample.Run(pathToLUT, pathToDatabaseFile, pathToReplayFile, pathToChecksumFile)) {
        if (++iteration < maxIterations) {
          Console.ForegroundColor = ConsoleColor.Blue;
          Console.WriteLine($"Iteration {iteration + 1}");
          Console.ForegroundColor = ConsoleColor.Gray;
        }
      }

      //Console.ReadKey();
    }
  }
}
