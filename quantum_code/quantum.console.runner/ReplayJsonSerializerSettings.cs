using Newtonsoft.Json;

namespace Quantum {
  public static class ReplayJsonSerializerSettings {
    public static JsonSerializerSettings GetSettings() {
      return new JsonSerializerSettings {
        Formatting = Formatting.Indented,
        NullValueHandling = NullValueHandling.Ignore,
      };
    }
  }
}