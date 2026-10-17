using Timberborn.GameFactionSystem;
using Timberborn.SingletonSystem;

namespace Tidetails
{
  internal class TidetailsFactionState : ILoadableSingleton, IUnloadableSingleton
  {
    public static readonly string FactionId = "Tidetails";

    private static TidetailsFactionState _instance;

    private readonly FactionService _factionService;

    // Read by Harmony patches, which have no access to dependency injection.
    public static bool IsActive => _instance?._factionService.Current?.Id == FactionId;

    public TidetailsFactionState(FactionService factionService)
    {
      _factionService = factionService;
      _instance = this;
    }

    public void Load()
    {
      _instance = this;
    }

    public void Unload()
    {
      if (_instance == this)
      {
        _instance = null;
      }
    }
  }
}
