using System;
using Timberborn.SingletonSystem;
using Timberborn.TickSystem;
using Timberborn.WaterSystemRendering;

namespace Tidetails
{
  internal class TidetailsWetPathService : ITickableSingleton, ILoadableSingleton, IUnloadableSingleton
  {
    private const int PathsCheckedPerTick = 20;

    private readonly EventBus _eventBus;

    private readonly System.Collections.Generic.List<TidetailsWetPath> _paths = new System.Collections.Generic.List<TidetailsWetPath>();

    private int _nextIndex;

    public TidetailsWetPathService(EventBus eventBus)
    {
      _eventBus = eventBus;
    }

    public void Load()
    {
      _eventBus.Register(this);
    }

    public void Unload()
    {
      _eventBus.Unregister(this);
    }

    public void Register(TidetailsWetPath path)
    {
      _paths.Add(path);
    }

    public void Unregister(TidetailsWetPath path)
    {
      _paths.Remove(path);
    }

    public void Tick()
    {
      if (!TidetailsFactionState.IsActive || _paths.Count == 0)
      {
        return;
      }
      int count = Math.Min(PathsCheckedPerTick, _paths.Count);
      for (int i = 0; i < count; i++)
      {
        if (_nextIndex >= _paths.Count)
        {
          _nextIndex = 0;
        }
        _paths[_nextIndex++].RefreshIfChanged();
      }
    }

    [OnEvent]
    public void OnWaterOpacityChanged(WaterOpacityChangedEvent waterOpacityChangedEvent)
    {
      if (!TidetailsFactionState.IsActive)
      {
        return;
      }
      for (int i = 0; i < _paths.Count; i++)
      {
        _paths[i].RefreshAppearance();
      }
    }
  }
}
