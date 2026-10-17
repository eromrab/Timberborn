using System;
using Timberborn.BlockSystem;
using Timberborn.Localization;
using Timberborn.TemplateSystem;
using Timberborn.TerrainSystem;
using Timberborn.WaterObjects;
using Timberborn.WaterSystem;
using UnityEngine;
using Wetlands;

namespace Tidetails
{
  internal class TidetailsPlacementValidator : IBlockObjectValidator
  {
    private const int MaxTilesFromWaterEntry = 5;

    private static readonly string MustBeInWaterLocKey = "Tidetails.Preview.MustBeInWater";

    private static readonly string RequiresWaterLocKey = "Tidetails.Preview.RequiresWater";

    private static readonly string OnlyMudSlideOnMudBlockLocKey = "Tidetails.Preview.OnlyMudSlideOnMudBlock";

    private static readonly string LandOrShallowWaterLocKey = "Tidetails.Preview.LandOrShallowWater";

    private static readonly string TooFarFromWaterLocKey = "Tidetails.Preview.TooFarFromWater";

    private static readonly string[] WetlandWorkplacePrefixes =
    {
      "ScavengerFlag",
      "GathererFlag",
      "LumberjackFlag",
      "Forester",
      "AquaticFarmhouse"
    };

    private readonly ILoc _loc;

    private readonly IThreadSafeWaterMap _threadSafeWaterMap;

    private readonly IBlockService _blockService;

    private readonly ITerrainService _terrainService;

    private readonly WetlandService _wetlandService;

    internal static TidetailsPlacementValidator Instance { get; private set; }

    public TidetailsPlacementValidator(
      ILoc loc,
      IThreadSafeWaterMap threadSafeWaterMap,
      IBlockService blockService,
      ITerrainService terrainService,
      WetlandService wetlandService)
    {
      _loc = loc;
      _threadSafeWaterMap = threadSafeWaterMap;
      _blockService = blockService;
      _terrainService = terrainService;
      _wetlandService = wetlandService;
      Instance = this;
    }

    public bool IsValid(BlockObject blockObject, out string errorMessage)
    {
      errorMessage = null;
      if (!TidetailsFactionState.IsActive || !blockObject.IsPreview)
      {
        return true;
      }
      bool requiresWater = blockObject.HasComponent<TidetailsRequiresWaterSpec>();
      bool isMudBlock = blockObject.HasComponent<TidetailsMudBlockSpec>();
      bool isMudSlide = blockObject.HasComponent<TidetailsMudSlideSpec>();
      bool onMudBlock = IsOnMudBlock(blockObject);
      if (onMudBlock && !isMudBlock && !isMudSlide)
      {
        errorMessage = _loc.T(OnlyMudSlideOnMudBlockLocKey);
        return false;
      }
      if (isMudSlide && onMudBlock)
      {
        return true;
      }
      if (blockObject.HasComponent<TidetailsLandOrShallowWaterPlacementSpec>())
      {
        if (!AllFoundationTilesLandOrShallowWater(blockObject))
        {
          errorMessage = _loc.T(LandOrShallowWaterLocKey);
          return false;
        }
      }
      else if (requiresWater)
      {
        Vector3Int baseCoordinates = blockObject.TransformCoordinates(Vector3Int.zero) + new Vector3Int(0, 0, blockObject.BaseZ);
        if (!HasWaterAbove(baseCoordinates))
        {
          errorMessage = _loc.T(RequiresWaterLocKey);
          return false;
        }
      }
      else if (blockObject.HasEntrance && !IsEntranceInWater(blockObject) && !IsWorkplaceOnWetland(blockObject))
      {
        errorMessage = _loc.T(MustBeInWaterLocKey);
        return false;
      }
      if (!isMudSlide
          && blockObject.HasComponent<TidetailsWetPathSpec>()
          && !IsWithinWaterRange(blockObject))
      {
        errorMessage = _loc.T(TooFarFromWaterLocKey);
        return false;
      }
      return true;
    }

    private bool IsEntranceInWater(BlockObject blockObject)
    {
      FloodableObject floodableObject = blockObject.GetComponent<FloodableObject>();
      return floodableObject != null
        ? floodableObject.IsPreviewFlooded()
        : HasWaterAbove(blockObject.PositionedEntrance.Coordinates);
    }

    private bool IsOnMudBlock(BlockObject blockObject)
    {
      foreach (Vector3Int coordinates in blockObject.PositionedBlocks.GetFoundationCoordinates())
      {
        if (_blockService.GetFirstObjectWithComponentAt<TidetailsMudBlockSpec>(coordinates + Vector3Int.back) != null)
        {
          return true;
        }
      }
      return false;
    }

    private bool HasWaterAbove(Vector3Int coordinates)
    {
      return _threadSafeWaterMap.CeiledWaterHeight(coordinates) - coordinates.z > 0;
    }

    private bool IsWetland(Vector3Int coordinates)
    {
      return _wetlandService.HasTileAt(coordinates);
    }

    private bool IsWorkplaceOnWetland(BlockObject blockObject)
    {
      TemplateSpec template = blockObject.GetComponent<TemplateSpec>();
      if (template == null || !IsWetlandWorkplace(template.TemplateName))
      {
        return false;
      }
      if (blockObject.HasEntrance && IsWetland(blockObject.PositionedEntrance.Coordinates))
      {
        return true;
      }
      foreach (Vector3Int coordinates in blockObject.PositionedBlocks.GetFoundationCoordinates())
      {
        if (IsWetland(coordinates))
        {
          return true;
        }
      }
      return false;
    }

    private static bool IsWetlandWorkplace(string templateName)
    {
      for (int i = 0; i < WetlandWorkplacePrefixes.Length; i++)
      {
        if (templateName.StartsWith(WetlandWorkplacePrefixes[i], StringComparison.Ordinal))
        {
          return true;
        }
      }
      return false;
    }

    private bool AllFoundationTilesLandOrShallowWater(BlockObject blockObject)
    {
      foreach (Vector3Int coordinates in blockObject.PositionedBlocks.GetFoundationCoordinates())
      {
        if (HasWaterAbove(coordinates))
        {
          return false;
        }
      }
      return true;
    }

    private bool IsWithinWaterRange(BlockObject blockObject)
    {
      bool any = false;
      foreach (Vector3Int coordinates in blockObject.PositionedBlocks.GetOccupiedCoordinates())
      {
        any = true;
        if (!IsWithinWaterRange(coordinates))
        {
          return false;
        }
      }
      if (!any)
      {
        return IsWithinWaterRange(blockObject.Coordinates);
      }
      return true;
    }

    private bool IsWithinWaterRange(Vector3Int coordinates)
    {
      if (IsWetland(coordinates) || HasWaterAbove(coordinates) || IsWaterEntry(coordinates))
      {
        return true;
      }
      for (int dx = -MaxTilesFromWaterEntry; dx <= MaxTilesFromWaterEntry; dx++)
      {
        int remaining = MaxTilesFromWaterEntry - Math.Abs(dx);
        for (int dy = -remaining; dy <= remaining; dy++)
        {
          if (dx == 0 && dy == 0)
          {
            continue;
          }
          Vector3Int candidate = coordinates + new Vector3Int(dx, dy, 0);
          if (IsWaterEntry(candidate))
          {
            return true;
          }
        }
      }
      return false;
    }

    private bool IsWaterEntry(Vector3Int coordinates)
    {
      if (!_terrainService.Contains(coordinates))
      {
        return false;
      }
      if (IsMudSlideAt(coordinates))
      {
        return true;
      }
      return HasWaterAbove(coordinates) && HasPathAt(coordinates);
    }

    private bool HasPathAt(Vector3Int coordinates)
    {
      return _blockService.GetFirstObjectWithComponentAt<TidetailsWetPathSpec>(coordinates) != null;
    }

    private bool IsMudSlideAt(Vector3Int coordinates)
    {
      if (_blockService.GetFirstObjectWithComponentAt<TidetailsMudSlideSpec>(coordinates) != null)
      {
        return true;
      }
      return _blockService.GetFirstObjectWithComponentAt<TidetailsMudSlideSpec>(coordinates + Vector3Int.back) != null;
    }
  }
}
