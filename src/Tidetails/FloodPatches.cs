using System;
using System.Reflection;
using HarmonyLib;
using Timberborn.BaseComponentSystem;
using Timberborn.BlockSystem;
using Timberborn.GameDistricts;
using Timberborn.Localization;
using Timberborn.StatusSystem;
using Timberborn.WaterObjects;

namespace Tidetails
{
  [HarmonyPatch(typeof(FloodableObject), "UpdateFloodedState")]
  internal static class FloodableObjectUpdateFloodedStatePatch
  {
    private static readonly Action<FloodableObject> Flood =
      AccessTools.MethodDelegate<Action<FloodableObject>>(AccessTools.Method(typeof(FloodableObject), "Flood"));

    private static readonly Action<FloodableObject> Unflood =
      AccessTools.MethodDelegate<Action<FloodableObject>>(AccessTools.Method(typeof(FloodableObject), "Unflood"));

    private static bool Prefix(FloodableObject __instance)
    {
      if (!TidetailsFactionState.IsActive || FloodsLikeVanilla(__instance))
      {
        return true;
      }
      if (!__instance.Enabled)
      {
        return false;
      }
      bool driedOut = CanDryOut(__instance) && !__instance.IsPreviewFlooded();
      if (!__instance.IsFlooded && driedOut)
      {
        Flood(__instance);
      }
      else if (__instance.IsFlooded && !driedOut)
      {
        Unflood(__instance);
      }
      return false;
    }

    private static bool CanDryOut(FloodableObject floodableObject)
    {
      BlockObject blockObject = floodableObject.GetComponent<BlockObject>();
      return blockObject != null && blockObject.HasEntrance && !floodableObject.HasComponent<DistrictCenter>();
    }

    internal static bool FloodsLikeVanilla(BaseComponent component)
    {
      return component.HasComponent<TidetailsLandOrShallowWaterPlacementSpec>();
    }
  }

  [HarmonyPatch]
  internal static class FloodedBuildingStatusAwakePatch
  {
    private static readonly string DriedOutLocKey = "Tidetails.Status.DriedOut";

    private static readonly string DriedOutShortLocKey = "Tidetails.Status.DriedOut.Short";

    private static MethodBase TargetMethod()
    {
      return AccessTools.Method("Timberborn.WaterBuildingsUI.FloodedBuildingStatus:Awake");
    }

    private static bool Prefix(BaseComponent __instance, ILoc ____loc, ref StatusToggle ____statusToggle)
    {
      if (!TidetailsFactionState.IsActive || FloodableObjectUpdateFloodedStatePatch.FloodsLikeVanilla(__instance))
      {
        return true;
      }
      StatusToggle statusToggle = StatusToggle.CreateNormalStatusWithAlertAndFloatingIcon("FloodedBuilding", ____loc.T(DriedOutLocKey), ____loc.T(DriedOutShortLocKey));
      ____statusToggle = statusToggle;
      FloodableObject floodableObject = __instance.GetComponent<FloodableObject>();
      floodableObject.Flooded += delegate { statusToggle.Activate(); };
      floodableObject.Unflooded += delegate { statusToggle.Deactivate(); };
      return false;
    }
  }

  [HarmonyPatch]
  internal static class FloodedBuildingPreviewValidatorPatch
  {
    private static MethodBase TargetMethod()
    {
      return AccessTools.Method("Timberborn.WaterBuildings.FloodedBuildingPreviewValidator:IsValid");
    }

    private static bool Prefix(ref bool __result, ref string warningMessage)
    {
      if (!TidetailsFactionState.IsActive)
      {
        return true;
      }
      warningMessage = string.Empty;
      __result = true;
      return false;
    }
  }
}
