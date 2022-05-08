using System.Threading;
using System.Threading.Tasks;

namespace PhotonRealtimeAsync {
  public static class AsyncSetup {
    public static void ForUnity() {
      Globals.TaskFactory = CreateUnityTaskFactory();
#if UNITY_5_3_OR_NEWER
      Log.InitForUnity();
#endif
    }

    public static TaskFactory CreateUnityTaskFactory() {
      return new TaskFactory(
        CancellationToken.None,
        TaskCreationOptions.DenyChildAttach,
        TaskContinuationOptions.DenyChildAttach | TaskContinuationOptions.ExecuteSynchronously,
        TaskScheduler.FromCurrentSynchronizationContext());
    }
  }
}
