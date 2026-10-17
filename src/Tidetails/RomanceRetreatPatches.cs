using System.Linq;
using System.Reflection;
using HarmonyLib;
using Timberborn.Attractions;
using Timberborn.BaseComponentSystem;
using Timberborn.Beavers;
using Timberborn.BehaviorSystem;
using Timberborn.Carrying;
using Timberborn.Characters;
using Timberborn.EnterableSystem;
using Timberborn.GameDistricts;
using Timberborn.NeedBehaviorSystem;
using Timberborn.NeedSystem;
using Timberborn.WorkSystem;
using UnityEngine;

namespace Tidetails
{
  // Positive Romance does not start a visit. A beaver already inside may stay until the pair finishes.
  [HarmonyPatch(typeof(AttractionNeedBehavior), "ActionPosition")]
  internal static class RomanceRetreatPositiveRomancePatch
  {
    private static bool Prefix(AttractionNeedBehavior __instance, NeedManager needManager, ref Vector3? __result)
    {
      if (ShouldTurnAway(__instance, needManager))
      {
        __result = null;
        return false;
      }
      return true;
    }

    internal static bool ShouldTurnAway(AttractionNeedBehavior behavior, NeedManager needManager)
    {
      if (behavior.GetComponent<RomanceRetreat>() == null || needManager == null)
      {
        return false;
      }
      if (needManager.GetComponent<Child>() != null)
      {
        return true;
      }
      if (RomanceRetreat.NeedsRomance(needManager))
      {
        return false;
      }
      Enterer enterer = needManager.GetComponent<Enterer>();
      Enterable enterable = behavior.GetComponent<Enterable>();
      return enterer == null || enterer.CurrentBuilding != enterable;
    }
  }

  // Keep a lone beaver inside briefly, fill Romance only once a pair is together, then send both out.
  [HarmonyPatch(typeof(AttractionNeedBehavior), "Decide")]
  internal static class RomanceRetreatVisitPatch
  {
    private static bool Prefix(AttractionNeedBehavior __instance, BehaviorAgent agent, ref Decision __result)
    {
      RomanceRetreat retreat = __instance.GetComponent<RomanceRetreat>();
      if (retreat == null || retreat.IsPaused)
      {
        return true;
      }
      if (agent.GetComponent<Child>() != null)
      {
        __result = Decision.ReleaseNextTick();
        return false;
      }
      if (!retreat.IsStocked && !retreat.VisitInProgress)
      {
        return true;
      }
      Enterable enterable = __instance.GetComponent<Enterable>();
      Enterer enterer = agent.GetComponent<Enterer>();
      NeedManager needs = agent.GetComponent<NeedManager>();
      bool inside = enterer != null && enterer.CurrentBuilding == enterable;
      if (!inside && !RomanceRetreat.NeedsRomance(needs))
      {
        __result = Decision.ReleaseNextTick();
        return false;
      }
      if (!inside)
      {
        return true;
      }
      if (enterable.NumberOfEnterersInside >= 2)
      {
        if (retreat.PairIsFull())
        {
          retreat.CompleteVisit();
          __result = Decision.ReleaseNextTick();
          return false;
        }
        if (RomanceRetreat.IsRomanceFull(needs))
        {
          __result = Decision.ReturnNextTick();
          return false;
        }
        return true;
      }
      if (retreat.AloneWaitExpired || OtherNeedIsCritical(needs))
      {
        __result = Decision.ReleaseNextTick();
        return false;
      }
      __result = Decision.ReturnNextTick();
      return false;
    }

    private static bool OtherNeedIsCritical(NeedManager needs)
    {
      return needs != null && needs.AnyNeedIsInCriticalState() && !RomanceRetreat.RomanceIsCritical(needs);
    }
  }

  // Off shift, or at Romance -1, walk to a stocked retreat instead of waiting on the leisure lottery.
  [HarmonyPatch(typeof(WorkerRootBehavior), "Decide")]
  internal static class RomanceRetreatSeekPatch
  {
    private static bool Prefix(BehaviorAgent agent, ref Decision __result)
    {
      if (!TryClaimRetreat(agent, out Decision decision))
      {
        return true;
      }
      __result = decision;
      return false;
    }

    private static bool TryClaimRetreat(BehaviorAgent agent, out Decision decision)
    {
      decision = default;
      Character character = agent.GetComponent<Character>();
      if (character == null || !character.Alive || agent.GetComponent<Child>() != null)
      {
        return false;
      }
      NeedManager needs = agent.GetComponent<NeedManager>();
      if (!RomanceRetreat.NeedsRomance(needs))
      {
        return false;
      }
      if (!RomanceRetreat.RomanceIsCritical(needs) && IsOnShift(agent))
      {
        return false;
      }
      Citizen citizen = agent.GetComponent<Citizen>();
      DistrictCenter district = citizen != null ? citizen.AssignedDistrict : null;
      if (!RomanceRetreat.TryFindVisit(district, out AttractionNeedBehavior retreat))
      {
        return false;
      }
      Decision inner = retreat.Decide(agent);
      if (inner.ShouldReleaseNow)
      {
        return false;
      }
      decision = Decision.TransferNow(retreat, inner);
      return true;
    }

    private static bool IsOnShift(BehaviorAgent agent)
    {
      WorkRefuser refuser = agent.GetComponent<WorkRefuser>();
      if (refuser != null && refuser.RefusesWork)
      {
        return false;
      }
      Worker worker = agent.GetComponent<Worker>();
      WorkerWorkingHours hours = agent.GetComponent<WorkerWorkingHours>();
      return worker != null && worker.Employed && hours != null && hours.AreWorkingHours;
    }
  }

  // Kits do not feel Romance until they grow up. Checked from both sides because awake order varies.
  [HarmonyPatch(typeof(NeedManager), "Awake")]
  internal static class RomanceRetreatChildNeedPatch
  {
    private static void Postfix(NeedManager __instance)
    {
      DisableRomance(__instance);
    }

    internal static void DisableRomance(BaseComponent beaver)
    {
      if (beaver == null || beaver.GetComponent<Child>() == null)
      {
        return;
      }
      NeedManager needs = beaver.GetComponent<NeedManager>();
      if (needs != null && needs.HasNeed(RomanceRetreat.RomanceNeedId))
      {
        needs.DisableNeed(RomanceRetreat.RomanceNeedId);
      }
    }
  }

  [HarmonyPatch(typeof(Child), "PreInitializeEntity")]
  internal static class RomanceRetreatChildInitPatch
  {
    private static void Postfix(Child __instance)
    {
      RomanceRetreatChildNeedPatch.DisableRomance(__instance);
    }
  }

  // Grab the kit SpawnChild just created so it can leave the doorway with the parents.
  [HarmonyPatch]
  internal static class RomanceRetreatKitSpawnPatch
  {
    private static MethodBase TargetMethod()
    {
      System.Type entityService = AccessTools.TypeByName("Timberborn.EntitySystem.EntityService");
      return AccessTools.GetDeclaredMethods(entityService).First(method =>
        method.Name == "Instantiate" && method.GetParameters().Length == 1 && method.GetParameters()[0].ParameterType.Name == "Builder");
    }

    private static void Postfix(BaseComponent __result)
    {
      if (!RomanceRetreat.ExpectingKit || __result == null)
      {
        return;
      }
      RomanceRetreat.SpawnedKit = __result.GetComponent<Child>();
    }
  }

  // Same haulers who stock breeding pods also stock the retreat.
  [HarmonyPatch]
  internal static class RomanceRetreatDeliveryPatch
  {
    private static MethodBase TargetMethod()
    {
      return AccessTools.Method("Timberborn.Reproduction.BringNutrientBehavior:Decide");
    }

    private static bool Prefix(BehaviorAgent agent, ref Decision __result)
    {
      Citizen citizen = agent.GetComponent<Citizen>();
      DistrictCenter district = citizen != null ? citizen.AssignedDistrict : null;
      if (!district)
      {
        return true;
      }
      CarrierInventoryFinder finder = agent.GetComponent<CarrierInventoryFinder>();
      if (finder != null && RomanceRetreat.TryStartDelivery(finder, district))
      {
        __result = Decision.ReleaseNextTick();
        return false;
      }
      return true;
    }
  }
}
