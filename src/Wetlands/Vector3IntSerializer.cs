using Timberborn.Persistence;
using UnityEngine;

namespace Wetlands
{
  public class Vector3IntSerializer : IValueSerializer<Vector3Int>
  {
    private static readonly PropertyKey<int> XKey = new PropertyKey<int>("X");

    private static readonly PropertyKey<int> YKey = new PropertyKey<int>("Y");

    private static readonly PropertyKey<int> ZKey = new PropertyKey<int>("Z");

    public void Serialize(Vector3Int value, IValueSaver valueSaver)
    {
      IObjectSaver saver = valueSaver.AsObject();
      saver.Set(XKey, value.x);
      saver.Set(YKey, value.y);
      saver.Set(ZKey, value.z);
    }

    public Obsoletable<Vector3Int> Deserialize(IValueLoader valueLoader)
    {
      IObjectLoader loader = valueLoader.AsObject();
      return new Vector3Int(loader.Get(XKey), loader.Get(YKey), loader.Get(ZKey));
    }
  }
}
