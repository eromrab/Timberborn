using System.Collections.Generic;
using HarmonyLib;
using Timberborn.BlockObjectTools;
using Timberborn.GameSceneLoading;
using Timberborn.SceneLoading;
using Timberborn.BlockSystem;
using Timberborn.Coordinates;
using Timberborn.SingletonSystem;
using Timberborn.StartingLocationSystem;
using Timberborn.TemplateSystem;
using Timberborn.TerrainSystem;
using UnityEngine;

namespace Tidetails
{
  internal class TidetailsStartingLocationFinder : IUnloadableSingleton
  {
    private const int MaxSearchRadius = 40;

    private static readonly Orientation[] Orientations = { Orientation.Cw0, Orientation.Cw90, Orientation.Cw180, Orientation.Cw270 };

    private static readonly Dictionary<string, Placement> MapStartingPlacements = new Dictionary<string, Placement>
    {
      { "Lakes", new Placement(new Vector3Int(100, 159, 2), Orientation.Cw0, FlipMode.Unflipped) },
    };

    private readonly StartingLocationService _startingLocationService;

    private readonly PreviewFactory _previewFactory;

    private readonly ITerrainService _terrainService;

    private readonly ISceneLoader _sceneLoader;

    // Read by Harmony patches, which have no access to dependency injection.
    public static TidetailsStartingLocationFinder Instance { get; private set; }

    public TidetailsStartingLocationFinder(StartingLocationService startingLocationService, PreviewFactory previewFactory, ITerrainService terrainService, ISceneLoader sceneLoader)
    {
      _startingLocationService = startingLocationService;
      _previewFactory = previewFactory;
      _terrainService = terrainService;
      _sceneLoader = sceneLoader;
      Instance = this;
    }

    public string MapName => _sceneLoader.GetSceneParameters<GameSceneParameters>()?.NewGameConfiguration?.MapFileReference.Name;

    public void LogPlacement(Placement placement)
    {
      Vector3Int coordinates = placement.Coordinates;
      Debug.Log($"[Tidetails] Starting building placed on map '{MapName}' at ({coordinates.x}, {coordinates.y}, {coordinates.z}), {placement.Orientation}");
    }

    public void Unload()
    {
      if (Instance == this)
      {
        Instance = null;
      }
    }

    public bool TryInitialize(object startingBuildingInitializer)
    {
      if (!_startingLocationService.HasStartingLocation())
      {
        return false;
      }
      Traverse initializer = Traverse.Create(startingBuildingInitializer);
      Traverse spawner = initializer.Field("_startingBuildingSpawner");
      TemplateSpec templateSpec = spawner.Property("StartingBuildingTemplateSpec").GetValue<TemplateSpec>();
      Placement original = _startingLocationService.GetPlacement();
      _startingLocationService.DeleteStartingLocations();
      string mapName = MapName;
      if (mapName != null && MapStartingPlacements.TryGetValue(mapName, out Placement mapPlacement))
      {
        original = mapPlacement;
      }
      Placement placement = FindValidPlacement(templateSpec.GetSpec<PlaceableBlockObjectSpec>(), original) ?? original;
      initializer.Property("InitialPlacement").SetValue((Placement?)placement);
      spawner.Method("Place", new[] { typeof(Placement?) }).GetValue((Placement?)placement);
      initializer.Method("SetCamera").GetValue();
      initializer.Method("Notify").GetValue();
      return true;
    }

    private Placement? FindValidPlacement(PlaceableBlockObjectSpec placeableBlockObjectSpec, Placement original)
    {
      Preview preview = _previewFactory.Create(placeableBlockObjectSpec);
      BlockObject blockObject = preview.BlockObject;
      try
      {
        if (IsValidAt(preview, blockObject, original))
        {
          return original;
        }
        Vector3Int center = original.Coordinates;
        for (int radius = 0; radius <= MaxSearchRadius; radius++)
        {
          for (int dx = -radius; dx <= radius; dx++)
          {
            for (int dy = -radius; dy <= radius; dy++)
            {
              if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)) != radius)
              {
                continue;
              }
              Vector2Int cell = new Vector2Int(center.x + dx, center.y + dy);
              if (!_terrainService.Contains(cell))
              {
                continue;
              }
              foreach (Vector3Int surface in _terrainService.GetAllHeightsInCell(cell))
              {
                foreach (Orientation orientation in Orientations)
                {
                  Placement candidate = new Placement(surface, orientation, FlipMode.Unflipped);
                  if (IsValidAt(preview, blockObject, candidate))
                  {
                    return candidate;
                  }
                }
              }
            }
          }
        }
        return null;
      }
      finally
      {
        Object.Destroy(preview.GameObject);
      }
    }

    private static bool IsValidAt(Preview preview, BlockObject blockObject, Placement placement)
    {
      preview.Reposition(placement);
      return blockObject.IsValid();
    }
  }
}
