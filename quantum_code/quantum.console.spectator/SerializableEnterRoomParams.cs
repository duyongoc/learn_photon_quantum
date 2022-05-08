using Photon.Realtime;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Xml;
using System.Xml.Serialization;

#if UNITY_5_3_OR_NEWER
    using Hashtable = ExitGames.Client.Photon.Hashtable;
#endif

[Serializable]
public class SerializableEnterRoomParams : EnterRoomParams {
  private List<DictionaryEntry> _playerProperties = new List<DictionaryEntry>();
  public List<DictionaryEntry> _customRoomProperties = new List<DictionaryEntry>();

  public static void Serialize(XmlWriter writer, SerializableEnterRoomParams obj) {
    if (obj.RoomOptions != null && obj.RoomOptions.CustomRoomProperties != null) {
      foreach (DictionaryEntry e in obj.RoomOptions.CustomRoomProperties) {
        obj._customRoomProperties.Add(e);
      }
    }

    if (obj.PlayerProperties != null) {
      foreach (DictionaryEntry e in obj.PlayerProperties) {
        obj._playerProperties.Add(e);
      }
    }

    CreateSerializer().Serialize(writer, obj);
  }

  public static SerializableEnterRoomParams Deserialize(XmlReader reader) {
    var obj = (SerializableEnterRoomParams)CreateSerializer().Deserialize(reader);

    if (obj._customRoomProperties != null && obj._customRoomProperties.Count > 0) {
      if (obj.RoomOptions == null) {
        obj.RoomOptions = new RoomOptions();
      }

      if (obj.RoomOptions.CustomRoomProperties == null) {
        obj.RoomOptions.CustomRoomProperties = new Hashtable();
      }

      foreach (DictionaryEntry e in obj._customRoomProperties) {
        obj.RoomOptions.CustomRoomProperties.Add(e.Key, e.Value);
      }
    }

    if (obj._playerProperties != null && obj._playerProperties.Count > 0) {
      if (obj.PlayerProperties == null) {
        obj.PlayerProperties = new Hashtable();
      }

      foreach (DictionaryEntry e in obj._customRoomProperties) {
        obj.RoomOptions.CustomRoomProperties.Add(e.Key, e.Value);
      }
    }

    return obj;
  }

  public static XmlSerializer CreateSerializer() {
    var overrides = new XmlAttributeOverrides();
    var attribs = new XmlAttributes() { XmlIgnore = true };
    overrides.Add(typeof(EnterRoomParams), "PlayerProperties", attribs);
    overrides.Add(typeof(RoomOptions), "CustomRoomProperties", attribs);
    return new XmlSerializer(typeof(SerializableEnterRoomParams), overrides);
  }
}