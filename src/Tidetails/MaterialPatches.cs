using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace Tidetails
{
  [HarmonyPatch]
  internal static class MaterialRepositoryLoadPatch
  {
    private static readonly string[] ColorProperties = { "_BaseColor", "_Color" };

    private static readonly (string Source, string Name, Color Color)[] Materials =
    {
      ("Blockage", "Tidetails.MudBlock", new Color(0.55f, 0.38f, 0.24f, 1f)),
      ("Oak", "Tidetails.LiveOak", new Color(0.42f, 0.55f, 0.28f, 1f)),
      ("Moss.Folktails", "Tidetails.SpanishMoss", new Color(0.78f, 0.80f, 0.62f, 1f)),
      ("Mangrove", "Tidetails.BaldCypress", new Color(0.35f, 0.48f, 0.32f, 1f)),
      ("Maple", "Tidetails.WaterTupelo", new Color(0.48f, 0.40f, 0.22f, 1f)),
    };

    private static MethodBase TargetMethod()
    {
      return AccessTools.Method("Timberborn.TimbermeshMaterials.MaterialRepository:Load");
    }

    private static void Postfix(Dictionary<string, Material> ____materials)
    {
      foreach ((string sourceName, string name, Color color) in Materials)
      {
        if (____materials.ContainsKey(name) || !____materials.TryGetValue(sourceName, out Material source))
        {
          continue;
        }
        Material material = new Material(source) { name = name };
        foreach (string property in ColorProperties)
        {
          if (material.HasProperty(property))
          {
            material.SetColor(property, color);
          }
        }
        ____materials.Add(name, material);
      }
    }
  }
}
