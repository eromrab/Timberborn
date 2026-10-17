using System.Collections.Generic;
using System.IO;
using HarmonyLib;
using Timberborn.AssetSystem;
using Timberborn.BlockSystem;
using Timberborn.Coordinates;
using Timberborn.LevelVisibilitySystem;
using Timberborn.Timbermesh;
using UnityEngine;

namespace Wetlands
{
  public class WetlandCattailRenderer
  {
    private const string SeedlingModel = "NaturalResources/Crops/Cattail/CattailSeedling";

    private const string CattailMaterialName = "Cattail";

    private const string CattailMaterialPath = "NaturalResources/Crops/Cattail/Materials/Cattail";

    private const int PlantsPerTile = 2;

    private readonly IAssetLoader _assetLoader;

    private readonly TimbermeshImporter _timbermeshImporter;

    private readonly IBlockService _blockService;

    private readonly ILevelVisibilityService _levelVisibilityService;

    private readonly Dictionary<Vector3Int, GameObject> _tiles = new Dictionary<Vector3Int, GameObject>();

    private GameObject _prefab;

    private GameObject _root;

    public WetlandCattailRenderer(
      IAssetLoader assetLoader,
      TimbermeshImporter timbermeshImporter,
      IBlockService blockService,
      ILevelVisibilityService levelVisibilityService)
    {
      _assetLoader = assetLoader;
      _timbermeshImporter = timbermeshImporter;
      _blockService = blockService;
      _levelVisibilityService = levelVisibilityService;
    }

    public void Refresh(ICollection<Vector3Int> tiles)
    {
      if (!EnsurePrefab())
      {
        return;
      }
      List<Vector3Int> removed = null;
      foreach (KeyValuePair<Vector3Int, GameObject> pair in _tiles)
      {
        if (tiles.Contains(pair.Key))
        {
          continue;
        }
        if (removed == null)
        {
          removed = new List<Vector3Int>();
        }
        removed.Add(pair.Key);
      }
      if (removed != null)
      {
        for (int i = 0; i < removed.Count; i++)
        {
          DestroyTile(removed[i]);
        }
      }
      foreach (Vector3Int coordinates in tiles)
      {
        GameObject tile = GetOrCreateTile(coordinates);
        bool visible = ShouldShow(coordinates);
        if (tile.activeSelf != visible)
        {
          tile.SetActive(visible);
        }
      }
    }

    public void Unload()
    {
      _tiles.Clear();
      if (_root != null)
      {
        Object.Destroy(_root);
        _root = null;
      }
      _prefab = null;
    }

    private bool ShouldShow(Vector3Int coordinates)
    {
      return WetlandSurfaceVisibility.ShouldShowPlants(coordinates, _blockService, _levelVisibilityService);
    }

    private bool EnsurePrefab()
    {
      if (_prefab != null)
      {
        return true;
      }
      BinaryData data;
      try
      {
        data = _assetLoader.Load<BinaryData>(SeedlingModel);
      }
      catch
      {
        return false;
      }
      if (data == null || data.Bytes == null)
      {
        return false;
      }
      EnsureCattailMaterial();
      _prefab = new GameObject("Wetlands.CattailPrefab");
      _prefab.SetActive(false);
      try
      {
        using (MemoryStream stream = new MemoryStream(data.Bytes))
        {
          _timbermeshImporter.Import(stream, _prefab.transform);
        }
      }
      catch (System.Exception exception)
      {
        Debug.LogWarning("Wetlands failed to import cattail seedling: " + exception.Message);
        UnityEngine.Object.Destroy(_prefab);
        _prefab = null;
        return false;
      }
      return true;
    }

    private void EnsureCattailMaterial()
    {
      // The seedling mesh names its material "Cattail". That asset is only in the
      // Folktails material collection, so other factions throw on import.
      object builder = AccessTools.Field(typeof(TimbermeshImporter), "_staticMeshBuilder").GetValue(_timbermeshImporter);
      if (builder == null)
      {
        return;
      }
      object repository = AccessTools.Field(builder.GetType(), "_materialRepository").GetValue(builder);
      if (repository == null)
      {
        return;
      }
      Dictionary<string, Material> materials = AccessTools.Field(repository.GetType(), "_materials").GetValue(repository) as Dictionary<string, Material>;
      if (materials == null || materials.ContainsKey(CattailMaterialName))
      {
        return;
      }
      Material material;
      try
      {
        material = _assetLoader.Load<Material>(CattailMaterialPath);
      }
      catch
      {
        return;
      }
      if (material == null)
      {
        return;
      }
      materials.Add(CattailMaterialName, material);
    }

    private GameObject GetOrCreateTile(Vector3Int coordinates)
    {
      if (_tiles.TryGetValue(coordinates, out GameObject existing) && existing != null)
      {
        return existing;
      }
      if (_root == null)
      {
        _root = new GameObject("Wetlands.Cattails");
      }
      GameObject tile = new GameObject("WetlandCattails " + coordinates);
      tile.transform.SetParent(_root.transform, false);
      tile.transform.position = CoordinateSystem.GridToWorld(coordinates + Vector3Int.forward);
      int hash = Mathf.Abs(coordinates.x * 73856093 ^ coordinates.y * 19349663 ^ coordinates.z * 83492791);
      for (int i = 0; i < PlantsPerTile; i++)
      {
        int plantHash = hash + i * 9973;
        GameObject plant = Object.Instantiate(_prefab, tile.transform);
        plant.name = "CattailSeedling";
        plant.SetActive(true);
        float ox = 0.28f + ((plantHash >> 3) % 45) / 100f;
        float oz = 0.28f + ((plantHash >> 9) % 45) / 100f;
        if (i == 1)
        {
          ox = 1f - ox;
          oz = 1f - oz;
        }
        float scale = 0.48f + (plantHash >> 7) % 22 / 100f;
        plant.transform.localPosition = new Vector3(ox, 0f, oz);
        plant.transform.localRotation = Quaternion.Euler(0f, plantHash % 360, 0f);
        plant.transform.localScale = new Vector3(scale, scale, scale);
      }
      _tiles[coordinates] = tile;
      return tile;
    }

    private void DestroyTile(Vector3Int coordinates)
    {
      if (!_tiles.TryGetValue(coordinates, out GameObject tile))
      {
        return;
      }
      _tiles.Remove(coordinates);
      if (tile != null)
      {
        Object.Destroy(tile);
      }
    }
  }
}
