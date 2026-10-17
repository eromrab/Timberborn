using HarmonyLib;
using Timberborn.ModManagerScene;

namespace Wetlands
{
  public class WetlandsModStarter : IModStarter
  {
    public void StartMod(IModEnvironment modEnvironment)
    {
      new Harmony("Wetlands").PatchAll();
    }
  }
}
