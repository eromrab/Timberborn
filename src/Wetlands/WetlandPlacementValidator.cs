using Timberborn.BlockSystem;
using Timberborn.Localization;
using Timberborn.WaterSystem;
using UnityEngine;

namespace Wetlands
{
  internal class WetlandPlacementValidator : IBlockObjectValidator
  {
    private static readonly string DryLandOnlyLocKey = "Wetlands.Preview.DryLandOnly";

    private readonly ILoc _loc;

    private readonly IThreadSafeWaterMap _threadSafeWaterMap;

    public WetlandPlacementValidator(ILoc loc, IThreadSafeWaterMap threadSafeWaterMap)
    {
      _loc = loc;
      _threadSafeWaterMap = threadSafeWaterMap;
    }

    public bool IsValid(BlockObject blockObject, out string errorMessage)
    {
      errorMessage = null;
      if (!blockObject.IsPreview || !blockObject.HasComponent<DryLandOnlyPlacementSpec>())
      {
        return true;
      }
      foreach (Vector3Int coordinates in blockObject.PositionedBlocks.GetFoundationCoordinates())
      {
        if (WetlandService.HasTileAtIndex(coordinates))
        {
          errorMessage = _loc.T(DryLandOnlyLocKey);
          return false;
        }
        Vector3Int surface = coordinates + Vector3Int.forward;
        if (_threadSafeWaterMap.CeiledWaterHeight(surface) - surface.z > 0)
        {
          errorMessage = _loc.T(DryLandOnlyLocKey);
          return false;
        }
      }
      return true;
    }
  }
}
