using System;
using System.Reflection;
using HarmonyLib;
using Timberborn.BaseComponentSystem;
using Timberborn.Reproduction;
using UnityEngine;

namespace Tidetails
{
  [HarmonyPatch(typeof(BreedingPod), "Awake")]
  internal static class BreedingPodEmbryoPlaceholderPatch
  {
    private const string DefaultEmbryoName = "#EmbryoMisc";

    private static readonly Type BreedingPodSpecType = AccessTools.TypeByName("Timberborn.Reproduction.BreedingPodSpec");

    private static readonly MethodInfo GetComponentGeneric = AccessTools.Method(typeof(BaseComponent), "GetComponent");

    private static readonly PropertyInfo EmbryoNameProperty =
      BreedingPodSpecType != null ? AccessTools.Property(BreedingPodSpecType, "EmbryoName") : null;

    private static void Prefix(BaseComponent __instance)
    {
      string embryoName = ResolveEmbryoName(__instance);
      if (string.IsNullOrEmpty(embryoName))
      {
        return;
      }
      GameObject root = __instance.GameObject;
      if (FindChildRecursive(root.transform, embryoName) != null)
      {
        return;
      }
      GameObject embryo = new GameObject(embryoName);
      embryo.transform.SetParent(root.transform, false);
      embryo.transform.localPosition = new Vector3(1f, 0.35f, 1f);
      embryo.SetActive(false);
    }

    private static string ResolveEmbryoName(BaseComponent instance)
    {
      if (BreedingPodSpecType == null || GetComponentGeneric == null || EmbryoNameProperty == null)
      {
        return DefaultEmbryoName;
      }
      MethodInfo getSpec = GetComponentGeneric.MakeGenericMethod(BreedingPodSpecType);
      object spec = getSpec.Invoke(instance, null);
      if (spec == null)
      {
        return DefaultEmbryoName;
      }
      return EmbryoNameProperty.GetValue(spec) as string ?? DefaultEmbryoName;
    }

    private static Transform FindChildRecursive(Transform parent, string childName)
    {
      for (int i = 0; i < parent.childCount; i++)
      {
        Transform child = parent.GetChild(i);
        if (child.name == childName)
        {
          return child;
        }
        Transform nested = FindChildRecursive(child, childName);
        if (nested != null)
        {
          return nested;
        }
      }
      return null;
    }
  }
}
