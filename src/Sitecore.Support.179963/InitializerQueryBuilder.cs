using Sitecore.ContentSearch;
using Sitecore.Pipelines;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Sitecore.Support
{
    public class InitializerQueryBuilder
    {
        public void Process(PipelineArgs args)
        {
            Sitecore.ContentSearch.Utilities.QueryBuilder queryBuilder = new Sitecore.ContentSearch.Utilities.QueryBuilder();
            ContentSearchManager.Locator.UnRegister(queryBuilder.GetType());
            Sitecore.ContentSearch.Utilities.QueryBuilder newQueryBuilder = new QueryBuilder();
            ContentSearchManager.Locator.Register<Sitecore.ContentSearch.Utilities.QueryBuilder>(c => newQueryBuilder);
        }
    }
}