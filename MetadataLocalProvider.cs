﻿using Playnite.SDK.Plugins;
using System;
using System.Collections.Generic;
using PluginCommon;
using Playnite.Common.Web;
using Newtonsoft.Json;
using System.Text;
using System.Net.Http;
using System.Linq;
using MetadataLocal.OriginLibrary;
using MetadataLocal.SteamLibrary;
using Playnite.SDK;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace MetadataLocal
{
    public class MetadataLocalProvider : OnDemandMetadataProvider
    {
        private static readonly ILogger logger = LogManager.GetLogger();

        private readonly MetadataRequestOptions options;
        private readonly MetadataLocal plugin;

        private HttpClient httpClient = new HttpClient();

        public string PlayniteConfigurationPath { get; set; }
        public static string  PlayniteLanguage { get; set; }
        
        private List<MetadataField> availableFields;
        public override List<MetadataField> AvailableFields
        {
            get
            {
                if (availableFields == null)
                {
                    availableFields = GetAvailableFields();
                }
        
                return availableFields;
            }
        }

        private List<MetadataField> GetAvailableFields()
        {
            var fields = new List<MetadataField> { MetadataField.Name };
            fields.Add(MetadataField.Description);
            return fields;
        }

        public MetadataLocalProvider(MetadataRequestOptions options, MetadataLocal plugin, string PlayniteConfigurationPath)
        {
            this.options = options;
            this.plugin = plugin;
            this.PlayniteConfigurationPath = PlayniteConfigurationPath;
        }

        // Override additional methods based on supported metadata fields.
        public override string GetDescription()
        {
            // Get type source, data and description
            string Data;
            string Description = "";

            if (AvailableFields.Contains(MetadataField.Description))
            {
                // Get Playnite language
                PlayniteLanguage = Localization.GetPlayniteLanguageConfiguration(PlayniteConfigurationPath);

                try
                {
                    string gameId = options.GameData.GameId;

                    switch (options.GameData.Source.Name.ToLower())
                    {
                        case "steam":
                            uint appId = uint.Parse(gameId);
                            Data = GetSteamData(appId, PlayniteLanguage);
                            var parsedData = JsonConvert.DeserializeObject<Dictionary<string, StoreAppDetailsResult>>(Data);
                            Description = parsedData[appId.ToString()].data.detailed_description;
                            break;

                        case "origin":
                            Description = GetOriginData(gameId, PlayniteLanguage);
                            break;

                        case "epic":
                            using (var client = new WebStoreClient())
                            {
                                var catalogs = client.QuerySearch(options.GameData.Name).GetAwaiter().GetResult();
                                if (catalogs.HasItems())
                                {
                                    var product = client.GetProductInfo(catalogs[0].productSlug, PlayniteLanguage).GetAwaiter().GetResult();
                                    if (product.pages.HasItems())
                                    {
                                        var page = product.pages.FirstOrDefault(a => a.type == "productHome");
                                        if (page == null)
                                        {
                                            page = product.pages[0];
                                        }

                                        Description = page.data.about.description;
                                        if (!Description.IsNullOrEmpty())
                                        {
                                            Description = Description.Replace("\n", "\n<br>");

                                            // Markdown image to html image  
                                            Description = Regex.Replace(
                                                Description,
                                                "!\\[[a-zA-Z0-9- ]*\\][\\s]*\\(((ftp|http|https):\\/\\/(\\w+:{0,1}\\w*@)?(\\S+)(:[0-9]+)?(\\/|\\/([\\w#!:.?+=&%@!\\-\\/]))?)\\)",
                                                "<img src=\"$1\" width=\"100%\"/>");
                                        }
                                    }
                                }
                            }
                            break;
                    }
                }
                catch (Exception ex)
                {
                    var LineNumber = new StackTrace(ex, true).GetFrame(0).GetFileLineNumber();
                    string FileName = new StackTrace(ex, true).GetFrame(0).GetFileName();
                    logger.Error(ex, $"MetadataLocal [{FileName} {LineNumber}] ");
                }
            }

            if (Description.IsNullOrEmpty())
            {
                return base.GetDescription();
            }
            else
            {
                return Description;
            }
        }


        // Override Steam function GetRawStoreAppDetail in WebApiClient on SteamLibrary.
        public static string GetSteamData(uint appId, string PlayniteLanguage)
        {
            string SteamLangCode = CodeLang.GetSteamLang(PlayniteLanguage);
            var url = $"https://store.steampowered.com/api/appdetails?appids={appId}&l={SteamLangCode}";
            return HttpDownloader.DownloadString(url);
        }

        // Override Origin function GetGameStoreData in OriginApiClient on OriginLibrary.
        public static string GetOriginData(string gameId, string PlayniteLanguage)
        {
            string OriginLang = CodeLang.GetOriginLang(PlayniteLanguage);
            string OriginLangCountry = CodeLang.GetOriginLangCountry(PlayniteLanguage);
            var url = string.Format(@"https://api2.origin.com/ecommerce2/public/supercat/{0}/{1}?country={2}",
                gameId, OriginLang, OriginLangCountry);
            var stringData = Encoding.UTF8.GetString(HttpDownloader.DownloadData(url));
            return JsonConvert.DeserializeObject<GameStoreDataResponse>(stringData).i18n.longDescription;
        }
    }
}