using Timberborn.BaseComponentSystem;
using Timberborn.WalkingSystem;

namespace Tidetails
{
  internal class TidetailsWaterAffinity : BaseComponent, IAwakableComponent, IWaterPenaltyModifier
  {
    private TidetailsWaterAffinitySpec _spec;

    public float WaterPenaltyModifier => _spec?.WaterSpeedMultiplier ?? 1f;

    public void Awake()
    {
      _spec = GetComponent<TidetailsWaterAffinitySpec>();
    }
  }
}
