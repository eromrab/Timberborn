using Timberborn.BlueprintSystem;

namespace Tidetails
{
  public record TidetailsWaterAffinitySpec : ComponentSpec
  {
    [Serialize]
    public float WaterSpeedMultiplier { get; init; }

    [Serialize]
    public float TubeWetFurPointsPerHour { get; init; }
  }
}
