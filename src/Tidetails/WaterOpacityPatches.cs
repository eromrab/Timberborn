using System.Reflection;
using HarmonyLib;
using Timberborn.GameFactionSystem;
using UnityEngine;

namespace Tidetails
{
  // Opaque water uses PhysicalWater_Opaque, which stays solid no matter what
  // _WaterOpacity is. See-through mode swaps in PhysicalWater_Transparent and
  // sets that global to 0.4. Tidetails keeps the blended material and uses 0.85.
  [HarmonyPatch]
  internal static class WaterOpacityPatch
  {
    internal const float TidetailsOpaqueOpacity = 0.85f;

    private static readonly int WaterOpacityProperty = Shader.PropertyToID("_WaterOpacity");

    private static MethodBase TargetMethod()
    {
      return AccessTools.Method("Timberborn.WaterSystemRendering.WaterOpacityService:ToggleOpacity");
    }

    private static void Postfix(bool setTransparent)
    {
      ApplyOpaque(setTransparent);
    }

    internal static void ApplyOpaque(bool waterIsTransparent)
    {
      if (waterIsTransparent || !TidetailsFactionState.IsActive)
      {
        return;
      }
      Shader.SetGlobalFloat(WaterOpacityProperty, TidetailsOpaqueOpacity);
    }
  }

  [HarmonyPatch]
  internal static class WaterMeshMaterialPatch
  {
    private static object _waterMesh;

    private static Material _transparentMaterial;

    private static MethodBase TargetMethod()
    {
      return AccessTools.Method("Timberborn.WaterSystemRendering.WaterMesh:GetWaterMaterial");
    }

    private static void Postfix(object __instance, ref Material __result)
    {
      _waterMesh = __instance;
      if (!TidetailsFactionState.IsActive)
      {
        return;
      }
      Material transparent = TransparentMaterial(__instance);
      if (transparent == null)
      {
        return;
      }
      bool seeThrough = __result == transparent;
      __result = transparent;
      WaterOpacityPatch.ApplyOpaque(seeThrough);
    }

    internal static void Refresh()
    {
      if (_waterMesh == null || !TidetailsFactionState.IsActive)
      {
        return;
      }
      MethodInfo update = AccessTools.Method(_waterMesh.GetType(), "UpdateMaterialTransparency");
      update.Invoke(_waterMesh, null);
    }

    private static Material TransparentMaterial(object waterMesh)
    {
      if (_transparentMaterial != null)
      {
        return _transparentMaterial;
      }
      object spec = AccessTools.Field(waterMesh.GetType(), "_waterMeshSpec").GetValue(waterMesh);
      if (spec == null)
      {
        return null;
      }
      object assetRef = spec.GetType().GetProperty("TransparentMaterial").GetValue(spec);
      if (assetRef == null)
      {
        return null;
      }
      _transparentMaterial = assetRef.GetType().GetProperty("Asset").GetValue(assetRef) as Material;
      return _transparentMaterial;
    }
  }

  [HarmonyPatch(typeof(FactionService), "Load")]
  internal static class WaterOpacityFactionLoadPatch
  {
    private static void Postfix()
    {
      WaterOpacityPatch.ApplyOpaque(waterIsTransparent: false);
      WaterMeshMaterialPatch.Refresh();
    }
  }
}
