using Bindito.Core;
using Timberborn.BlockSystem;
using Timberborn.EntityPanelSystem;
using Timberborn.TemplateInstantiation;

namespace Tidetails
{
  [Context("Game")]
  internal class TidetailsConfigurator : Configurator
  {
    private class RomanceRetreatPanelModuleProvider : IProvider<EntityPanelModule>
    {
      private readonly RomanceRetreatFragment _romanceRetreatFragment;

      public RomanceRetreatPanelModuleProvider(RomanceRetreatFragment romanceRetreatFragment)
      {
        _romanceRetreatFragment = romanceRetreatFragment;
      }

      public EntityPanelModule Get()
      {
        EntityPanelModule.Builder builder = new EntityPanelModule.Builder();
        builder.AddMiddleFragment(_romanceRetreatFragment, 20);
        return builder.Build();
      }
    }

    private class TidetailsTemplateModuleProvider : IProvider<TemplateModule>
    {
      private readonly RomanceRetreatInventoryInitializer _romanceRetreatInventoryInitializer;

      public TidetailsTemplateModuleProvider(RomanceRetreatInventoryInitializer romanceRetreatInventoryInitializer)
      {
        _romanceRetreatInventoryInitializer = romanceRetreatInventoryInitializer;
      }

      public TemplateModule Get()
      {
        TemplateModule.Builder builder = new TemplateModule.Builder();
        builder.AddDecorator<TidetailsWaterAffinitySpec, TidetailsWaterAffinity>();
        builder.AddDecorator<TidetailsWaterAffinitySpec, TidetailsTubeSoaker>();
        builder.AddDecorator<TidetailsWetPathSpec, TidetailsWetPath>();
        builder.AddDecorator<TidetailsGlassLidSpec, TidetailsGlassLid>();
        builder.AddDecorator<RomanceRetreatSpec, RomanceRetreat>();
        builder.AddDedicatedDecorator(_romanceRetreatInventoryInitializer);
        return builder.Build();
      }
    }

    protected override void Configure()
    {
      Bind<TidetailsFactionState>().AsSingleton();
      Bind<TidetailsWetPathService>().AsSingleton();
      Bind<TidetailsStartingLocationFinder>().AsSingleton();
      Bind<TidetailsWaterAffinity>().AsTransient();
      Bind<TidetailsTubeSoaker>().AsTransient();
      Bind<TidetailsWetPath>().AsTransient();
      Bind<TidetailsGlassLid>().AsTransient();
      Bind<RomanceRetreat>().AsTransient();
      Bind<RomanceRetreatInventoryInitializer>().AsSingleton();
      Bind<RomanceRetreatFragment>().AsSingleton();
      MultiBind<EntityPanelModule>().ToProvider<RomanceRetreatPanelModuleProvider>().AsSingleton();
      MultiBind<IBlockObjectValidator>().To<TidetailsPlacementValidator>().AsSingleton();
      MultiBind<TemplateModule>().ToProvider<TidetailsTemplateModuleProvider>().AsSingleton();
    }
  }
}
