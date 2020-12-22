// Copyright (c) City of Bellevue. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Azure.Cosmos.Table;
using Microsoft.Azure.Documents.Client;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Schema;
using Microsoft.BotBuilderSamples;
using Microsoft.EntityFrameworkCore.ValueGeneration.Internal;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using QnABot.Model;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace QnABot.Util
{
    public static class Util
    {
        private static string TableDBConnectionString { get; set; }
        private static string s_errorMsg;
        private static Dictionary<string, string> s_defaultNoAnswerDict = new Dictionary<string, string>();
        private static string s_requestingUserFeedback;
        private static Dictionary<string, string> s_messageAfterFeedbackHelpfulDict = new Dictionary<string, string>();
        private static Dictionary<string, string> s_messageAfterFeedbackNotHelpfulDict = new Dictionary<string, string>();
        // (language -> (phrase -> English))
        private static Dictionary<string, Dictionary<string, string>> s_translationDict = new Dictionary<string, Dictionary<string, string>>();
        // (English_Url -> (language -> Url))
        private static Dictionary<string, Dictionary<string, string>> s_urlMappingDict = new Dictionary<string, Dictionary<string, string>>();
        public static ConversationState ConversationState { get; set; }
        
        public static bool IsRequestingUserFeedback { get; set; }
        public static bool IsSupportingMultiLingual { get; set; }
        public static bool IsLogging { get; set; }
        public static void Initialize(IConfiguration configuration)
        {
            if (s_defaultNoAnswerDict.Count == 0)
            {
                var configList = new Dictionary<string, Dictionary<string, string>>()
                {
                    {"DefaultNoAnswer_MultiLingual", s_defaultNoAnswerDict },
                    {"MessageAfterFeedbackHelpful_MultiLingual", s_messageAfterFeedbackHelpfulDict },
                    {"MessageAfterFeedbackNotHelpful_MultiLingual", s_messageAfterFeedbackNotHelpfulDict } 
                };
                foreach (var config in configList)
                {
                    var name = config.Key;
                    var dict = config.Value;
                    var content = GetValue(configuration, name, ref s_errorMsg);
                    PopulateLanguageContent(content, dict);
                }
            }

            // Initialize config variables.
            var configContent = GetValue(configuration, "appsettings", ref s_errorMsg);
            dynamic configJsonObj = JsonConvert.DeserializeObject(configContent);
            IsRequestingUserFeedback = (configJsonObj.IsRequestingUserFeedback == "Y");
            IsSupportingMultiLingual = (configJsonObj.IsSupportingMultiLingual == "Y");
            IsLogging = (configJsonObj.IsLogging == "Y");
            InitializeTranslationDictAndUrlMapping(configuration);
        }

        private static void InitializeTranslationDictAndUrlMapping(IConfiguration configuration)
        {
            TableDBConnectionString = configuration["TableDbConnectionString"];
            try
            {
                PopulateTranslationDict(TableDBConnectionString);
                PopulateUrlMappings(TableDBConnectionString);
            }
            catch (Exception ex)
            {
                s_errorMsg += ex.Message + ex.StackTrace;
            }
        }


        public static string GetMappedUrl(string url, string language)
        {
            Dictionary<string, string> lang_UrlDict = null;
            if (!s_urlMappingDict.TryGetValue(url, out lang_UrlDict))
            {
                return GetAutomatedMapping(url, language);
            }
            string mappedUrl = null;
            if (language == "zh-Hans")
            {
                language = "zh-Hant"; // Only support Chinese Traditional
            }
            if (!lang_UrlDict.TryGetValue(language, out mappedUrl) || String.IsNullOrEmpty(mappedUrl))
            {
                return GetAutomatedMapping(url, language);
            }
            return mappedUrl;
        }

        private static void PopulateTranslationDict(string connectionString)
        {
            var table = GetTable(connectionString, "Translations");

            TableQuery<TranslationEntity> query = new TableQuery<TranslationEntity>();
            foreach (TranslationEntity entity in table.ExecuteQuery(query))
            {
                Dictionary<string, string> phraseDict1 = null;
                if (!s_translationDict.TryGetValue(entity.Language, out phraseDict1))
                {
                    phraseDict1 = new Dictionary<string, string>();
                    s_translationDict[entity.Language] = phraseDict1;
                }
                phraseDict1[entity.Phrase] = entity.English;
            }
        }

        private static void PopulateUrlMappings(string connectionString)
        {
            var table = GetTable(connectionString, "UrlMappings");

            TableQuery<UrlMappingEntity> query = new TableQuery<UrlMappingEntity>();
            foreach (UrlMappingEntity mapping in table.ExecuteQuery(query))
            {
                if (!s_urlMappingDict.ContainsKey(mapping.en))
                {
                    Dictionary<string, string> languageUrlDict = new Dictionary<string, string>();
                    languageUrlDict.Add("es", mapping.es); // "en", "es", "ko", "zh-Hant", "vi", "ru"
                    languageUrlDict.Add("ko", mapping.ko);
                    languageUrlDict.Add("zh-Hant", mapping.zh);
                    languageUrlDict.Add("vi", mapping.vi);
                    languageUrlDict.Add("ru", mapping.ru);
                    s_urlMappingDict.Add(mapping.en, languageUrlDict);
                }
            }
        }
        public static void PopulateLanguageContent(string json, Dictionary<string, string> dict)
        {
            dynamic fetch = JsonConvert.DeserializeObject(json);
            foreach(var p in fetch)
            {
                dict.Add(p.language.ToString(), p.value.ToString());
            }
        }
        public static string GetDefaultNoAnswer(string language)
        {
            return GetValue(language,s_defaultNoAnswerDict);
        }

        public static string GetValue(string language, Dictionary<string, string> dict)
        {
            if (!Consts.SupportedLanguagesDisplay.Contains(language))
            {
                if (language == "zh-Hans")
                {
                    language = "zh-Hant"; // display Chinese traditional
                }
                else
                {
                    // return English
                    language = "en";
                }
            }
            string value = null;
            if (!dict.TryGetValue(language, out value))
            {
                return dict["en"];
            }
            return value;
        }

        public static string GetMessageAfterFeedbackHelpful(string language)
        {
            return GetValue(language, s_messageAfterFeedbackHelpfulDict);
        }

        public static string GetMessageAfterFeedbackNotHelpful(string language)
        {
            return GetValue(language, s_messageAfterFeedbackNotHelpfulDict);
        }
 
        public static string GetValue(IConfiguration configuration, string itemName, ref string errorMsg)
        {
            string configContent = String.Empty;
            try
            {
                var endpoint = configuration["DocDbEndPoint"];
                var masterKey = configuration["DocDbMasterKey"];
                var container = configuration["DocDbContainer"];
                var database = configuration["DocDbDatabase"];

                errorMsg += "Info: database: " + database + "\n";
                using (var client = new DocumentClient(new Uri(endpoint), masterKey))
                {
                    var option = new FeedOptions { EnableCrossPartitionQuery = true };
                    var searchStr = $"select * from c where c.name = '{itemName}'";
                    var response = client.CreateDocumentQuery(UriFactory.CreateDocumentCollectionUri(database, container),
                        searchStr, option);

                    var document = response.ToList().First();

                    if (!(document is null))
                    {
                        configContent = document.value.ToString();
                    }
                    
                }
            }
            catch (Exception ex)
            {
                errorMsg += ex.Message + ex.StackTrace;
            }

            if (String.IsNullOrEmpty(configContent))
            {
                try
                {
                    // combine path for cross platform support
                    string[] paths = { ".", "Resources", $"{itemName}.json" };
                    var itemContent = File.ReadAllText(Path.Combine(paths));
                    dynamic obj = JObject.Parse(itemContent);
                    configContent = obj.value.ToString();
                }
                catch(Exception ex)
                {
                    errorMsg += ex.Message + ex.StackTrace;
                }
            }
            return configContent;
        }

        public static Attachment CreateAdaptiveCardAttachment(string strCard, ref string errorMsg)
        {
            try
            {
                var adaptiveCardAttachment = new Attachment()
                {
                    ContentType = "application/vnd.microsoft.card.adaptive",
                    Content = JsonConvert.DeserializeObject(strCard),
                };
                return adaptiveCardAttachment;
            }
            catch (Exception ex)
            {
                errorMsg += ex.Message;
                errorMsg += ex.StackTrace;
                return null;
            }

        }

        private static CloudStorageAccount CreateStorageAccountFromConnectionString(string storageConnectionString)
        {
            CloudStorageAccount storageAccount = null;
            try
            {
                storageAccount = CloudStorageAccount.Parse(storageConnectionString);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message + ex.StackTrace);
            }
            return storageAccount;
        }

        public static async Task RecordQuestionAnswerFeedback(string connectionString, string question, string answer, string translatedQuestion, string translatedAnswer, int helpful, string language)
        {
            try
            {
                var storageAccount = CreateStorageAccountFromConnectionString(connectionString);
                // Create a table client for interacting with the table service
                CloudTableClient tableClient = storageAccount.CreateCloudTableClient(new TableClientConfiguration());

                // Create a table client for interacting with the table service 
                CloudTable table = tableClient.GetTableReference("QnAFeedback");
                var entity = new QnAFeedbackEntity(question, answer, translatedQuestion, translatedAnswer, helpful, language);
                await InsertEntityAsync(table, entity);
            }
            catch(Exception ex)
            {
                Debug.WriteLine(ex.Message + ex.StackTrace);
            }
        }

        public static async Task Log(string userQuestion, string userAnswer, string translatedQuestion, string translatedAnswer, string userLanguage)
        {
            try
            {
                var storageAccount = CreateStorageAccountFromConnectionString(TableDBConnectionString);
                // Create a table client for interacting with the table service
                CloudTableClient tableClient = storageAccount.CreateCloudTableClient(new TableClientConfiguration());

                // Create a table client for interacting with the table service 
                CloudTable table = tableClient.GetTableReference("QnALogs");
                var entity = new QnALogEntity(userQuestion, userAnswer, translatedQuestion, translatedAnswer, userLanguage);
                await InsertEntityAsync(table, entity);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message + ex.StackTrace);
            }
        }

        public static async Task<T> InsertEntityAsync<T>(CloudTable table, T entity) where T : TableEntity
        {
            if (entity == null)
            {
                Debug.WriteLine("Null entity");
                return null;
            }
            try
            {
                // Create the InsertOrReplace table operation
                TableOperation insertOrMergeOperation = TableOperation.InsertOrMerge(entity);

                // Execute the operation.
                TableResult result = await table.ExecuteAsync(insertOrMergeOperation);
                T inserted = result.Result as T;

                return inserted;
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message + ex.StackTrace);
                return null;
            }
        }

        public static string GetTranslation(string phrase, string language)
        {
            Dictionary<string, string> phraseDict = null;
            
            if (!s_translationDict.TryGetValue(language, out phraseDict))
            {
                return null;
            }
            string translation = null;
            if (!phraseDict.TryGetValue(phrase, out translation))
            {
                return null;
            }
            return translation;
        }

        private static CloudTable GetTable(string connectionString, string tableName)
        {
            CloudStorageAccount account = CreateStorageAccountFromConnectionString(connectionString);

            CloudTableClient client = account.CreateCloudTableClient();
            CloudTable table = client.GetTableReference(tableName);
            return table;

        }
        
        private static string GetAutomatedMappedPage(string url, string languageCode)
        {
            if (url.StartsWith("https://bellevuewa.gov/city-government/departments/city-managers-office/communications/emergencies/covid-19/community-resources", StringComparison.OrdinalIgnoreCase))
            {
                var language = Consts.LanguageNameDict[languageCode];
                string rtn = $"https://bellevuewa.gov/{language}/covid-19/community-resources";
                return rtn;
            }
            return url; // Not mapped.
        }
        private static string GetAutomatedMapping(string url, string languageCode)
        {
            Uri uriAddress = new Uri(url);
            var leftPart = uriAddress.GetLeftPart(UriPartial.Path);
            var pagePath = GetAutomatedMappedPage(leftPart, languageCode);
            string mappedUrl = pagePath + uriAddress.Fragment;
            if (!DoesUrlExist(mappedUrl)) // Display English version if it does not exist
            {
                return url;
            }
            return mappedUrl;

        }

        public static bool ShouldSkipTranslation(string text, string language)
        {
            var shouldSkip = (text == GetDefaultNoAnswer(language)) || (text == GetMessageAfterFeedbackHelpful(language))
                || (text == GetMessageAfterFeedbackNotHelpful(language));
            return shouldSkip;
        }

        public static bool IsDefaultFeedbackMessage(string text, string language)
        {
            var shouldSkip = (text == GetMessageAfterFeedbackHelpful(language)) || (text == GetMessageAfterFeedbackNotHelpful(language));
            return shouldSkip;
        }
        private static bool DoesUrlExist(string url)
        {
            try
            {
                Uri uriAddress = new Uri(url);
                var leftPart = uriAddress.GetLeftPart(UriPartial.Path);
                var fragment = uriAddress.Fragment;
                WebClient wc = new WebClientWithTimeout();
                var content = wc.DownloadString(leftPart);
                var searchStr = fragment.Replace("#", "");
                if (!String.IsNullOrEmpty(searchStr))
                {
                    if (content is null)
                    {
                        return false;
                    }
                    if (content.Contains(searchStr))
                    {
                        return true;
                    }
                    else
                    {
                        return false;
                    }
                }
                else
                {
                    return !String.IsNullOrEmpty(content);
                }
            }
            catch (WebException)
            {
                return false;
            }
        }
    }
}
