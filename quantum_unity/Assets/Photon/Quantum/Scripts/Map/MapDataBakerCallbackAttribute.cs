using System;

public class MapDataBakerCallbackAttribute : Attribute {

  public int InvokeOrder { get; private set; }

  public MapDataBakerCallbackAttribute(int invokeOrder) {
    InvokeOrder = invokeOrder;
  }
}
