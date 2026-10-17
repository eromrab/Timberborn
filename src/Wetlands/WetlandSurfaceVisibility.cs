using Timberborn.BlockSystem;
using Timberborn.Buildings;
using Timberborn.LevelVisibilitySystem;
using UnityEngine;

namespace Wetlands
{
  internal static class WetlandSurfaceVisibility
  {
    public static bool ShouldShowPlants(
      Vector3Int coordinates,
      IBlockService blockService,
      ILevelVisibilityService levelVisibilityService)
    {
      Vector3Int surface = coordinates + Vector3Int.forward;
      if (!levelVisibilityService.BlockIsVisible(surface))
      {
        return false;
      }
      if (blockService.GetPathObjectAt(surface) != null)
      {
        return false;
      }
      return !HasBuilding(blockService, surface);
    }

    private static bool HasBuilding(IBlockService blockService, Vector3Int surface)
    {
      foreach (BlockObject blockObject in blockService.GetObjectsAt(surface))
      {
        if (blockObject.HasComponent<BuildingSpec>())
        {
          return true;
        }
      }
      return false;
    }
  }
}
