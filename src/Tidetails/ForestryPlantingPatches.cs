using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Timberborn.BottomBarSystem;
using Timberborn.Forestry;
using Timberborn.NaturalResources;
using Timberborn.Planting;
using Timberborn.PlantingUI;
using Timberborn.TemplateSystem;
using Timberborn.ToolButtonSystem;
using Timberborn.ToolSystem;

namespace Tidetails
{
  // Forestry lists every tree, then looks up a planter with Single().
  // Succulent is moved off the Forester group, so that lookup throws while loading a save.
  [HarmonyPatch]
  internal static class ForestryButtonGetElementsPatch
  {
    private static MethodBase TargetMethod()
    {
      return AccessTools.Method("Timberborn.ForestryUI.ForestryButton:GetElements");
    }

    private static bool Prefix(object __instance, ref IEnumerable<BottomBarElement> __result)
    {
      __result = BuildElements(__instance);
      return false;
    }

    private static IEnumerable<BottomBarElement> BuildElements(object instance)
    {
      Traverse traverse = Traverse.Create(instance);
      ToolGroupButtonFactory toolGroupButtonFactory = traverse.Field("_toolGroupButtonFactory").GetValue<ToolGroupButtonFactory>();
      TemplateService templateService = traverse.Field("_templateService").GetValue<TemplateService>();
      PlantingToolButtonFactory plantingToolButtonFactory = traverse.Field("_plantingToolButtonFactory").GetValue<PlantingToolButtonFactory>();
      ToolGroupService toolGroupService = traverse.Field("_toolGroupService").GetValue<ToolGroupService>();

      HashSet<string> planterGroups = new HashSet<string>(
        templateService.GetAll<PlanterBuildingSpec>().Select(building => building.PlantableResourceGroup));

      ToolGroupSpec toolGroup = toolGroupService.GetGroup("Forestry");
      ToolGroupButton toolGroupButton = toolGroupButtonFactory.CreateBlue(toolGroup);
      List<PlantableSpec> source = templateService.GetAll<PlantableSpec>()
        .Where(plantable => plantable.GetSpec<NaturalResourceSpec>().UsableWithCurrentFeatureToggles)
        .OrderBy(plantable => plantable.GetSpec<NaturalResourceSpec>().Order)
        .ToList();
      IEnumerable<PlantableSpec> plantables = source
        .Where(template => template.HasSpec<BushSpec>())
        .Concat(source.Where(template => template.HasSpec<TreeComponentSpec>()))
        .Where(template => planterGroups.Contains(template.ResourceGroup));

      foreach (PlantableSpec plantable in plantables)
      {
        ToolButton toolButton = plantingToolButtonFactory.CreatePlantingTool(plantable, toolGroupButton.ToolButtonsElement);
        toolGroupButton.AddTool(toolButton);
        toolGroupService.AssignToGroup(toolGroup, toolButton.Tool);
      }

      ToolButton cancelButton = plantingToolButtonFactory.CreateCancelTool(toolGroupButton.ToolButtonsElement);
      toolGroupService.AssignToGroup(toolGroup, cancelButton.Tool);
      toolGroupButton.AddTool(cancelButton);
      yield return BottomBarElement.CreateMultiLevel(toolGroupButton.Root, toolGroupButton.ToolButtonsElement);
    }
  }
}
