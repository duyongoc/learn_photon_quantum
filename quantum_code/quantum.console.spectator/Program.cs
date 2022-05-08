using Photon.Deterministic;
using Photon.Realtime;
using Quantum;
using Quantum.Spectator;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Xml;

public class Program {
  public static async Task Main(string[] args) {
    DeterministicLog.InitForConsole();
    Log.InitForConsole();
    PhotonRealtimeAsync.Log.InitForSystemDiagnostics();

    // Realtime logs to System.Diagnostics.Debug as well
    Trace.Listeners.Add(new TextWriterTraceListener(System.Console.Out));

    // get configs from json files
    // check out App.config for asset paths
    // check out Unity menu > Quantum > Demo > Export Spectator Assets
    var runtimeConfig = LoadJsonResource<RuntimeConfig>(ConfigurationManager.AppSettings["RuntimeConfig"]);
    var sessionConfig = LoadJsonResource<DeterministicSessionConfig>(ConfigurationManager.AppSettings["SessionConfig"]);

    // Photon connection and room settings
    var appSettings = LoadJsonResource<AppSettings>(ConfigurationManager.AppSettings["AppSettings"]);
    if (string.IsNullOrEmpty(appSettings.AppIdRealtime)) {
      Log.Error("No AppId set");
      return;
    }

    var enterRoomParams = default(EnterRoomParams);
    using (var reader = XmlReader.Create(ConfigurationManager.AppSettings["EnterRoomParams"]))
      enterRoomParams = SerializableEnterRoomParams.Deserialize(reader);

    // setup lut provider
    LutProvider lutProvider = path => File.ReadAllBytes($"{ConfigurationManager.AppSettings["LutFolder"]}/{path}.bytes");

    // get asset db binary data
    var assetDb = File.ReadAllBytes(ConfigurationManager.AppSettings["AssetDB"]);

    var spectator = new Spectator();
    await spectator.Run(runtimeConfig, sessionConfig, appSettings, enterRoomParams, lutProvider, assetDb, Spectator.StartParameter.Default);
  }

  private static T LoadJsonResource<T>(string path) {
    var resource = default(T);
    using (TextReader reader = File.OpenText(path)) {
      resource = (T)QuantumJsonSerializer.CreateSerializer().Deserialize(reader, typeof(T));
    }
    return resource;
  }
}