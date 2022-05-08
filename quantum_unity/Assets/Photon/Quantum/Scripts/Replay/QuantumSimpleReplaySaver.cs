using System.IO;
using UnityEngine;

/// <summary>
///   Saves replays into user data using shortcuts.
///   Recording is no triggered here.
/// </summary>
public class QuantumSimpleReplaySaver : MonoBehaviour {
  public string Folderpath = "replays";
  private bool isSaving;

  public void Update() {
    if (this.isSaving)
      this.isSaving = this.IsTriggerValid();
    else if (this.IsTriggerValid()) {
      this.isSaving = true;
      this.Save();
    }
  }

  protected virtual bool IsTriggerValid() {
#if UNITY_EDITOR || UNITY_STANDALONE
    // Alt + R
    return (Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt)) && Input.GetKeyDown(KeyCode.R);
#elif UNITY_ANDROID || UNITY_IOS
    // Three touches
    return Input.touches.Length > 2;
#else 
    // Implement me for different platforms
    return false;
#endif
  }

  protected virtual void Save() {
    if (QuantumRunner.Default != null) {
//      var replayName = string.Format("replay_{0:yyyy-MM-dd--hh-mm-ss}", System.DateTime.Now);
//      var replayDirectory = Path.Combine(Application.persistentDataPath, (Path.Combine(this.Folderpath, replayName)));
//      if (!Directory.Exists(replayDirectory))
//        Directory.CreateDirectory(replayDirectory);
//
//      using (var serializer = new Quantum.JsonReplaySerializer()) {
//        using (var stream = File.Create(Path.Combine(replayDirectory, "replay.json")))
//          QuantumGame.ExportRecordedReplay(QuantumRunner.Default.Game, stream, serializer);
//
//        using (var stream = File.Create(Path.Combine(replayDirectory, "db.json")))
//          QuantumGame.ExportDatabase(UnityDB.DBInstance, stream, serializer, replayDirectory, MapDataBaker.NavMeshSerializationBufferSize);
//
//        if (QuantumRunner.Default.Game.RecordedChecksums != null) {
//          using (var stream = File.Create(Path.Combine(replayDirectory, "checksum.json")))
//            QuantumGame.ExportRecordedChecksums(QuantumRunner.Default.Game, stream, serializer);
//        }
//
//        Debug.Log("Saved replay to " + replayDirectory);
      //}
    }
  }
}