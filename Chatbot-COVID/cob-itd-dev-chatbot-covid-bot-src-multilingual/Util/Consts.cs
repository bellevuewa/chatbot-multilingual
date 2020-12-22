// Copyright (c) City of Bellevue. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace QnABot.Util
{
    public static class Consts
    {
        // When displaying the cards, only provide the option of Traditional Chinese
        static public string[] SupportedLanguagesDisplay = new string[] { "en", "es", "ko", "zh-Hant", "vi", "ru" };
        static public string[] SupportedLanguages = new string[] { "en", "es", "ko", "zh-Hans", "zh-Hant", "vi", "ru" };
        static public Dictionary<string, string> LanguageNameDict = new Dictionary<string, string>()
        {
            {"es", "spanish-espanol" },
            {"ko", "korean" },
            {"zh-Hans", "chinese" },
            {"zh-Hant", "chinese" },
            {"ru", "russian" },
            {"vi", "vietnamese" }
        };
    }
}
