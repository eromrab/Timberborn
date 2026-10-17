using Timberborn.BaseComponentSystem;
using Timberborn.Effects;
using Timberborn.NeedSystem;
using Timberborn.TickSystem;
using Timberborn.TimeSystem;
using Timberborn.WaterSystem;

namespace Tidetails
{
  internal class TidetailsTubeSoaker : TickableComponent, IAwakableComponent
  {
    private static readonly string WetFurNeedId = "WetFur";

    private readonly IDayNightCycle _dayNightCycle;

    private TidetailsWaterAffinitySpec _spec;

    private NeedManager _needManager;

    // Vanilla tubes report beavers inside as water resistant, which is exactly "in a tube".
    private IWaterResistor _waterResistor;

    public TidetailsTubeSoaker(IDayNightCycle dayNightCycle)
    {
      _dayNightCycle = dayNightCycle;
    }

    public void Awake()
    {
      _spec = GetComponent<TidetailsWaterAffinitySpec>();
      _needManager = GetComponent<NeedManager>();
      _waterResistor = GetComponent<IWaterResistor>();
    }

    public override void Tick()
    {
      if (_needManager != null && _waterResistor != null && _waterResistor.IsWaterResistant)
      {
        float points = _spec.TubeWetFurPointsPerHour * _dayNightCycle.FixedDeltaTimeInHours;
        InstantEffect effect = new InstantEffect(WetFurNeedId, points, 1);
        _needManager.ApplyEffect(in effect);
      }
    }
  }
}
