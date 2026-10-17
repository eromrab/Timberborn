using HarmonyLib;
using Timberborn.ModManagerScene;

namespace BadwaterRush
{
  public class BadwaterRushModStarter : IModStarter
  {
    public void StartMod(IModEnvironment modEnvironment)
    {
      new Harmony("BadwaterRush").PatchAll();
    }
  }
}
