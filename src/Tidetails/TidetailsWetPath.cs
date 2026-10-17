using System;
using System.Collections.Generic;
using HarmonyLib;
using Timberborn.BaseComponentSystem;
using Timberborn.BlockSystem;
using Timberborn.BlockSystemNavigation;
using Timberborn.BuildingsNavigation;
using Timberborn.Navigation;
using Timberborn.WaterSystem;
using Timberborn.WaterSystemRendering;
using UnityEngine;
using UnityEngine.Rendering;
using Wetlands;

namespace Tidetails
{
  internal class TidetailsWetPath : BaseComponent, IAwakableComponent, IFinishedStateListener
  {
    private static readonly AccessTools.FieldRef<BuildingNavMesh, bool> IsAddedToRegularNavMesh =
      AccessTools.FieldRefAccess<BuildingNavMesh, bool>("_isAddedToRegularNavMesh");

    // Same brown as MaterialPatches Tidetails.MudBlock (cloned from Blockage).
    private static readonly Color MudColor = new Color(0.55f, 0.38f, 0.24f, 1f);

    private static readonly Color InvalidPreviewColor = new Color(0.6f, 0f, 0f, 1f);

    // Path.IronTeeth has no _BaseColor; albedo is _MainColor / _DetailColor.
    private static readonly string[] ColorProperties =
    {
      "_BaseColor", "_Color", "_MainColor", "_DetailColor", "_TintColor", "_UserColor"
    };

    private readonly MaterialPropertyBlock _mudPropertyBlock = new MaterialPropertyBlock();

    private readonly MaterialPropertyBlock _invalidPreviewBlock = new MaterialPropertyBlock();

    private Material _mudMaterial;

    private readonly IThreadSafeWaterMap _threadSafeWaterMap;

    private readonly TidetailsWetPathService _wetPathService;

    private readonly WetlandService _wetlandService;

    private readonly WaterOpacityService _waterOpacityService;

    private readonly Dictionary<Renderer, Material> _originalMaterials = new Dictionary<Renderer, Material>();

    private TidetailsWetPathSpec _spec;

    private BlockObject _blockObject;

    private IBlockObjectNavMesh _blockObjectNavMesh;

    private BuildingNavMesh _buildingNavMesh;

    private bool _appearanceWet;

    private bool _appearanceSeeThrough;

    private bool _invalidPreviewTinted;

    public bool IsWet { get; private set; }

    public float CostMultiplier => IsWet ? _spec.WetCostMultiplier : 1f;

    public TidetailsWetPath(
      IThreadSafeWaterMap threadSafeWaterMap,
      TidetailsWetPathService wetPathService,
      WetlandService wetlandService,
      WaterOpacityService waterOpacityService)
    {
      _threadSafeWaterMap = threadSafeWaterMap;
      _wetPathService = wetPathService;
      _wetlandService = wetlandService;
      _waterOpacityService = waterOpacityService;
    }

    public void Awake()
    {
      _spec = GetComponent<TidetailsWetPathSpec>();
      _blockObject = GetComponent<BlockObject>();
      _blockObjectNavMesh = GetComponent<IBlockObjectNavMesh>();
      _buildingNavMesh = GetComponent<BuildingNavMesh>();
    }

    public void OnEnterFinishedState()
    {
      _wetPathService.Register(this);
      ApplyAppearance(HasWaterAbove());
    }

    public void OnExitFinishedState()
    {
      _wetPathService.Unregister(this);
      ApplyAppearance(false);
    }

    public void UpdateWetState()
    {
      IsWet = HasWaterAbove();
    }

    public void RefreshIfChanged()
    {
      bool wet = HasWaterAbove();
      ApplyAppearance(wet);
      if (IsWet == wet || _blockObjectNavMesh == null)
      {
        return;
      }
      if (_buildingNavMesh != null && !IsAddedToRegularNavMesh(_buildingNavMesh))
      {
        return;
      }
      NavMeshObject navMeshObject = _blockObjectNavMesh.NavMeshObject;
      navMeshObject.EnqueueRemoveFromRegularNavMesh();
      _blockObjectNavMesh.RecalculateNavMeshObject();
      navMeshObject.EnqueueAddToRegularNavMesh();
    }

    public void RefreshAppearance()
    {
      _appearanceWet = false;
      ApplyAppearance(HasWaterAbove());
    }

    public void RefreshAppearanceAfterModelChange()
    {
      _appearanceWet = false;
      ApplyAppearance(HasWaterAbove());
    }

    public void RefreshPreviewValidityTint()
    {
      Preview preview = GetComponent<Preview>();
      // Color follows whether the path can actually be placed. PreviewState can
      // stay unbuildable on a wetland tile after the block itself is allowed.
      bool rejectedByFactionRules = TidetailsPlacementValidator.Instance != null
        && !TidetailsPlacementValidator.Instance.IsValid(_blockObject, out _);
      bool invalid = TidetailsFactionState.IsActive
        && _blockObject.IsPreview
        && preview != null
        && (rejectedByFactionRules || !_blockObject.IsValid());
      TidetailsInvalidPreviewTiles.Update(_blockObject, invalid);
      if (invalid)
      {
        ApplyInvalidPreviewTint();
      }
      else
      {
        ClearInvalidPreviewTint();
      }
    }

    private void ApplyAppearance(bool wet)
    {
      bool seeThrough = _waterOpacityService.IsWaterTransparent;
      if (!wet || !seeThrough)
      {
        if (_appearanceWet)
        {
          RestoreOriginalMaterials();
        }
        _appearanceWet = false;
        _appearanceSeeThrough = false;
        return;
      }

      if (_appearanceWet && _appearanceSeeThrough)
      {
        return;
      }

      List<MeshRenderer> renderers = PathRenderers();
      Material mud = GetMudMaterial(renderers);
      if (mud == null)
      {
        return;
      }

      FillColorPropertyBlock(_mudPropertyBlock, mud, MudColor);
      foreach (MeshRenderer renderer in renderers)
      {
        if (!_originalMaterials.ContainsKey(renderer))
        {
          _originalMaterials[renderer] = renderer.sharedMaterial;
        }
        renderer.sharedMaterial = mud;
        renderer.SetPropertyBlock(_mudPropertyBlock);
      }

      _appearanceWet = true;
      _appearanceSeeThrough = true;
    }

    private Material GetMudMaterial(List<MeshRenderer> renderers)
    {
      Material source = null;
      foreach (MeshRenderer renderer in renderers)
      {
        if (_originalMaterials.TryGetValue(renderer, out Material original) && original != null && original != _mudMaterial)
        {
          source = original;
          break;
        }
        Material current = renderer.sharedMaterial;
        if (current != null && current != _mudMaterial)
        {
          source = current;
          break;
        }
      }
      if (source == null)
      {
        return _mudMaterial;
      }
      if (_mudMaterial == null || _mudMaterial.shader != source.shader)
      {
        _mudMaterial = new Material(source) { name = "Tidetails.WetPath" };
      }
      ApplyMudColor(_mudMaterial);
      return _mudMaterial;
    }

    private static void ApplyMudColor(Material material)
    {
      material.color = MudColor;
      Shader shader = material.shader;
      if (shader != null)
      {
        int count = shader.GetPropertyCount();
        for (int i = 0; i < count; i++)
        {
          if (shader.GetPropertyType(i) != ShaderPropertyType.Color)
          {
            continue;
          }
          string name = shader.GetPropertyName(i);
          if (name.IndexOf("Emission", StringComparison.OrdinalIgnoreCase) >= 0)
          {
            continue;
          }
          material.SetColor(name, MudColor);
        }
      }
      foreach (string property in ColorProperties)
      {
        if (material.HasColor(property) || material.HasProperty(property))
        {
          material.SetColor(property, MudColor);
        }
      }
    }

    private void ApplyInvalidPreviewTint()
    {
      List<MeshRenderer> renderers = PathRenderers();
      Material material = null;
      foreach (MeshRenderer renderer in renderers)
      {
        material = renderer.sharedMaterial;
        if (material != null)
        {
          break;
        }
      }
      FillColorPropertyBlock(_invalidPreviewBlock, material, InvalidPreviewColor);
      foreach (MeshRenderer renderer in renderers)
      {
        renderer.SetPropertyBlock(_invalidPreviewBlock);
      }
      _invalidPreviewTinted = true;
    }

    private void ClearInvalidPreviewTint()
    {
      if (!_invalidPreviewTinted)
      {
        return;
      }
      foreach (MeshRenderer renderer in PathRenderers())
      {
        renderer.SetPropertyBlock(_appearanceWet ? _mudPropertyBlock : null);
      }
      _invalidPreviewTinted = false;
    }

    private void FillColorPropertyBlock(MaterialPropertyBlock propertyBlock, Material material, Color color)
    {
      propertyBlock.Clear();
      propertyBlock.SetColor("_Color", color);
      propertyBlock.SetColor("_MainColor", color);
      propertyBlock.SetColor("_DetailColor", color);
      foreach (string property in ColorProperties)
      {
        propertyBlock.SetColor(property, color);
      }
      Shader shader = material != null ? material.shader : null;
      if (shader == null)
      {
        return;
      }
      int count = shader.GetPropertyCount();
      for (int i = 0; i < count; i++)
      {
        if (shader.GetPropertyType(i) != ShaderPropertyType.Color)
        {
          continue;
        }
        string name = shader.GetPropertyName(i);
        if (name.IndexOf("Emission", StringComparison.OrdinalIgnoreCase) >= 0)
        {
          continue;
        }
        propertyBlock.SetColor(name, color);
      }
    }

    private void RestoreOriginalMaterials()
    {
      foreach (MeshRenderer renderer in PathRenderers())
      {
        renderer.SetPropertyBlock(null);
        if (_originalMaterials.TryGetValue(renderer, out Material original))
        {
          renderer.sharedMaterial = original;
        }
      }
    }

    private List<MeshRenderer> PathRenderers()
    {
      List<MeshRenderer> renderers = new List<MeshRenderer>();
      foreach (MeshRenderer renderer in GameObject.GetComponentsInChildren<MeshRenderer>(true))
      {
        if (renderer.gameObject.name == "TidetailsWetPathShine")
        {
          continue;
        }
        if (!renderer.enabled && !renderer.gameObject.activeInHierarchy)
        {
          continue;
        }
        renderers.Add(renderer);
      }
      return renderers;
    }

    private bool HasWaterAbove()
    {
      if (!TidetailsFactionState.IsActive)
      {
        return false;
      }
      foreach (Block block in _blockObject.PositionedBlocks.GetOccupiedBlocks())
      {
        if (!block.Occupation.Intersects(BlockOccupations.Path))
        {
          continue;
        }
        if (CellIsWet(block.Coordinates) || _wetlandService.IsWetAt(block.Coordinates))
        {
          return true;
        }
      }
      return false;
    }

    private bool CellIsWet(Vector3Int coordinates)
    {
      return _threadSafeWaterMap.CeiledWaterHeight(coordinates) - coordinates.z > 0;
    }
  }
}
