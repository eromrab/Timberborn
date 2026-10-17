using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using Timberborn.BaseComponentSystem;
using Timberborn.BeaverContaminationSystem;
using Timberborn.BonusSystem;
using Timberborn.LifeSystem;
using Timberborn.Navigation;
using Timberborn.NeedBehaviorSystem;
using Timberborn.NeedSpecs;
using Timberborn.NeedSystem;
using Timberborn.WalkingSystem;
using Timberborn.WaterSystem;
using Timberborn.WorkSystem;
using UnityEngine;

namespace BadwaterRush
{
  internal static class BadwaterRushRules
  {
    internal const string ContaminationNeedId = "BadwaterContamination";

    internal const string RomanceNeedId = "Romance";

    internal const string AgeNeedId = "Age";

    internal const float AgeMultiplier = 2f;

    internal const float ActionMultiplier = 4f;

    internal const int TicksOutOfBadwaterBeforeClear = 10;

    internal const float MinimumWaterContamination = 0.05f;

    internal static bool IsContaminated(BaseComponent component)
    {
      Contaminable contaminable = component.GetComponent<Contaminable>();
      return contaminable != null && contaminable.IsContaminated;
    }
  }

  [HarmonyPatch(typeof(ContaminationIncubator), nameof(ContaminationIncubator.StartIncubation))]
  internal static class ContaminationIncubatorStartIncubationPatch
  {
    private static bool Prefix(ContaminationIncubator __instance)
    {
      Contaminable contaminable = __instance.GetComponent<Contaminable>();
      if (contaminable != null && !contaminable.IsContaminated)
      {
        contaminable.Contaminate();
      }
      return false;
    }
  }

  [HarmonyPatch]
  internal static class ContaminationApplierTickPatch
  {
    private static readonly ConditionalWeakTable<BaseComponent, TickCounter> OutOfBadwaterTicks = new ConditionalWeakTable<BaseComponent, TickCounter>();

    private sealed class TickCounter
    {
      internal int Ticks;
    }

    private static MethodBase TargetMethod()
    {
      return AccessTools.Method(
        AccessTools.TypeByName("Timberborn.BeaverContaminationSystem.ContaminationApplier"),
        "Tick");
    }

    private static void Postfix(object __instance)
    {
      BaseComponent component = __instance as BaseComponent;
      if (component == null)
      {
        return;
      }
      Contaminable contaminable = component.GetComponent<Contaminable>();
      if (contaminable == null)
      {
        return;
      }

      ContaminationIncubator incubator = component.GetComponent<ContaminationIncubator>();
      if (!contaminable.IsContaminated && incubator != null && (incubator.IsIncubating || incubator.IncubationFinished))
      {
        contaminable.Contaminate();
        incubator.ResetIncubation();
      }

      if (!contaminable.IsContaminated)
      {
        OutOfBadwaterTicks.Remove(component);
        return;
      }

      if (IsInContaminatedWater(component))
      {
        if (OutOfBadwaterTicks.TryGetValue(component, out TickCounter existing))
        {
          existing.Ticks = 0;
        }
        return;
      }

      TickCounter counter = OutOfBadwaterTicks.GetValue(component, _ => new TickCounter());
      counter.Ticks++;
      if (counter.Ticks < BadwaterRushRules.TicksOutOfBadwaterBeforeClear)
      {
        return;
      }

      NeedManager needManager = component.GetComponent<NeedManager>();
      if (needManager != null)
      {
        needManager.ResetNeed(BadwaterRushRules.ContaminationNeedId);
      }
      OutOfBadwaterTicks.Remove(component);
    }

    private static bool IsInContaminatedWater(BaseComponent applier)
    {
      IThreadSafeWaterMap waterMap = Traverse.Create(applier).Field("_threadSafeWaterMap").GetValue<IThreadSafeWaterMap>();
      if (waterMap == null)
      {
        return false;
      }
      Vector3Int coordinates = NavigationCoordinateSystem.WorldToGridInt(applier.Transform.position);
      return waterMap.CellIsUnderwater(coordinates)
        && waterMap.ColumnContamination(coordinates) >= BadwaterRushRules.MinimumWaterContamination;
    }
  }

  [HarmonyPatch]
  internal static class ContaminationNeedEnablerIsDisabledWhenContaminatedPatch
  {
    private static MethodBase TargetMethod()
    {
      return AccessTools.Method(
        AccessTools.TypeByName("Timberborn.BeaverContaminationSystem.ContaminationNeedEnabler"),
        "IsDisabledWhenContaminated");
    }

    private static void Postfix(NeedSpec needSpec, ref bool __result)
    {
      if (needSpec.Id == BadwaterRushRules.RomanceNeedId || needSpec.Id == BadwaterRushRules.AgeNeedId)
      {
        __result = false;
      }
    }
  }

  [HarmonyPatch(typeof(NeedManager), "Tick")]
  internal static class AgeNeedTickPatch
  {
    private static void Postfix(NeedManager __instance)
    {
      if (!__instance.HasNeed(BadwaterRushRules.AgeNeedId))
      {
        return;
      }
      LifeProgressor progressor = __instance.GetComponent<LifeProgressor>();
      ILongevity longevity = __instance.GetComponent<ILongevity>();
      if (progressor == null || longevity == null)
      {
        return;
      }
      LifeService lifeService = Traverse.Create(progressor).Field("_lifeService").GetValue<LifeService>();
      if (lifeService == null)
      {
        return;
      }
      Traverse life = Traverse.Create(lifeService);
      int childhoodDays = life.Field("_daysOfChildhood").GetValue<int>();
      int lifespanDays = life.Field("_averageLifespan").GetValue<int>();
      if (lifespanDays <= 0 || childhoodDays <= 0 || childhoodDays >= lifespanDays)
      {
        return;
      }
      NeedSpec spec = __instance.GetNeedSpec(BadwaterRushRules.AgeNeedId);
      float childEnd = childhoodDays / (float)lifespanDays;
      float death = longevity.ExpectedLongevity;
      if (death <= childEnd)
      {
        death = childEnd + 0.0001f;
      }
      float lifeProgress = progressor.LifeProgress;
      float points = lifeProgress <= childEnd
        ? Mathf.Lerp(spec.MinimumValue, 0f, lifeProgress / childEnd)
        : Mathf.Lerp(0f, spec.MaximumValue, (lifeProgress - childEnd) / (death - childEnd));
      Traverse.Create(__instance).Method("GetNeed", BadwaterRushRules.AgeNeedId).GetValue<Need>().SetPoints(points);
    }
  }

  [HarmonyPatch(typeof(NeedManager), nameof(NeedManager.AnyNeedIsInCriticalState))]
  internal static class NeedManagerAnyNeedIsInCriticalStatePatch
  {
    private static void Postfix(NeedManager __instance, ref bool __result)
    {
      if (!__result || OtherNeedIsCritical(__instance))
      {
        return;
      }
      __result = false;
    }

    private static bool OtherNeedIsCritical(NeedManager needManager)
    {
      foreach (NeedSpec needSpec in needManager.NeedSpecs)
      {
        if (needSpec.Id != BadwaterRushRules.ContaminationNeedId && needManager.NeedIsInCriticalState(needSpec.Id))
        {
          return true;
        }
      }
      return false;
    }
  }

  [HarmonyPatch(typeof(WorkRefuser), "ShouldRefuseWork")]
  internal static class WorkRefuserShouldRefuseWorkPatch
  {
    private static void Postfix(WorkRefuser __instance, ref bool __result)
    {
      if (!__result)
      {
        return;
      }
      NeedManager needManager = __instance.GetComponent<NeedManager>();
      if (needManager == null)
      {
        return;
      }
      foreach (NeedSpec needSpec in needManager.NeedSpecs)
      {
        if (needSpec.Id == BadwaterRushRules.ContaminationNeedId)
        {
          continue;
        }
        if (needManager.NeedIsInCriticalState(needSpec.Id) && needSpec.HasSpec<NeedPreventingWorkSpec>())
        {
          return;
        }
      }
      __result = false;
    }
  }

  [HarmonyPatch(typeof(NeedManager), nameof(NeedManager.NeedIsInCriticalState))]
  internal static class NeedManagerNeedIsInCriticalStatePatch
  {
    private static void Postfix(string needId, ref bool __result)
    {
      if (needId == BadwaterRushRules.ContaminationNeedId)
      {
        __result = false;
      }
    }
  }

  [HarmonyPatch(typeof(NeedPenaltyManager), "UpdatePenalties")]
  internal static class NeedPenaltyManagerUpdatePenaltiesPatch
  {
    private static bool Prefix(NeedSpec needSpec)
    {
      return needSpec.Id != BadwaterRushRules.ContaminationNeedId;
    }
  }

  [HarmonyPatch(typeof(NeedPenaltyManager), nameof(NeedPenaltyManager.InitializeEntity))]
  internal static class NeedPenaltyManagerInitializeEntityPatch
  {
    private static void Postfix(NeedPenaltyManager __instance)
    {
      NeedManager needManager = Traverse.Create(__instance).Field("_needManager").GetValue<NeedManager>();
      BonusManager bonusManager = Traverse.Create(__instance).Field("_bonusManager").GetValue<BonusManager>();
      if (needManager == null || bonusManager == null || needManager.NeedIsFavorable(BadwaterRushRules.ContaminationNeedId))
      {
        return;
      }
      bonusManager.RemoveBonus("MovementSpeed", -0.7f);
    }
  }

  [HarmonyPatch(typeof(LifeProgressor), "IncreaseLifeProgress")]
  internal static class LifeProgressorIncreaseLifeProgressPatch
  {
    private static void Postfix(LifeProgressor __instance)
    {
      if (!BadwaterRushRules.IsContaminated(__instance))
      {
        return;
      }
      Traverse traverse = Traverse.Create(__instance);
      LifeService lifeService = traverse.Field("_lifeService").GetValue<LifeService>();
      BonusManager bonusManager = traverse.Field("_bonusManager").GetValue<BonusManager>();
      float extra = lifeService.LifeProgressIncreasePerTick
        / bonusManager.Multiplier("LifeExpectancy")
        * (BadwaterRushRules.AgeMultiplier - 1f);
      float progress = traverse.Property("LifeProgress").GetValue<float>();
      traverse.Property("LifeProgress").SetValue(progress + extra);
    }
  }

  [HarmonyPatch(typeof(Worker), "Tick")]
  internal static class WorkerTickPatch
  {
    private static void Postfix(Worker __instance)
    {
      if (!__instance.Enabled || !BadwaterRushRules.IsContaminated(__instance))
      {
        return;
      }
      Traverse.Create(__instance).Property("WorkingSpeedMultiplier")
        .SetValue(__instance.WorkingSpeedMultiplier * BadwaterRushRules.ActionMultiplier);
    }
  }

  [HarmonyPatch(typeof(WalkerSpeedManager), nameof(WalkerSpeedManager.GetWalkerSpeedAtCurrentPosition))]
  internal static class WalkerSpeedManagerGetWalkerSpeedAtCurrentPositionPatch
  {
    private static void Postfix(WalkerSpeedManager __instance, ref float __result)
    {
      if (BadwaterRushRules.IsContaminated(__instance))
      {
        __result *= BadwaterRushRules.ActionMultiplier;
      }
    }
  }
}
