﻿using PluginCommon;
using PluginCommon.PlayniteResources;
using PluginCommon.PlayniteResources.API;
using PluginCommon.PlayniteResources.Common;
using PluginCommon.PlayniteResources.Converters;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Web;

namespace MetadataLocal
{
    public class WebStoreClient : IDisposable
    {
        private HttpClient httpClient = new HttpClient();

        public const string GraphQLEndpoint = @"https://graphql.epicgames.com/graphql";
        public const string ProductUrlBase = @"https://store-content.ak.epicgames.com/api/{1}/content/products/{0}";

        public WebStoreClient()
        {
        }

        public void Dispose()
        {
            httpClient.Dispose();
        }

        public async Task<List<WebStoreModelsAppsList.QuerySearchResponse.SearchStoreElement>> QuerySearch(string searchTerm)
        {
            var query = new WebStoreModelsAppsList.QuerySearch();
            query.variables.keywords = HttpUtility.UrlPathEncode(searchTerm);
            var content = new StringContent(Serialization.ToJson(query), Encoding.UTF8, "application/json");
            var response = await httpClient.PostAsync(GraphQLEndpoint, content).ConfigureAwait(false);
            var str = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            var data = Serialization.FromJson<WebStoreModelsAppsList.QuerySearchResponse>(str);
            return data.data.Catalog.searchStore.elements;
        }

        public async Task<WebStoreModels.ProductResponse> GetProductInfo(string productSlug, string PlayniteLanguage)
        {
            string EpicLangCountry = CodeLang.GetEpicLangCountry(PlayniteLanguage);
            if (PlayniteLanguage == "es_ES" || PlayniteLanguage == "zh_TW")
            {
                EpicLangCountry = CodeLang.GetEpicLang(PlayniteLanguage);
            }
            var slugUri = productSlug.Split('/').First();
            var productUrl = string.Format(ProductUrlBase, slugUri, EpicLangCountry);
            var str = await httpClient.GetStringAsync(productUrl);
            return Serialization.FromJson<WebStoreModels.ProductResponse>(str);
        }
    }
}
