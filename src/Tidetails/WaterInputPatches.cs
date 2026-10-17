using System;
using HarmonyLib;
using Timberborn.WaterBuildings;
using UnityEngine;

namespace Tidetails
{
  // Lets the intake draw from water at its own level when the tile below is solid, instead of being obstructed.
  [HarmonyPatch(typeof(WaterInputPipeCoordinates), "UpdateCoordinatesAndDepth")]
  internal static class WaterInputPipeCoordinatesUpdatePatch
  {
    private static readonly Action<WaterInputPipeCoordinates, int> SetDepth =
      AccessTools.MethodDelegate<Action<WaterInputPipeCoordinates, int>>(AccessTools.PropertySetter(typeof(WaterInputPipeCoordinates), nameof(WaterInputPipeCoordinates.Depth)));

    private static readonly AccessTools.FieldRef<WaterInputPipeCoordinates, EventHandler<Vector3Int>> CoordinatesChanged =
      AccessTools.FieldRefAccess<WaterInputPipeCoordinates, EventHandler<Vector3Int>>(nameof(WaterInputPipeCoordinates.CoordinatesChanged));

    private static void Postfix(WaterInputPipeCoordinates __instance)
    {
      if (!TidetailsFactionState.IsActive || __instance.Depth != 0 || (__instance.UseDepthLimit && __instance.DepthLimit == 0))
      {
        return;
      }
      SetDepth(__instance, 1);
      CoordinatesChanged(__instance)?.Invoke(__instance, __instance.Coordinates);
    }
  }
}
