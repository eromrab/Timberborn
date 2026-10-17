using HarmonyLib;
using Timberborn.ModManagerScene;

namespace Tidetails
{
  public class TidetailsModStarter : IModStarter
  {
    public void StartMod(IModEnvironment modEnvironment)
    {
      new Harmony("Tidetails").PatchAll();
    }
  }
}
