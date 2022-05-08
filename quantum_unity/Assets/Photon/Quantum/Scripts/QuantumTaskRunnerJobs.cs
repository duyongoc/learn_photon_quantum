using System;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Profiling;

public class QuantumTaskRunnerJobs : MonoBehaviour, Photon.Deterministic.IDeterministicPlatformTaskRunner {
  struct ActionJob : IJob {
    public int Index;

    public void Execute() {
      _delegates[Index]();
    }
  }

  static Action[] _delegates;

  public static QuantumTaskRunnerJobs GetInstance() {
    var instance = FindObjectOfType<QuantumTaskRunnerJobs>();
    if (instance) {
      return instance;
    }

    var go = new GameObject(nameof(QuantumTaskRunnerJobs));
    UnityEngine.Object.DontDestroyOnLoad(go);
    return go.AddComponent<QuantumTaskRunnerJobs>();
  }


  NativeArray<JobHandle> _handles;
  ActionJob[]            _jobs;

  public void Schedule(Action[] delegates) {
    Profiler.BeginSample("Schedule");
    _delegates = delegates;

    
    Profiler.BeginSample("Array Creation");
    if (_jobs == null || _jobs.Length != delegates.Length) {
      _jobs = new ActionJob[delegates.Length];

      if (_handles.IsCreated) {
        _handles.Dispose();
        _handles = default;
      }

      _handles = new NativeArray<JobHandle>(delegates.Length, Allocator.Persistent);
    }
    Profiler.EndSample();

    Profiler.BeginSample("Job Scheduling");
    for (int i = 0; i < delegates.Length; ++i) {
      // create job
      _jobs[i] = new ActionJob {
        Index = i,
      };

      // schedule it
      _handles[i] = _jobs[i].Schedule();
    }
    Profiler.EndSample();
    
    Profiler.BeginSample("JobHandle.ScheduleBatchedJobs");
    JobHandle.ScheduleBatchedJobs();
    Profiler.EndSample();
    
    Profiler.EndSample();
  }

  public bool PollForComplete() {
    if (_handles.IsCreated) {
      for (int i = 0; i < _handles.Length; ++i) {
        if (_handles[i].IsCompleted == false) {
          return false;
        }
      }
    }

    return true;
  }

  public void WaitForComplete() {
    JobHandle.CompleteAll(_handles);
  }

  void OnDestroy() {
    if (_handles.IsCreated) {
      _handles.Dispose();
      _handles = default;
    }
  }
}