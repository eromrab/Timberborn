using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Timberborn.BaseComponentSystem;
using Timberborn.BlockObjectTools;
using Timberborn.BlockSystem;
using Timberborn.Navigation;
using UnityEngine;

namespace Tidetails
{
  [HarmonyPatch]
  internal static class NavMeshObjectUpdaterUpdatePatch
  {
    internal static float CostMultiplier = 1f;

    private static MethodBase TargetMethod()
    {
      return AccessTools.Method("Timberborn.BlockSystemNavigation.NavMeshObjectUpdater:Update");
    }

    private static void Prefix(BlockObject blockObject)
    {
      TidetailsWetPath wetPath = blockObject.GetComponent<TidetailsWetPath>();
      if (wetPath != null)
      {
        wetPath.UpdateWetState();
        CostMultiplier = wetPath.CostMultiplier;
      }
    }

    private static Exception Finalizer(Exception __exception)
    {
      CostMultiplier = 1f;
      return __exception;
    }
  }

  [HarmonyPatch]
  internal static class NavMeshObjectUpdaterGetManuallySetEdgePatch
  {
    private static MethodBase TargetMethod()
    {
      return AccessTools.Method("Timberborn.BlockSystemNavigation.NavMeshObjectUpdater:GetManuallySetEdge");
    }

    private static void Postfix(ref NavMeshEdge __result)
    {
      float multiplier = NavMeshObjectUpdaterUpdatePatch.CostMultiplier;
      if (multiplier != 1f && __result.IsRoad)
      {
        __result = NavMeshEdge.CreateGrouped(__result.Start, __result.End, __result.GroupId, true, __result.Cost * multiplier);
      }
    }
  }

  [HarmonyPatch]
  internal static class DynamicPathModelUpdateModelPatch
  {
    private static MethodBase TargetMethod()
    {
      return AccessTools.Method("Timberborn.PathSystem.DynamicPathModel:UpdateModel");
    }

    private static void Postfix(BaseComponent __instance)
    {
      if (!TidetailsFactionState.IsActive)
      {
        return;
      }
      TidetailsWetPath wetPath = __instance.GetComponent<TidetailsWetPath>();
      if (wetPath == null)
      {
        return;
      }
      wetPath.RefreshAppearanceAfterModelChange();
      wetPath.RefreshPreviewValidityTint();
    }
  }

  [HarmonyPatch]
  internal static class PreviewShowerShowPreviewPatch
  {
    private static readonly Color UnbuildableRed = new Color(0.6f, 0f, 0f, 1f);

    private static MethodBase TargetMethod()
    {
      return AccessTools.Method(typeof(PreviewShower), "ShowPreview");
    }

    private static void Prefix(Preview preview, ref Color color)
    {
      if (!TidetailsFactionState.IsActive || TidetailsPlacementValidator.Instance == null)
      {
        return;
      }
      BlockObject blockObject = preview.BlockObject;
      if (blockObject != null && !TidetailsPlacementValidator.Instance.IsValid(blockObject, out _))
      {
        color = UnbuildableRed;
      }
    }
  }

  [HarmonyPatch]
  internal static class PathRangeDrawerGetDistrictCenterPatch
  {
    private static MethodBase TargetMethod()
    {
      return AccessTools.Method("Timberborn.BuildingsNavigation.PathRangeDrawer:GetDistrictCenter", new[] { typeof(bool), typeof(bool) });
    }

    private static bool Prefix(BaseComponent __instance, ref object __result)
    {
      if (!TidetailsFactionState.IsActive || TidetailsPlacementValidator.Instance == null)
      {
        return true;
      }
      BlockObject blockObject = __instance.GetComponent<BlockObject>();
      if (blockObject != null
        && blockObject.IsPreview
        && !TidetailsPlacementValidator.Instance.IsValid(blockObject, out _))
      {
        __result = null;
        return false;
      }
      return true;
    }
  }

  [HarmonyPatch]
  internal static class PathMeshDrawerAddPatch
  {
    private static MethodBase TargetMethod()
    {
      return AccessTools.Method("Timberborn.BuildingsNavigation.PathMeshDrawer:Add");
    }

    private static void Prefix(ref WeightedCoordinates node)
    {
      if (TidetailsInvalidPreviewTiles.Contains(node.Coordinates))
      {
        node = new WeightedCoordinates(node.Coordinates, 70f);
      }
    }
  }

  internal static class TidetailsInvalidPreviewTiles
  {
    private static readonly Dictionary<BlockObject, List<Vector3Int>> Tiles =
      new Dictionary<BlockObject, List<Vector3Int>>();

    private static readonly HashSet<Vector3Int> Coordinates = new HashSet<Vector3Int>();

    public static bool Contains(Vector3Int coordinates)
    {
      return Coordinates.Contains(coordinates);
    }

    public static void Update(BlockObject blockObject, bool invalid)
    {
      bool changed = Remove(blockObject);
      if (!blockObject.Positioned)
      {
        if (changed)
        {
          MarkRangesDirty();
        }
        return;
      }
      if (!invalid)
      {
        // A different preview may have left this tile flagged. A placeable path
        // on it, including a wetland tile, has to draw green.
        foreach (Vector3Int coordinates in blockObject.PositionedBlocks.GetOccupiedCoordinates())
        {
          changed |= Coordinates.Remove(coordinates);
        }
        if (changed)
        {
          MarkRangesDirty();
        }
        return;
      }
      List<Vector3Int> tiles = new List<Vector3Int>();
      foreach (Vector3Int coordinates in blockObject.PositionedBlocks.GetOccupiedCoordinates())
      {
        tiles.Add(coordinates);
        changed |= Coordinates.Add(coordinates);
      }
      Tiles[blockObject] = tiles;
      if (changed)
      {
        MarkRangesDirty();
      }
    }

    private static bool Remove(BlockObject blockObject)
    {
      if (!Tiles.TryGetValue(blockObject, out List<Vector3Int> tiles))
      {
        return false;
      }
      Tiles.Remove(blockObject);
      foreach (Vector3Int coordinates in tiles)
      {
        Coordinates.Remove(coordinates);
      }
      return tiles.Count > 0;
    }

    internal static void SetRangeInvalidator(object invalidator)
    {
      _rangeInvalidator = invalidator;
      _markDistrictDrawersDirty = null;
    }

    private static object _rangeInvalidator;

    private static MethodInfo _markDistrictDrawersDirty;

    private static void MarkRangesDirty()
    {
      // DistrictPathNavRangeDrawer is a BaseComponent, not a UnityEngine.Object.
      // FindObjectsByType returns null for it, and reading the array length throws.
      if (_rangeInvalidator == null)
      {
        return;
      }
      if (_markDistrictDrawersDirty == null)
      {
        _markDistrictDrawersDirty = AccessTools.Method(_rangeInvalidator.GetType(), "MarkDistrictDrawersDirty");
      }
      if (_markDistrictDrawersDirty == null)
      {
        return;
      }
      _markDistrictDrawersDirty.Invoke(_rangeInvalidator, null);
    }
  }

  [HarmonyPatch]
  internal static class PathNavRangeDrawerInvalidatorLoadPatch
  {
    private static MethodBase TargetMethod()
    {
      return AccessTools.Method("Timberborn.BuildingsNavigation.PathNavRangeDrawerInvalidator:Load");
    }

    private static void Postfix(object __instance)
    {
      TidetailsInvalidPreviewTiles.SetRangeInvalidator(__instance);
    }
  }

  [HarmonyPatch(typeof(Preview), "Hide")]
  internal static class PreviewHidePatch
  {
    private static void Postfix(Preview __instance)
    {
      BlockObject blockObject = __instance.BlockObject;
      if (blockObject != null)
      {
        TidetailsInvalidPreviewTiles.Update(blockObject, false);
      }
    }
  }
}
