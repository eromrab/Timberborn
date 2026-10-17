using System.Collections.Generic;
using Timberborn.MapIndexSystem;
using Timberborn.Persistence;
using Timberborn.SingletonSystem;
using Timberborn.TerrainSystem;
using Timberborn.TickSystem;
using Timberborn.WaterSystem;
using Timberborn.WorldPersistence;
using UnityEngine;

namespace Wetlands
{
  // Wetland dirt meshes keep the grass shader and cliff blend. Only their
  // color texture is swapped: marshy green and mud when wet, cracked dirt
  // when dry. Cattails hide under paths and buildings.
  public class WetlandService : ILoadableSingleton, IPostLoadableSingleton, IUnloadableSingleton, ITickableSingleton, ISaveableSingleton
  {
    private static readonly Vector3Int[] Around =
    {
      new Vector3Int(0, 0, 0),
      new Vector3Int(1, 0, 0),
      new Vector3Int(-1, 0, 0),
      new Vector3Int(0, 1, 0),
      new Vector3Int(0, -1, 0),
      new Vector3Int(1, 1, 0),
      new Vector3Int(1, -1, 0),
      new Vector3Int(-1, 1, 0),
      new Vector3Int(-1, -1, 0)
    };

    private static readonly SingletonKey SingletonKey = new SingletonKey("Wetlands.Tiles");

    private static readonly ListKey<Vector3Int> TilesKey = new ListKey<Vector3Int>("Tiles");

    private static WetlandService _instance;

    private readonly IThreadSafeWaterMap _threadSafeWaterMap;

    private readonly ITerrainService _terrainService;

    private readonly MapIndexService _mapIndexService;

    private readonly ISingletonLoader _singletonLoader;

    private readonly Vector3IntSerializer _vector3IntSerializer;

    private readonly WetlandCattailRenderer _cattailRenderer;

    private readonly HashSet<Vector3Int> _tiles = new HashSet<Vector3Int>();

    private HashSet<Vector3Int> _wetCoordinates = new HashSet<Vector3Int>();

    private HashSet<int> _moisturizingIndex3D = new HashSet<int>();

    private HashSet<int> _moisturizingIndex2D = new HashSet<int>();

    public WetlandService(
      IThreadSafeWaterMap threadSafeWaterMap,
      ITerrainService terrainService,
      MapIndexService mapIndexService,
      ISingletonLoader singletonLoader,
      Vector3IntSerializer vector3IntSerializer,
      WetlandCattailRenderer cattailRenderer)
    {
      _threadSafeWaterMap = threadSafeWaterMap;
      _terrainService = terrainService;
      _mapIndexService = mapIndexService;
      _singletonLoader = singletonLoader;
      _vector3IntSerializer = vector3IntSerializer;
      _cattailRenderer = cattailRenderer;
      _instance = this;
    }

    public void Load()
    {
      _instance = this;
      _terrainService.TerrainHeightChanged += OnTerrainHeightChanged;
      if (!_singletonLoader.TryGetSingleton(SingletonKey, out IObjectLoader loader))
      {
        return;
      }
      foreach (Vector3Int coordinates in loader.Get(TilesKey, _vector3IntSerializer))
      {
        _tiles.Add(coordinates);
      }
    }

    public void PostLoad()
    {
      foreach (Vector3Int coordinates in _tiles)
      {
        UnsetField(coordinates);
      }
      RefreshVisuals();
      TerrainMeshInvalidator.Invalidate(_tiles);
    }

    public void Unload()
    {
      _terrainService.TerrainHeightChanged -= OnTerrainHeightChanged;
      if (_instance == this)
      {
        _instance = null;
      }
      _tiles.Clear();
      _wetCoordinates = new HashSet<Vector3Int>();
      _moisturizingIndex3D = new HashSet<int>();
      _moisturizingIndex2D = new HashSet<int>();
      _cattailRenderer.Unload();
    }

    public void Save(ISingletonSaver singletonSaver)
    {
      singletonSaver.GetSingleton(SingletonKey).Set(TilesKey, _tiles, _vector3IntSerializer);
    }

    public void AddTile(Vector3Int coordinates)
    {
      _tiles.Add(coordinates);
      UnsetField(coordinates);
      TerrainMeshInvalidator.Invalidate(new[] { coordinates });
      RefreshVisuals();
    }

    public bool HasTileAt(Vector3Int coordinates)
    {
      return _tiles.Contains(coordinates)
        || _tiles.Contains(coordinates + Vector3Int.back);
    }

    public bool IsWetAt(Vector3Int coordinates)
    {
      return _wetCoordinates.Contains(coordinates)
        || _wetCoordinates.Contains(coordinates + Vector3Int.back);
    }

    internal static bool HasTileAtIndex(Vector3Int coordinates)
    {
      return _instance != null && _instance.HasTileAt(coordinates);
    }

    internal static bool IsWetAtIndex(Vector3Int coordinates)
    {
      return _instance != null && _instance.IsWetAt(coordinates);
    }

    internal static bool CoversWetland(Vector3Int coordinates)
    {
      return HasTileAtIndex(coordinates)
        || HasTileAtIndex(coordinates + new Vector3Int(1, 0, 0))
        || HasTileAtIndex(coordinates + new Vector3Int(0, 1, 0))
        || HasTileAtIndex(coordinates + new Vector3Int(1, 1, 0));
    }

    internal static bool IsMoisturizingIndex3D(int index3D)
    {
      HashSet<int> set = _instance?._moisturizingIndex3D;
      return set != null && set.Contains(index3D);
    }

    internal static bool IsMoisturizingIndex2D(int index2D)
    {
      HashSet<int> set = _instance?._moisturizingIndex2D;
      return set != null && set.Contains(index2D);
    }

    public void Tick()
    {
      if (_tiles.Count == 0)
      {
        return;
      }
      HashSet<Vector3Int> nextWet = new HashSet<Vector3Int>();
      HashSet<Vector3Int> visited = new HashSet<Vector3Int>();
      foreach (Vector3Int start in _tiles)
      {
        if (!visited.Add(start))
        {
          continue;
        }
        List<Vector3Int> group = CollectGroup(start, visited);
        if (GroupHasWaterContact(group))
        {
          for (int i = 0; i < group.Count; i++)
          {
            nextWet.Add(group[i]);
          }
        }
      }
      HashSet<int> nextIndex3D = new HashSet<int>();
      HashSet<int> nextIndex2D = new HashSet<int>();
      foreach (Vector3Int coordinates in nextWet)
      {
        nextIndex3D.Add(_mapIndexService.CoordinatesToIndex3D(coordinates));
        nextIndex3D.Add(_mapIndexService.CoordinatesToIndex3D(coordinates + Vector3Int.forward));
        nextIndex2D.Add(_mapIndexService.CellToIndex(new Vector2Int(coordinates.x, coordinates.y)));
      }
      bool wetChanged = !_wetCoordinates.SetEquals(nextWet);
      _wetCoordinates = nextWet;
      _moisturizingIndex3D = nextIndex3D;
      _moisturizingIndex2D = nextIndex2D;
      if (wetChanged)
      {
        TerrainMeshInvalidator.Invalidate(_tiles);
      }
      RefreshVisuals();
    }

    private List<Vector3Int> CollectGroup(Vector3Int start, HashSet<Vector3Int> visited)
    {
      List<Vector3Int> group = new List<Vector3Int> { start };
      Queue<Vector3Int> queue = new Queue<Vector3Int>();
      queue.Enqueue(start);
      while (queue.Count > 0)
      {
        Vector3Int current = queue.Dequeue();
        for (int n = 1; n <= 4; n++)
        {
          Vector3Int neighbor = current + Around[n];
          if (_tiles.Contains(neighbor) && visited.Add(neighbor))
          {
            group.Add(neighbor);
            queue.Enqueue(neighbor);
          }
        }
      }
      return group;
    }

    private bool GroupHasWaterContact(List<Vector3Int> group)
    {
      for (int i = 0; i < group.Count; i++)
      {
        Vector3Int coordinates = group[i];
        if (CellHasWaterNearby(coordinates))
        {
          return true;
        }
        for (int n = 0; n < Around.Length; n++)
        {
          Vector3Int neighbor = coordinates + Around[n];
          if (_tiles.Contains(neighbor))
          {
            continue;
          }
          if (ColumnHasWater(neighbor))
          {
            return true;
          }
        }
      }
      return false;
    }

    private void UnsetField(Vector3Int coordinates)
    {
      UnsetFieldIfValid(coordinates);
      UnsetFieldIfValid(coordinates + Vector3Int.forward);
    }

    private void UnsetFieldIfValid(Vector3Int coordinates)
    {
      if (_terrainService.Contains(coordinates))
      {
        _terrainService.UnsetField(coordinates);
      }
    }

    private bool CellHasWaterNearby(Vector3Int coordinates)
    {
      return ColumnHasWater(coordinates);
    }

    private bool ColumnHasWater(Vector3Int coordinates)
    {
      if (!_terrainService.Contains(new Vector2Int(coordinates.x, coordinates.y)))
      {
        return false;
      }
      return _threadSafeWaterMap.IsWaterOnAnyHeight(new Vector2Int(coordinates.x, coordinates.y));
    }

    private void OnTerrainHeightChanged(object sender, TerrainHeightChangeEventArgs args)
    {
      TerrainHeightChange change = args.Change;
      if (change.SetTerrain)
      {
        return;
      }
      bool removed = false;
      for (int z = change.From; z <= change.To; z++)
      {
        Vector3Int coordinates = new Vector3Int(change.Coordinates.x, change.Coordinates.y, z);
        if (_tiles.Remove(coordinates))
        {
          UnsetField(coordinates);
          TerrainMeshInvalidator.Invalidate(new[] { coordinates });
          removed = true;
        }
      }
      if (removed)
      {
        RefreshVisuals();
      }
    }

    private void RefreshVisuals()
    {
      _cattailRenderer.Refresh(_tiles);
    }
  }
}
