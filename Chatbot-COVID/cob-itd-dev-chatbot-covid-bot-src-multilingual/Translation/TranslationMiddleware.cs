// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

// Modification Copyright (c) City of Bellevue. All rights reserved.
// Licensed under the MIT License.

using System;
using System.IO;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Schema;
using Microsoft.BotBuilderSamples.Dialog;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using QnABot.Model;
using System.Net;
using System.Web;
using QnABot.Util;
using System.Text.RegularExpressions;
using Microsoft.Azure.Documents;

namespace Microsoft.BotBuilderSamples.Translation
{
    /// <summary>
    /// Middleware for translating text between the user and bot.
    /// Uses the Microsoft Translator Text API.
    /// </summary>
    public class TranslationMiddleware : IMiddleware
    {
        private readonly MicrosoftTranslator _translator;
        private static string prevPrompt;
        ConversationState _conversationState;
        IConfiguration _configuration;
        private static Regex s_regexParenthesesReplacementZH = new Regex(@"\[(.+)\] *（([^（）]+)）");
        private static Regex s_regexParenthesesReplacementRU = new Regex(@"«(.+)» *\(([^（）]+)\)");
        private static Regex s_regexRemoveMarkdownSpaces = new Regex(@"\] *\(");
        private static Regex s_regexUrl = new Regex(@"(?<url>(http|https)://([\w_-]+(?:(?:\.[\w_-]+)+))([\w.,@?^=%&:/~+#-]*[\w@?^=%&/~+#-])?)");
        /// <summary>
        /// Initializes a new instance of the <see cref="TranslationMiddleware"/> class.
        /// </summary>
        /// <param name="translator">Translator implementation to be used for text translation.</param>
        /// <param name="languageStateProperty">State property for current language.</param>
        public TranslationMiddleware(MicrosoftTranslator translator, ConversationState conversationState, IConfiguration configuration)
        {
            _translator = translator ?? throw new ArgumentNullException(nameof(translator));
            _conversationState = conversationState;
            _configuration = configuration;
            // Set the static ConversationState for use in QnAMakerBaseDialog
            Util.ConversationState = conversationState; 
        }

        /// <summary>
        /// Processes an incoming activity.
        /// </summary>
        /// <param name="turnContext">Context object containing information for a single turn of conversation with a user.</param>
        /// <param name="next">The delegate to call to continue the bot middleware pipeline.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public async Task OnTurnAsync(ITurnContext turnContext, NextDelegate next, CancellationToken cancellationToken = default(CancellationToken))
        {
            // Grab the conversation data
            var conversationStateAccessors = _conversationState.CreateProperty<ConversationData>(nameof(ConversationData));
            var conversationData = await conversationStateAccessors.GetAsync(turnContext, () => new ConversationData());
            string utterance = null;
            string detectedLanguage = null;


            if (turnContext == null)
            {
                throw new ArgumentNullException(nameof(turnContext));
            }

            if (turnContext.Activity.Type == ActivityTypes.Message)
            {
                utterance = ConvertToUtterance(turnContext);
                if (!String.IsNullOrEmpty(utterance))
                {
                    // Detect language
                    if (_configuration["SkipLanguageDetectionAfterInitialChoice"].ToLower() == "false")
                    {
                        detectedLanguage = await _translator.DetectTextRequestAsync(utterance, Consts.SupportedLanguages);
                        if (detectedLanguage != null)
                        {
                            if (detectedLanguage != conversationData.LanguagePreference)
                            {
                                conversationData.LanguageChangeDetected = true;
                                conversationData.LanguagePreference = detectedLanguage;
                            }
                        }
                    }

                    conversationData.UserQuestion = turnContext.Activity.Text;
                    var translate = ShouldTranslateAsync(turnContext, conversationData.LanguagePreference, cancellationToken);

                    if (translate)
                    {
                        if (turnContext.Activity.Type == ActivityTypes.Message)
                        {
                            var specifiedTranslation = Util.GetTranslation(turnContext.Activity.Text, conversationData.LanguagePreference);
                            if (!String.IsNullOrEmpty(specifiedTranslation))
                            {
                                turnContext.Activity.Text = specifiedTranslation;
                            }
                            else
                            {
                                turnContext.Activity.Text = await _translator.TranslateAsync(turnContext.Activity.Text, _configuration["TranslateTo"], cancellationToken);
                            }
                            conversationData.TranslatedQuestion = turnContext.Activity.Text;
                        }
                    }
                }
                turnContext.OnSendActivities(async (newContext, activities, nextSend) =>
                {
                // Grab the conversation data
                var conversationStateAccessors = _conversationState.CreateProperty<ConversationData>(nameof(ConversationData));
                    var conversationData = await conversationStateAccessors.GetAsync(turnContext, () => new ConversationData());

                    string userLanguage = conversationData.LanguagePreference;
                    bool shouldTranslate = (userLanguage != _configuration["TranslateTo"]);

                    // Translate messages sent to the user to user language
                    if (shouldTranslate)
                    {
                        List<Task> tasks = new List<Task>();
                        foreach (Activity currentActivity in activities.Where(a => a.Type == ActivityTypes.Message))
                        {
                            var storedAnswer = currentActivity.Text;
                            conversationData.TranslatedAnswer = storedAnswer;
                            if (!Util.ShouldSkipTranslation(storedAnswer, userLanguage)) // Do not translated the stored non-English answer.
                            {
                                // Always return traditional Chinese to be consistent.
                                var langTranslateTo = (userLanguage == "zh-Hans") ? "zh-Hant" : userLanguage;
                                tasks.Add(TranslateMessageActivityAsync(conversationData, currentActivity.AsMessageActivity(), langTranslateTo, true));
                            }
                        }

                        if (tasks.Any())
                        {
                            await Task.WhenAll(tasks).ConfigureAwait(false);
                        }
                        // Log the questions (There are trace activities that pass through and we do not want to log.)
                        if ((conversationData.TranslatedAnswer != null) && !Util.IsDefaultFeedbackMessage(conversationData.TranslatedAnswer, userLanguage))
                        {
                            if (!String.IsNullOrEmpty(conversationData.UserQuestion))
                            {
                                await Util.Log(conversationData.UserQuestion, conversationData.UserAnswer, conversationData.TranslatedQuestion, conversationData.TranslatedAnswer, userLanguage);
                            }
                        }
                    }

                    return await nextSend();
                });

                turnContext.OnUpdateActivity(async (newContext, activity, nextUpdate) =>
                {
                // Grab the conversation data
                    var conversationStateAccessors = _conversationState.CreateProperty<ConversationData>(nameof(ConversationData));
                    var conversationData = await conversationStateAccessors.GetAsync(turnContext, () => new ConversationData());

                    string userLanguage = conversationData.LanguagePreference;
                    bool shouldTranslate = userLanguage != _configuration["TranslateTo"];
                    
                    // Translate messages sent to the user to user language
                    if (activity.Type == ActivityTypes.Message)
                    {
                        if (shouldTranslate)
                        {
                            await TranslateMessageActivityAsync(conversationData, activity.AsMessageActivity(), userLanguage, false);
                        }
                    }

                    return await nextUpdate();
                });
            }

            await next(cancellationToken).ConfigureAwait(false);
        }

        private async Task TranslateMessageActivityAsync(ConversationData conversationData, IMessageActivity activity, string targetLocale, bool translateAnswer, CancellationToken cancellationToken = default(CancellationToken))
        {

            if (activity.Type == ActivityTypes.Message)
            {
                var txt = activity.Text;
                var txt_dict = ExtractUrls(txt);
                txt = await _translator.TranslateAsync(txt_dict.Item1, targetLocale);

                // Restore the URLs
                var dict = txt_dict.Item2;
                foreach(var placeHolder_URL in dict)
                {
                    var url = Util.GetMappedUrl(placeHolder_URL.Value, targetLocale);
                    if (String.IsNullOrEmpty(url))
                    {
                        url = placeHolder_URL.Value;
                    }
                    if (targetLocale.StartsWith("zh")) // For Chinese, the added period can prevent the link from working. 
                    {
                        txt = txt.Replace(placeHolder_URL.Key + "。", placeHolder_URL.Key);
                    }
                    txt = txt.Replace(placeHolder_URL.Key, url);
                }
                // Translate the parentheis back so that the Markdown get displayed properly.
                if (targetLocale.StartsWith("zh"))
                {
                    txt = s_regexParenthesesReplacementZH.Replace(txt, "[$1]($2)");
                }
                else if(targetLocale == "ru")
                {
                    txt = s_regexParenthesesReplacementRU.Replace(txt, "[$1]($2)");
                }

                // escape the quotes added
                txt = txt.Replace("\"", "\\\"");
                txt = s_regexRemoveMarkdownSpaces.Replace(txt, "](");
                activity.Text = txt;
                if (translateAnswer)
                {
                    conversationData.UserAnswer = txt;
                }
            }
        }

        private static Tuple<string, Dictionary<string, string>> ExtractUrls(string txt)
        {
            int cnt = 0;
            Dictionary<string, string> capturedDict = new Dictionary<string, string>();
            string result = s_regexUrl.Replace(txt, m => {
                cnt++;
                //Use 0123456789 because special character may get translated.
                var replaceText = $"0123456789{cnt}";
                var foundText = m.Groups["url"].Value;
                capturedDict.Add(replaceText, foundText);
                return replaceText;
            });
            return new Tuple<string, Dictionary<string, string>>(result, capturedDict);
        }

        private bool ShouldTranslateAsync(ITurnContext turnContext, string usersLanguage, CancellationToken cancellationToken = default(CancellationToken))
        {
            return turnContext.Activity.Text != null && usersLanguage != _configuration["TranslateTo"];
        }

        private string ConvertToUtterance(ITurnContext turnContext)
        {
            string utterance = null;

            // If this is a postback, check to see if its a "preferred language" choice
            if (turnContext.Activity.Value != null)
            {
                // Split out the language choice
                string[] tokens = turnContext.Activity.Value
                                                        .ToString()
                                                        .Replace('{', ' ')
                                                        .Replace('}', ' ')
                                                        .Replace('"', ' ')
                                                        .Trim()
                                                        .Split(':');

                // If postback is a language choice then grab that choice
                if (tokens.Count() == 2 && tokens[0].Trim() == "LanguagePreference")
                    turnContext.Activity.Text = utterance = tokens[1].Trim();
            }
            else
            {
                utterance = turnContext.Activity.Text.ToLower();
            }

            return utterance;
        }

        private static async Task AskMultilingualActivityCardAsync(string lang, ITurnContext turnContext, CancellationToken cancellationToken)
        {
            string displayThisLanguageCard = lang + "SelectActivityCard.json";
            var languageActivityCard = CreateAdaptiveCardAttachment(displayThisLanguageCard);
            var response = MessageFactory.Attachment(languageActivityCard);
            await turnContext.SendActivityAsync(response, cancellationToken);
        }

        private static Microsoft.Bot.Schema.Attachment CreateAdaptiveCardAttachment(string cardType)
        {
            // combine path for cross platform support
            string[] paths = { ".", "Cards", cardType };
            var fullPath = Path.Combine(paths);
            var adaptiveCard = File.ReadAllText(fullPath);
            return new Microsoft.Bot.Schema.Attachment()
            {
                ContentType = "application/vnd.microsoft.card.adaptive",
                Content = JsonConvert.DeserializeObject(adaptiveCard),
            };
        }
    }
}
