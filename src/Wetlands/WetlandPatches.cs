using System;
using System.Reflection;
using HarmonyLib;
using Timberborn.BlockSystem;
using Timberborn.PrefabOptimization;
using UnityEngine;

namespace Wetlands
{
  [HarmonyPatch]
  internal static class MoistureCalculationTaskGetInitialMoisturizerRangePatch
  {
    private static MethodBase TargetMethod()
    {
      return AccessTools.Method("Timberborn.SoilMoistureSystem.MoistureCalculationTask:GetInitialMoisturizerRange");
    }

    private static void Postfix(int index3D, ref float __result)
    {
      if (WetlandService.IsMoisturizingIndex3D(index3D))
      {
        __result = Math.Max(__result, 5f);
      }
    }
  }

  [HarmonyPatch]
  internal static class MoistureCalculationTaskGetMoisturePatch
  {
    private static MethodBase TargetMethod()
    {
      Type type = AccessTools.TypeByName("Timberborn.SoilMoistureSystem.MoistureCalculationTask");
      return AccessTools.Method(type, "GetMoisture", new[] { typeof(int) });
    }

    private static void Postfix(int index3D, ref int __result)
    {
      if (WetlandService.IsMoisturizingIndex3D(index3D))
      {
        __result = Math.Max(__result, 1);
      }
    }
  }

  [HarmonyPatch]
  internal static class MoistureCalculationTaskGetMoistureFromWaterPatch
  {
    private static MethodBase TargetMethod()
    {
      return AccessTools.Method("Timberborn.SoilMoistureSystem.MoistureCalculationTask:GetMoistureFromWater");
    }

    private static void Postfix(int neighborIndex, ref int __result)
    {
      if (__result > 0)
      {
        return;
      }
      if (WetlandService.IsMoisturizingIndex3D(neighborIndex)
          || WetlandService.IsMoisturizingIndex2D(neighborIndex))
      {
        __result = 1;
      }
    }
  }

  [HarmonyPatch]
  internal static class MoistureCalculationTaskCalculateMoistureForCellPatch
  {
    private static MethodBase TargetMethod()
    {
      return AccessTools.Method("Timberborn.SoilMoistureSystem.MoistureCalculationTask:CalculateMoistureForCell");
    }

    private static void Postfix(int index2D, int index3D, ref float __result)
    {
      if (WetlandService.IsMoisturizingIndex3D(index3D)
          || WetlandService.IsMoisturizingIndex2D(index2D))
      {
        __result = Math.Max(__result, 1f);
      }
    }
  }

  [HarmonyPatch]
  internal static class FloodableNaturalResourceServiceWaterDepthPatch
  {
    private static MethodBase TargetMethod()
    {
      return AccessTools.Method("Timberborn.NaturalResourcesMoisture.FloodableNaturalResourceService:WaterDepth");
    }

    private static void Postfix(Vector3Int coordinates, ref int __result)
    {
      if (WetlandService.HasTileAtIndex(coordinates))
      {
        __result = Math.Max(__result, 1);
      }
    }
  }

  [HarmonyPatch]
  internal static class MatterBelowValidatorGroundPatch
  {
    private static MethodBase TargetMethod()
    {
      return AccessTools.Method(typeof(MatterBelowValidator), "Validate", new[] { typeof(Block).MakeByRefType(), typeof(bool) });
    }

    private static void Postfix(in Block block, ref bool __result)
    {
      if (__result)
      {
        return;
      }
      if (block.MatterBelow != MatterBelow.Ground && block.MatterBelow != MatterBelow.GroundOrStackable)
      {
        return;
      }
      if (WetlandService.HasTileAtIndex(block.Coordinates))
      {
        __result = true;
      }
    }
  }

  [HarmonyPatch]
  internal static class TerrainMeshManagerGetTerrainBlockPatch
  {
    private static MethodBase TargetMethod()
    {
      return AccessTools.Method("Timberborn.TerrainSystemRendering.TerrainMeshManager:GetTerrainBlock");
    }

    private static void Postfix(Vector3Int coordinates, ref IntermediateMesh __result)
    {
      if (__result == null)
      {
        return;
      }
      if (!WetlandService.CoversWetland(coordinates))
      {
        return;
      }
      __result = WetlandBlockMaterials.Apply(__result, coordinates);
    }
  }

  [HarmonyPatch]
  internal static class TerrainTileUpdateMeshPatch
  {
    private static MethodBase TargetMethod()
    {
      Type outer = AccessTools.TypeByName("Timberborn.TerrainSystemRendering.TerrainMeshManager");
      Type inner = AccessTools.Inner(outer, "TileComponents");
      return AccessTools.Method(inner, "UpdateMesh");
    }

    private static void Postfix(MeshRenderer ____meshRenderer, BuiltMesh buildMesh)
    {
      if (____meshRenderer != null && buildMesh.Materials != null)
      {
        ____meshRenderer.sharedMaterials = buildMesh.Materials;
      }
    }
  }

  [HarmonyPatch]
  internal static class TerrainMeshManagerLoadPatch
  {
    private static MethodBase TargetMethod()
    {
      return AccessTools.Method("Timberborn.TerrainSystemRendering.TerrainMeshManager:Load");
    }

    private static void Postfix(object __instance)
    {
      TerrainMeshInvalidator.Capture(__instance);
    }
  }

}
