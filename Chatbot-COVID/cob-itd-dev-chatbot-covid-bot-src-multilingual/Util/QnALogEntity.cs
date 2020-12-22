// Copyright (c) City of Bellevue. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Azure.Cosmos.Table;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace QnABot.Util
{
    public class QnALogEntity : TableEntity
    {
        public QnALogEntity(string userQuestion, string userAnswer, string translatedQuestion, string translatedAnswer, string userLanguage)
        {
            PartitionKey = "0";
            RowKey = Guid.NewGuid().ToString();
            UserQuestion = userQuestion;
            UserAnswer = userAnswer;
            TranslatedQuestion = translatedQuestion;
            TranslatedAnswer = translatedAnswer;
            UserLanguage = userLanguage;
        }

        public string UserQuestion { get; set; }
        public string UserAnswer { get; set; }
        public string TranslatedQuestion { get; set; }
        public string TranslatedAnswer { get; set; }
        public string UserLanguage { get; set; }
    }
}
