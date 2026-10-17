using Timberborn.BlueprintSystem;

namespace Tidetails
{
  public record TidetailsWetPathSpec : ComponentSpec
  {
    [Serialize]
    public float WetCostMultiplier { get; init; }
  }
}
