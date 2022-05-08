using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Quantum {

  public class QuantumJsonSerializer : Quantum.JsonAssetSerializerBase {
    private readonly JsonSerializer _serializer = CreateSerializer();

    public static JsonSerializer CreateSerializer() {
      return JsonSerializer.Create(CreateSettings());
    }

    protected override object FromJson(string json, Type type) {
      using (var reader = new StringReader(json)) {
        var result = _serializer.Deserialize(reader, type);
        return result;
      }
    }

    protected override string ToJson(object obj) {
      using (var writer = new StringWriter()) {
        _serializer.Serialize(writer, obj);
        return writer.ToString();
      }
    }

    private static JsonSerializerSettings CreateSettings() {
      return new JsonSerializerSettings {
        ContractResolver = new WritablePropertiesOnlyResolver(),
        Formatting = Formatting.Indented,
        NullValueHandling = NullValueHandling.Ignore,
        Converters = { new ByteArrayConverter() },
      };
    }
    private class ByteArrayConverter : JsonConverter {

      public override bool CanConvert(Type objectType) {
        return objectType == typeof(byte[]);
      }

      public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer) {
        if (reader.TokenType == JsonToken.StartArray) {
          var byteList = new List<byte>();

          while (reader.Read()) {
            switch (reader.TokenType) {
              case JsonToken.Integer:
                byteList.Add(Convert.ToByte(reader.Value));
                break;

              case JsonToken.EndArray:
                return byteList.ToArray();

              case JsonToken.Comment:
                // skip
                break;

              default:
                throw new Exception(string.Format("Unexpected token when reading bytes: {0}", reader.TokenType));
            }
          }

          throw new Exception("Unexpected end when reading bytes.");
        } else {
          throw new Exception(string.Format("Unexpected token parsing binary. Expected StartArray, got {0}.", reader.TokenType));
        }
      }

      public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer) {
        if (value == null) {
          writer.WriteNull();
          return;
        }

        byte[] data = (byte[])value;

        // compose an array
        writer.WriteStartArray();

        for (var i = 0; i < data.Length; i++) {
          writer.WriteValue(data[i]);
        }

        writer.WriteEndArray();
      }
    }

    private class WritablePropertiesOnlyResolver : DefaultContractResolver {

      protected override IList<JsonProperty> CreateProperties(Type type, MemberSerialization memberSerialization) {
        IList<JsonProperty> props = base.CreateProperties(type, memberSerialization);
        return props.Where(p => p.Writable).ToList();
      }

      protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization memberSerialization) {
        if (member is FieldInfo) {
          // just fields
          return base.CreateProperty(member, memberSerialization);
        } else {
          return null;
        }
      }
    }
  }
}