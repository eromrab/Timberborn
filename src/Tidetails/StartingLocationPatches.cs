using System.Reflection;
using HarmonyLib;
using Timberborn.Buildings;
using Timberborn.Coordinates;
using Timberborn.Goods;
using Timberborn.InventorySystem;
using Timberborn.SimpleOutputBuildings;

namespace Tidetails
{
  [HarmonyPatch]
  internal static class StartingBuildingInitializerInitializePatch
  {
    private static MethodBase TargetMethod()
    {
      return AccessTools.Method("Timberborn.GameStartup.StartingBuildingInitializer:Initialize");
    }

    private static bool Prefix(object __instance)
    {
      if (!TidetailsFactionState.IsActive || TidetailsStartingLocationFinder.Instance == null)
      {
        return true;
      }
      return !TidetailsStartingLocationFinder.Instance.TryInitialize(__instance);
    }
  }

  [HarmonyPatch]
  internal static class StartingBuildingSpawnerPlacePatch
  {
    private static MethodBase TargetMethod()
    {
      return AccessTools.Method("Timberborn.GameStartup.StartingBuildingSpawner:Place");
    }

    private static void Postfix(Placement? placement)
    {
      if (placement.HasValue && TidetailsFactionState.IsActive)
      {
        TidetailsStartingLocationFinder.Instance?.LogPlacement(placement.Value);
      }
    }
  }

  internal static class TidetailsStartingGoods
  {
    public static readonly GoodAmount StartingLogs = new GoodAmount("Log", 20);

    public static Inventory GetInventory(Building startingBuilding)
    {
      return startingBuilding != null && TidetailsFactionState.IsActive
        ? startingBuilding.GetComponent<SimpleOutputInventory>()?.Inventory
        : null;
    }
  }

  [HarmonyPatch]
  internal static class StartingGoodsProviderAddPatch
  {
    private static MethodBase TargetMethod()
    {
      return AccessTools.Method("Timberborn.GameStartup.StartingGoodsProvider:AddStartingInventory");
    }

    private static void Postfix(Building startingBuilding)
    {
      TidetailsStartingGoods.GetInventory(startingBuilding)?.GiveExistingIgnoringCapacity(TidetailsStartingGoods.StartingLogs);
    }
  }

  [HarmonyPatch]
  internal static class StartingGoodsProviderRemovePatch
  {
    private static MethodBase TargetMethod()
    {
      return AccessTools.Method("Timberborn.GameStartup.StartingGoodsProvider:RemoveStartingInventory");
    }

    private static void Postfix(Building startingBuilding)
    {
      TidetailsStartingGoods.GetInventory(startingBuilding)?.TakeExisting(TidetailsStartingGoods.StartingLogs);
    }
  }
}
