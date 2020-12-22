// Copyright (c) City of Bellevue. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Bot.Schema;
using Microsoft.BotBuilderSamples.Translation;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace QnABot.Util
{
    public static class Cards
    {
        private static string s_errorMsg;
        private static Dictionary<string,string> s_feedbackCardContentDict = new Dictionary<string, string>();
        private static Dictionary<string, Dictionary<CardType, Attachment>> s_cardDict = new Dictionary<string, Dictionary<CardType, Attachment>>();
        private static Attachment s_selectLanguageCard;

        public static void InitializeCards(IConfiguration configuration)
        {
            try
            {
                if (s_cardDict.Count == 0)
                {
                    s_selectLanguageCard = Util.CreateAdaptiveCardAttachment(Util.GetValue(configuration, $"CardSelectLanguage", ref s_errorMsg), ref s_errorMsg);
                    
                    Dictionary<string, CardType> cardList = new Dictionary<string, CardType>()
                    {{ "CardIntro_MultiLingual", CardType.Intro },
                        {"CardAfterAgreeing_MultiLingual", CardType.AfterAgreeing },
                        { "CardFeedback_MultiLingual", CardType.Feedback } };
                    foreach(var name_type in cardList)
                    {
                        var name = name_type.Key;
                        var type = name_type.Value;
                        var value = Util.GetValue(configuration, name, ref s_errorMsg);
                        Dictionary<string, string> dict = new Dictionary<string, string>();
                        Util.PopulateLanguageContent(value, dict);
                        foreach(var lang_value in dict)
                        {
                            var lang = lang_value.Key;
                            var content = lang_value.Value;
                            Dictionary<CardType, Attachment> dictCard = null;
                            if (!s_cardDict.TryGetValue(lang, out dictCard))
                            {
                                dictCard = new Dictionary<CardType, Attachment>();
                                s_cardDict[lang] = dictCard;
                            }
                            if (type != CardType.Feedback)
                            {
                                var attachment = Util.CreateAdaptiveCardAttachment(content, ref s_errorMsg);
                                dictCard[type] = attachment;
                            }
                            else
                            {
                                s_feedbackCardContentDict.Add(lang, content);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                s_errorMsg += ex.Message + ex.StackTrace;
            }
        }
        public static Attachment GetCard(string language, CardType type)
        {
            if (!Consts.SupportedLanguagesDisplay.Contains(language))
            {
                //Debug.Assert(false);
                throw new ArgumentException(nameof(language) + ": " + language);
            }
            Dictionary<CardType, Attachment> dict = null;
            if (!s_cardDict.TryGetValue(language, out dict))
            {
                dict = s_cardDict["en"];
            }
            return dict[type];
        }

        public static Attachment GetSelectLanguageCard()
        {
            return s_selectLanguageCard;
        }
        public static Attachment GetFeedbackCard(string language, string question, string answer, string translatedQuestion, string translatedAnswer)
        {
            if (language == "zh-Hans") // Only support displaying Chinese traditional
            {
                language = "zh-Hant";
            }
            if (!Consts.SupportedLanguagesDisplay.Contains(language))
            {
                //Debug.Assert(false);
                return null;
            }
            string cardContent = null;
            if (!s_feedbackCardContentDict.TryGetValue(language, out cardContent))
            {
                cardContent = s_feedbackCardContentDict["en"];
            }
            var content = cardContent.Replace("__question__", question);
            content = content.Replace("__answer__", answer);
            content = content.Replace("__translatedQuestion__", translatedQuestion);
            content = content.Replace("__translatedAnswer__", translatedAnswer);
            var attachment = Util.CreateAdaptiveCardAttachment(content, ref s_errorMsg);
            return attachment;
        }
    }
}
