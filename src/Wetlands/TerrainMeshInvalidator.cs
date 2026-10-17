using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Timberborn.TerrainSystem;
using UnityEngine;

namespace Wetlands
{
  internal static class TerrainMeshInvalidator
  {
    private static object _manager;

    private static MethodInfo _updateTile;

    public static void Capture(object manager)
    {
      _manager = manager;
      _updateTile = AccessTools.Method(manager.GetType(), "UpdateTile");
    }

    public static void Invalidate(IEnumerable<Vector3Int> cells)
    {
      if (_manager == null || _updateTile == null || cells == null)
      {
        return;
      }
      HashSet<Vector3Int> tiles = new HashSet<Vector3Int>();
      foreach (Vector3Int cell in cells)
      {
        tiles.Add(WorldTiling.CoordinatesToTileIndex3D(cell));
        tiles.Add(WorldTiling.CoordinatesToTileIndex3D(cell + Vector3Int.forward));
        tiles.Add(WorldTiling.CoordinatesToTileIndex3D(cell + new Vector3Int(1, 0, 0)));
        tiles.Add(WorldTiling.CoordinatesToTileIndex3D(cell + new Vector3Int(0, 1, 0)));
        tiles.Add(WorldTiling.CoordinatesToTileIndex3D(cell + new Vector3Int(1, 1, 0)));
        tiles.Add(WorldTiling.CoordinatesToTileIndex3D(cell + new Vector3Int(-1, 0, 0)));
        tiles.Add(WorldTiling.CoordinatesToTileIndex3D(cell + new Vector3Int(0, -1, 0)));
        tiles.Add(WorldTiling.CoordinatesToTileIndex3D(cell + new Vector3Int(-1, -1, 0)));
        tiles.Add(WorldTiling.CoordinatesToTileIndex3D(cell + new Vector3Int(1, -1, 0)));
        tiles.Add(WorldTiling.CoordinatesToTileIndex3D(cell + new Vector3Int(-1, 1, 0)));
      }
      foreach (Vector3Int tile in tiles)
      {
        try
        {
          _updateTile.Invoke(_manager, new object[] { tile });
        }
        catch
        {
        }
      }
    }
  }
}
