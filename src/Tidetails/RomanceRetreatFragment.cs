using Timberborn.BaseComponentSystem;
using Timberborn.EntityPanelSystem;
using Timberborn.InventorySystemUI;
using UnityEngine.UIElements;

namespace Tidetails
{
  internal class RomanceRetreatFragment : IEntityPanelFragment
  {
    private readonly InventoryFragmentBuilderFactory _inventoryFragmentBuilderFactory;

    private InventoryFragment _inventoryFragment;

    public RomanceRetreatFragment(InventoryFragmentBuilderFactory inventoryFragmentBuilderFactory)
    {
      _inventoryFragmentBuilderFactory = inventoryFragmentBuilderFactory;
    }

    public VisualElement InitializeFragment()
    {
      VisualElement root = new VisualElement();
      _inventoryFragment = _inventoryFragmentBuilderFactory.CreateBuilder(root)
        .ShowEmptyRows()
        .ShowRowLimit()
        .Build();
      VisualElement inventory = root.Q("InventoryFragment");
      inventory.AddToClassList("bg-sub-box--green");
      inventory.AddToClassList("entity-sub-panel");
      return root;
    }

    public void ShowFragment(BaseComponent entity)
    {
      _inventoryFragment.ClearFragment();
      RomanceRetreat retreat = entity.GetComponent<RomanceRetreat>();
      if (retreat != null && retreat.Inventory != null)
      {
        _inventoryFragment.ShowFragment(retreat.Inventory);
      }
    }

    public void ClearFragment()
    {
      _inventoryFragment.ClearFragment();
      UpdateFragment();
    }

    public void UpdateFragment()
    {
      _inventoryFragment.UpdateFragment();
    }
  }
}
