using System.Threading.Tasks;

namespace PhotonRealtimeAsync {
  public static class Globals {
    public const int ServiceIntervalMs = 10;
    public const float OperationTimeoutSec = 15.0f;

    private static TaskFactory _taskFactory = Task.Factory;

    public static TaskFactory TaskFactory {
      get { return _taskFactory; }
      set { _taskFactory = value; }
    }

    public static TaskScheduler TaskScheduler {
      get {
        if (_taskFactory.Scheduler != null) {
          return _taskFactory.Scheduler;
        }

        return TaskScheduler.Default;
      }
    }
  }
}
