using System.Collections.Generic;
using Timberborn.TemplateCollectionSystem;

namespace Wetlands
{
  internal class WetlandsTemplateCollectionIdProvider : ITemplateCollectionIdProvider
  {
    public IEnumerable<string> GetTemplateCollectionIds()
    {
      yield return "Buildings.Wetlands";
    }
  }
}
