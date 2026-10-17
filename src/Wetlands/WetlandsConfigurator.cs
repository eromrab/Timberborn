using Bindito.Core;
using Timberborn.BlockSystem;
using Timberborn.ConstructionSites;
using Timberborn.TemplateCollectionSystem;
using Timberborn.TemplateInstantiation;

namespace Wetlands
{
  [Context("Game")]
  internal class WetlandsConfigurator : Configurator
  {
    private class WetlandsTemplateModuleProvider : IProvider<TemplateModule>
    {
      public TemplateModule Get()
      {
        TemplateModule.Builder builder = new TemplateModule.Builder();
        builder.AddDecorator<WetlandConstructionSpec, WetlandConstruction>();
        builder.AddDecorator<WetlandConstruction, DeleteOnFinishConstructionSite>();
        return builder.Build();
      }
    }

    protected override void Configure()
    {
      Bind<Vector3IntSerializer>().AsSingleton();
      Bind<WetlandCattailRenderer>().AsSingleton();
      Bind<WetlandService>().AsSingleton();
      Bind<WetlandConstruction>().AsTransient();
      MultiBind<ITemplateCollectionIdProvider>().To<WetlandsTemplateCollectionIdProvider>().AsSingleton();
      MultiBind<IBlockObjectValidator>().To<WetlandPlacementValidator>().AsSingleton();
      MultiBind<TemplateModule>().ToProvider<WetlandsTemplateModuleProvider>().AsSingleton();
    }
  }
}
