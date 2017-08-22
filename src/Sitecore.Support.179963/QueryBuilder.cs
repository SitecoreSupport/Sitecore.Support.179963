using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Sitecore;
using Sitecore.Abstractions;
using Sitecore.ContentSearch;
using Sitecore.ContentSearch.Linq.Common;
using Sitecore.ContentSearch.Pipelines.GetGlobalFilters;
using Sitecore.ContentSearch.Pipelines.StripQueryStringParameters;
using Sitecore.ContentSearch.Pipelines.TranslateQuery;
using Sitecore.ContentSearch.Utilities;
using Sitecore.Data;
using Sitecore.Data.Items;
using Sitecore.Diagnostics;
using Sitecore.Reflection;

namespace Sitecore.Support
{
    public class QueryBuilder : Sitecore.ContentSearch.Utilities.QueryBuilder
    {
        private readonly string DateCompareIdentifier = "#datecompare#";

        public override IQueryable<T> CreateQuery<T>(IProviderSearchContext context, IEnumerable<SearchStringModel> searchStringModel, Item startLocationItem, IEnumerable<IExecutionContext> additionalContexts = null)
        {
            ICorePipeline instance1 = context.Index.Locator.GetInstance<ICorePipeline>();
            Abstractions.IFactory instance2 = context.Index.Locator.GetInstance<Abstractions.IFactory>();
            List<Item> objList = new List<Item>();
            Database database = Context.ContentDatabase ?? Context.Database;
            if (database == null && startLocationItem != null)
                database = startLocationItem.Database;
            if (database == null)
                database = instance2.GetDatabase("master");
            Assert.IsNotNull((object)database, "Content Database cannot be resolved");
            Item rootItem = database.GetItem(Sitecore.ContentSearch.Utilities.Constants.BucketSearchType);
            if (rootItem != null)
                objList = rootItem.GetDescendantsByTemplateWithFallback(Sitecore.ContentSearch.Utilities.Constants.SearchCommandTemplateID).ToList<Item>();
            searchStringModel = searchStringModel.Distinct<SearchStringModel>((IEqualityComparer<SearchStringModel>)new LinqHelper.DistinctSearchComparer());
            HashSet<SearchStringModel> searchStringModelSet = searchStringModel.ToHashSet<SearchStringModel>();
            List<Item> list1 = objList.Where<Item>((Func<Item, bool>)(x =>
            {
                if (searchStringModelSet.Any<SearchStringModel>((Func<SearchStringModel, bool>)(y => y.Type == x.Fields["Name"].Value)))
                    return x.Fields["Control Type"].Value == SearchTypeConstants.Calendar;
                return false;
            })).ToList<Item>();
            Dictionary<string, Tuple<DateTime, DateTime, string>> dictionary = new Dictionary<string, Tuple<DateTime, DateTime, string>>();
            foreach (Item obj in list1)
            {
                string key = obj.Fields["Field"].Value;
                string searchTypeName = obj.Fields["Name"].Value;
                string str = obj.Fields["Control Type Parameters"].Value;
                SearchStringModel searchStringModel1 = searchStringModelSet.First<SearchStringModel>((Func<SearchStringModel, bool>)(x => x.Type == searchTypeName));
                searchStringModelSet.RemoveWhere((Predicate<SearchStringModel>)(x => x.Type == searchTypeName));
                DateTime result1 = DateTime.MinValue;
                DateTime result2 = DateTime.MaxValue;
                if (!(str == "start"))
                {
                    if (str == "end")
                    {
                        DateTime.TryParse(searchStringModel1.Value, (IFormatProvider)this.GetContextCultureInfo(), DateTimeStyles.None, out result2);
                    }
                    else
                    {
                        DateTime.TryParse(searchStringModel1.Value, (IFormatProvider)this.GetContextCultureInfo(), DateTimeStyles.None, out result1);
                        result2 = result1;
                    }

                    if (result2 <= DateTime.MaxValue.AddDays(-1))
                    {
                        result2 = result2.AddDays(1.0).Subtract(new TimeSpan(0, 0, 0, 1));
                    }
                }
                else
                {
                    DateTime.TryParse(searchStringModel1.Value, (IFormatProvider)this.GetContextCultureInfo(), DateTimeStyles.None, out result1);
                }

                if (dictionary.ContainsKey(key))
                {
                    result1 = dictionary[key].Item1 > result1 ? dictionary[key].Item1 : result1;
                    result2 = dictionary[key].Item2 < result2 ? dictionary[key].Item2 : result2;
                    dictionary.Remove(key);
                }
                dictionary.Add(key, new Tuple<DateTime, DateTime, string>(result1, result2, searchStringModel1.Operation));
            }
            foreach (KeyValuePair<string, Tuple<DateTime, DateTime, string>> keyValuePair in dictionary)
            {
                string fieldFormat = FieldFormat.GetFieldFormat(context.Index, keyValuePair.Key, typeof(DateTime));
                DateTime universalTime1 = DateUtil.ToUniversalTime(keyValuePair.Value.Item1);
                DateTime universalTime2 = DateUtil.ToUniversalTime(keyValuePair.Value.Item2);
                string str = string.Format("{2}[{0} TO {1}]", (object)universalTime1.ToString(fieldFormat), (object)universalTime2.ToString(fieldFormat), (object)this.DateCompareIdentifier);
                searchStringModelSet.Add(new SearchStringModel(keyValuePair.Key, str, keyValuePair.Value.Item3));
            }
            bool restrictPath = searchStringModelSet.All<SearchStringModel>((Func<SearchStringModel, bool>)(searchString =>
            {
                if (!(searchString.Type != "location"))
                    return searchString.Operation.ToLowerInvariant().Contains("not");
                return true;
            }));
            HashSet<SearchStringModel> searchStringModelSet1 = new HashSet<SearchStringModel>();
            foreach (SearchStringModel searchStringModel1 in searchStringModelSet)
            {
                SearchStringModel stringModel = searchStringModel1;
                if (stringModel.Type == "custom")
                {
                    string type = stringModel.Value.Split('|')[0];
                    if (stringModel.Value.Split('|').Length > 1)
                    {
                        string str = stringModel.Value.Split('|')[1];
                        if (str.IsGuid())
                            str = IdHelper.NormalizeGuid(str, true);
                        if (stringModel.Operation == null)
                            stringModel.Operation = "must";
                        searchStringModelSet1.Add(new SearchStringModel(type, str, stringModel.Operation));
                    }
                }
                else
                {
                    if (stringModel.Value.IsGuid())
                        stringModel.Value = IdHelper.NormalizeGuid(stringModel.Value, true);
                    if (objList.Any<Item>())
                    {
                        Item[] array = objList.Where<Item>((Func<Item, bool>)(x => x.Fields["Name"].Value == stringModel.Type)).ToArray<Item>();
                        if (array.Length == 1)
                        {
                            string str = array[0].Fields["Field"].Value;
                            stringModel.Type = str.Replace(",", "|");
                        }
                    }
                    if ((stringModel.Value == "*" || stringModel.Value == string.Empty) && (stringModel.Type == "_content|_name" || stringModel.Type == "_name|_content"))
                        stringModel.Type = "*";
                }
            }
            searchStringModelSet.UnionWith((IEnumerable<SearchStringModel>)searchStringModelSet1);
            searchStringModelSet.RemoveWhere((Predicate<SearchStringModel>)(x => x.Type == "custom"));
            searchStringModelSet = TranslateQueryPipeline.Run(instance1, new TranslateQueryArgs(context, (IIndexable)(SitecoreIndexableItem)startLocationItem, (IEnumerable<SearchStringModel>)searchStringModelSet)).ToHashSet<SearchStringModel>();
            searchStringModelSet = StripQueryStringParametersPipeline.Run(instance1, new StripQueryStringParametersArgs((IIndexable)(SitecoreIndexableItem)startLocationItem, (IEnumerable<SearchStringModel>)searchStringModelSet)).ToHashSet<SearchStringModel>();
            List<Tuple<CulturePredicateType, string>> tupleList = this.RetriveParsedLanguages(searchStringModelSet);
            searchStringModelSet = this.RemoveParsedLanguagesFromModel(searchStringModelSet);
            IEnumerable<IGrouping<string, SearchStringModel>> list2 = (IEnumerable<IGrouping<string, SearchStringModel>>)searchStringModelSet.GroupBy<SearchStringModel, string>((Func<SearchStringModel, string>)(item => item.Operation)).ToList<IGrouping<string, SearchStringModel>>();
            IQueryable<T> queryable1 = (IQueryable<T>)null;
            foreach (Tuple<CulturePredicateType, string> parsedLanguage in tupleList)
            {
                List<IExecutionContext> executionContextList = new List<IExecutionContext>();
                if (additionalContexts != null)
                    executionContextList.AddRange(additionalContexts);
                IQueryable<T> q1 = this.ApplyCultureInfo<T>(context.GetQueryable<T>(executionContextList.ToArray()), parsedLanguage);
                IQueryable<T> q2 = this.BuildNotPredicate<T>(context, q1, objList, list2);
                IQueryable<T> q3 = this.BuildMustPredicate<T>(context, q2, objList, list2);
                IQueryable<T> searchResultItems = this.BuildShouldPredicate<T>(context, q3, objList, list2);
                IQueryable<T> queryable2 = this.ApplySorting<T>(context, searchResultItems, (IEnumerable<SearchStringModel>)searchStringModelSet);
                IQueryable<T> source1 = GetGlobalFiltersPipeline.Run<T>(instance1, new GetGlobalFiltersArgs((IQueryable<object>)queryable2, typeof(T), (IIndexable)(SitecoreIndexableItem)startLocationItem, restrictPath));
                queryable1 = queryable1 != null ? Queryable.Union<T>(source1, (IEnumerable<T>)queryable1) : source1;
            }
            return queryable1;
        }
    }
}