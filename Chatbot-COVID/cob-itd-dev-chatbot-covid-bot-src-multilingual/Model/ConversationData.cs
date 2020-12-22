// Copyright (c) City of Bellevue. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace QnABot.Model
{
    public class ConversationData
    {
        public string UserQuestion { get; set; }
        public string TranslatedQuestion { get; set; }

        public string UserAnswer { get; set; }
        public string TranslatedAnswer { get; set; }
        public string LanguagePreference { get; set; } = "en";
        public bool LanguageChangeDetected { get; set; }
    }
}
