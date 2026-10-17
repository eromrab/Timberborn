using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Timberborn.BaseComponentSystem;
using Timberborn.Goods;
using Timberborn.Localization;
using Timberborn.SliderToggleSystem;
using Timberborn.TemplateSystem;
using Timberborn.WaterBuildings;
using Timberborn.Workshops;
using UnityEngine;
using UnityEngine.UIElements;

namespace Tidetails
{
  // Vanilla cannot put water / badwater / both on a worker pump: mechanical pumps use WaterMover
  // (fluid, powered), and the two manufactory water consumers deadlock if both specs are present.
  // Patches no-op unless Tidetails is the current faction and the building is DeepWaterPump.IronTeeth.
  internal static class TidetailsUnifiedDeepPump
  {
    internal const string UnfilteredRecipeId = "Tidetails.DeepPumpUnfiltered";

    private const string WaterGoodId = "Water";

    private const string BadwaterGoodId = "Badwater";

    private const float WaterPerGood = 0.2f;

    private const float EfficiencyThreshold = 0.0001f;

    internal static bool IsUnifiedPump(Manufactory manufactory)
    {
      if (!TidetailsFactionState.IsActive || manufactory == null)
      {
        return false;
      }
      TemplateSpec template = manufactory.GetComponent<TemplateSpec>();
      return template != null && template.TemplateName == "DeepWaterPump.IronTeeth";
    }

    internal static bool IsUnfiltered(Manufactory manufactory)
    {
      return IsUnifiedPump(manufactory) && manufactory.CurrentRecipe?.Id == UnfilteredRecipeId;
    }

    internal static float ProductionEfficiency(Manufactory manufactory)
    {
      WaterInput waterInput = manufactory.GetComponent<WaterInput>();
      if (waterInput == null || !waterInput.IsUnderwater)
      {
        return 0f;
      }
      string recipeId = manufactory.CurrentRecipe?.Id;
      if (recipeId == UnfilteredRecipeId)
      {
        return 1f;
      }
      float contamination = waterInput.ContaminationPercentage;
      if (recipeId == BadwaterGoodId)
      {
        return contamination < EfficiencyThreshold ? 0f : contamination;
      }
      if (recipeId == WaterGoodId)
      {
        float clean = 1f - contamination;
        return clean < EfficiencyThreshold ? 0f : clean;
      }
      return 0f;
    }

    internal static float MaxProductionProgressChange(Manufactory manufactory, float expectedProductionProgressChange)
    {
      WaterInput waterInput = manufactory.GetComponent<WaterInput>();
      if (waterInput == null)
      {
        return 0f;
      }
      string recipeId = manufactory.CurrentRecipe?.Id;
      float needed = WaterPerGood * expectedProductionProgressChange;
      if (recipeId == UnfilteredRecipeId)
      {
        float contamination = waterInput.ContaminationPercentage;
        float cleanNeeded = needed * (1f - contamination);
        float dirtyNeeded = needed - cleanNeeded;
        float available = 0f;
        if (cleanNeeded > 0f)
        {
          available += waterInput.DemandCleanWaterAmount(cleanNeeded);
        }
        if (dirtyNeeded > 0f)
        {
          available += waterInput.DemandContaminatedWaterAmount(dirtyNeeded);
        }
        return available / WaterPerGood;
      }
      if (recipeId == BadwaterGoodId)
      {
        return waterInput.DemandContaminatedWaterAmount(needed) / WaterPerGood;
      }
      if (recipeId == WaterGoodId)
      {
        return waterInput.DemandCleanWaterAmount(needed) / WaterPerGood;
      }
      return 0f;
    }

    internal static void ConsumeUnfiltered(Manufactory manufactory, float productionProgressChange)
    {
      WaterInput waterInput = manufactory.GetComponent<WaterInput>();
      if (waterInput == null)
      {
        return;
      }
      float amount = WaterPerGood * productionProgressChange;
      float contamination = waterInput.ContaminationPercentage;
      float clean = amount * (1f - contamination);
      float dirty = amount - clean;
      if (clean > 0f)
      {
        waterInput.RemoveCleanWater(clean);
      }
      if (dirty > 0f)
      {
        waterInput.RemoveContaminatedWater(dirty);
      }
    }

    internal static bool HasUnfilteredProductSpace(Manufactory manufactory)
    {
      return HasGoodSpace(manufactory, WaterGoodId) || HasGoodSpace(manufactory, BadwaterGoodId);
    }

    internal static void FinishUnfiltered(Manufactory manufactory, Action<Manufactory> consumeFuel)
    {
      string goodId = ChooseUnfilteredProduct(manufactory);
      if (goodId != null)
      {
        manufactory.Inventory.GiveProduced(new GoodAmount(goodId, 1));
      }
      consumeFuel(manufactory);
      manufactory.ResetProductionProgress();
    }

    private static string ChooseUnfilteredProduct(Manufactory manufactory)
    {
      WaterInput waterInput = manufactory.GetComponent<WaterInput>();
      bool wantBadwater = waterInput != null && waterInput.ContaminationPercentage >= EfficiencyThreshold
        && (waterInput.ContaminationPercentage >= 1f - EfficiencyThreshold
          || UnityEngine.Random.value < waterInput.ContaminationPercentage);
      bool waterSpace = HasGoodSpace(manufactory, WaterGoodId);
      bool badwaterSpace = HasGoodSpace(manufactory, BadwaterGoodId);
      if (wantBadwater && badwaterSpace)
      {
        return BadwaterGoodId;
      }
      if (!wantBadwater && waterSpace)
      {
        return WaterGoodId;
      }
      if (waterSpace)
      {
        return WaterGoodId;
      }
      return badwaterSpace ? BadwaterGoodId : null;
    }

    private static bool HasGoodSpace(Manufactory manufactory, string goodId)
    {
      return manufactory.Inventory != null && manufactory.Inventory.HasUnreservedCapacity(new GoodAmount(goodId, 1));
    }
  }

  [HarmonyPatch(typeof(Manufactory), "ProductionEfficiency")]
  internal static class UnifiedDeepPumpProductionEfficiencyPatch
  {
    private static bool Prefix(Manufactory __instance, ref float __result)
    {
      if (!TidetailsUnifiedDeepPump.IsUnifiedPump(__instance))
      {
        return true;
      }
      __result = TidetailsUnifiedDeepPump.ProductionEfficiency(__instance);
      return false;
    }
  }

  [HarmonyPatch(typeof(Manufactory), "MaxProductionProgressChange")]
  internal static class UnifiedDeepPumpMaxProductionProgressChangePatch
  {
    private static bool Prefix(Manufactory __instance, float expectedProductionProgressChange, ref float __result)
    {
      if (!TidetailsUnifiedDeepPump.IsUnifiedPump(__instance))
      {
        return true;
      }
      __result = Mathf.Min(1f, TidetailsUnifiedDeepPump.MaxProductionProgressChange(__instance, expectedProductionProgressChange));
      return false;
    }
  }

  [HarmonyPatch(typeof(Manufactory), nameof(Manufactory.HasUnreservedCapacityForCurrentProducts))]
  internal static class UnifiedDeepPumpProductCapacityPatch
  {
    private static bool Prefix(Manufactory __instance, ref bool __result)
    {
      if (!TidetailsUnifiedDeepPump.IsUnfiltered(__instance))
      {
        return true;
      }
      __result = TidetailsUnifiedDeepPump.HasUnfilteredProductSpace(__instance);
      return false;
    }
  }

  [HarmonyPatch(typeof(Manufactory), "FinishProduction")]
  internal static class UnifiedDeepPumpFinishProductionPatch
  {
    private static readonly Action<Manufactory> ConsumeFuel =
      AccessTools.MethodDelegate<Action<Manufactory>>(AccessTools.DeclaredMethod(typeof(Manufactory), "ConsumeFuel"));

    private static readonly AccessTools.FieldRef<Manufactory, EventHandler> ProductionFinished =
      AccessTools.FieldRefAccess<Manufactory, EventHandler>(nameof(Manufactory.ProductionFinished));

    private static bool Prefix(Manufactory __instance)
    {
      if (!TidetailsUnifiedDeepPump.IsUnfiltered(__instance))
      {
        return true;
      }
      TidetailsUnifiedDeepPump.FinishUnfiltered(__instance, ConsumeFuel);
      ProductionFinished(__instance)?.Invoke(__instance, EventArgs.Empty);
      return false;
    }
  }

  [HarmonyPatch]
  internal static class UnifiedDeepPumpWaterConsumerProgressPatch
  {
    private static MethodBase TargetMethod()
    {
      return AccessTools.Method("Timberborn.WaterWorkshops.ManufactoryWaterConsumer:OnProductionProgressed");
    }

    private static bool Prefix(BaseComponent __instance, ProductionProgressedEventArgs e)
    {
      Manufactory manufactory = __instance.GetComponent<Manufactory>();
      if (!TidetailsUnifiedDeepPump.IsUnfiltered(manufactory))
      {
        return true;
      }
      TidetailsUnifiedDeepPump.ConsumeUnfiltered(manufactory, e.ProductionProgressChange);
      return false;
    }
  }

  [HarmonyPatch]
  internal static class UnifiedDeepPumpContaminationConsumerProgressPatch
  {
    private static MethodBase TargetMethod()
    {
      return AccessTools.Method("Timberborn.WaterWorkshops.ManufactoryWaterContaminationConsumer:OnProductionProgressed");
    }

    private static bool Prefix(BaseComponent __instance)
    {
      return !TidetailsUnifiedDeepPump.IsUnfiltered(__instance.GetComponent<Manufactory>());
    }
  }

  // Multiple recipes leave CurrentRecipe unset until ManufactoryTogglableRecipes
  // initializes. A water consumer that initializes first calls UpdateRecipe and
  // reads Products on that null recipe.
  [HarmonyPatch]
  internal static class ManufactoryWaterConsumerUpdateRecipePatch
  {
    private static MethodBase TargetMethod()
    {
      return AccessTools.Method("Timberborn.WaterWorkshops.ManufactoryWaterConsumer:UpdateRecipe");
    }

    private static bool Prefix(BaseComponent __instance)
    {
      Manufactory manufactory = __instance.GetComponent<Manufactory>();
      return manufactory != null && manufactory.CurrentRecipe != null;
    }
  }

  [HarmonyPatch]
  internal static class ManufactoryWaterContaminationConsumerUpdateRecipePatch
  {
    private static MethodBase TargetMethod()
    {
      return AccessTools.Method("Timberborn.WaterWorkshops.ManufactoryWaterContaminationConsumer:UpdateRecipe");
    }

    private static bool Prefix(BaseComponent __instance)
    {
      Manufactory manufactory = __instance.GetComponent<Manufactory>();
      return manufactory != null && manufactory.CurrentRecipe != null;
    }
  }

  // Recipe icons composite all products, so Unfiltered (Water + Badwater) looks wrong.
  // Use the same CSS class / sprite as the Deep Mechanical Pump Fluid toggle.
  [HarmonyPatch]
  internal static class UnifiedDeepPumpRecipeSliderPatch
  {
    private const string UnfilteredIconClass = "water-mover-toggle__icon--unfiltered";

    private static MethodBase TargetMethod()
    {
      return AccessTools.Method("Timberborn.WorkshopsUI.ManufactoryRecipeSliderToggleFactory:Create");
    }

    private static bool Prefix(object __instance, VisualElement parent, Manufactory manufactory, ref SliderToggle __result)
    {
      if (!TidetailsUnifiedDeepPump.IsUnifiedPump(manufactory))
      {
        return true;
      }
      Type factoryType = __instance.GetType();
      ILoc loc = (ILoc)AccessTools.Field(factoryType, "_loc").GetValue(__instance);
      SliderToggleFactory sliderToggleFactory = (SliderToggleFactory)AccessTools.Field(factoryType, "_sliderToggleFactory").GetValue(__instance);
      __result = sliderToggleFactory.Create(parent, CreateItems(loc, manufactory));
      return false;
    }

    private static SliderToggleItem[] CreateItems(ILoc loc, Manufactory manufactory)
    {
      List<SliderToggleItem> items = new List<SliderToggleItem>();
      foreach (RecipeSpec recipe in GetRecipes(manufactory))
      {
        RecipeSpec selected = recipe;
        if (selected.Id == TidetailsUnifiedDeepPump.UnfilteredRecipeId)
        {
          items.Add(SliderToggleItem.Create(
            () => loc.T(selected.DisplayLocKey),
            UnfilteredIconClass,
            () => manufactory.SetRecipe(selected),
            () => manufactory.CurrentRecipe == selected));
        }
        else
        {
          items.Add(SliderToggleItem.Create(
            () => loc.T(selected.DisplayLocKey),
            GetRecipeIcon(selected),
            () => manufactory.SetRecipe(selected),
            () => manufactory.CurrentRecipe == selected));
        }
      }
      return items.ToArray();
    }

    private static Sprite GetRecipeIcon(RecipeSpec recipe)
    {
      object uiIcon = AccessTools.Property(typeof(RecipeSpec), "UIIcon").GetValue(recipe);
      if (uiIcon == null)
      {
        return null;
      }
      return AccessTools.Property(uiIcon.GetType(), "Value").GetValue(uiIcon) as Sprite;
    }

    private static IEnumerable<RecipeSpec> GetRecipes(Manufactory manufactory)
    {
      object recipes = AccessTools.Property(typeof(Manufactory), "ProductionRecipes").GetValue(manufactory);
      foreach (RecipeSpec recipe in (IEnumerable)recipes)
      {
        yield return recipe;
      }
    }
  }
}
