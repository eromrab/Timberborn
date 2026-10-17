using Timberborn.BaseComponentSystem;
using Timberborn.BlockSystem;
using Timberborn.DeconstructionSystem;
using Timberborn.RecoverableGoodSystem;

namespace Wetlands
{
  public class WetlandConstruction : BaseComponent, IAwakableComponent, IFinishedStateListener
  {
    private readonly WetlandService _wetlandService;

    private BlockObject _blockObject;

    public WetlandConstruction(WetlandService wetlandService)
    {
      _wetlandService = wetlandService;
    }

    public void Awake()
    {
      _blockObject = GetComponent<BlockObject>();
    }

    public void OnEnterFinishedState()
    {
      GetComponent<RecoverableGoodProvider>()?.DisableGoodRecovery();
      GetComponent<Deconstructible>()?.DisableDeconstruction();
      _wetlandService.AddTile(_blockObject.Coordinates);
    }

    public void OnExitFinishedState()
    {
    }
  }
}
