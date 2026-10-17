using System.Collections.Generic;
using Timberborn.Goods;
using Timberborn.InventorySystem;
using Timberborn.TemplateInstantiation;

namespace Tidetails
{
  internal class RomanceRetreatInventoryInitializer : IDedicatedDecoratorInitializer<RomanceRetreat, Inventory>
  {
    private readonly InventoryInitializerFactory _inventoryInitializerFactory;

    public RomanceRetreatInventoryInitializer(InventoryInitializerFactory inventoryInitializerFactory)
    {
      _inventoryInitializerFactory = inventoryInitializerFactory;
    }

    public void Initialize(RomanceRetreat subject, Inventory decorator)
    {
      List<StorableGoodAmount> goods = new List<StorableGoodAmount>
      {
        new StorableGoodAmount(StorableGood.CreateAsGivable("Water"), 2),
        new StorableGoodAmount(StorableGood.CreateAsGivable("Berries"), 2)
      };
      InventoryInitializer initializer = _inventoryInitializerFactory.Create(decorator, 4, "RomanceRetreat");
      initializer.AddAllowedGoods(goods);
      initializer.Initialize();
      subject.InitializeInventory(decorator);
    }
  }
}
