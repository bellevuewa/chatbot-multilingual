// Copyright (c) City of Bellevue. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Azure.Cosmos.Table;

namespace QnABot.Util
{ 
    public class QnAFeedbackEntity : TableEntity
    {
        public QnAFeedbackEntity()
        {
        }

        public QnAFeedbackEntity(string question, string answer, string tranlsatedQuestion, string translatedAnswer, int helpful, string language)
        {
            PartitionKey = "0";
            RowKey = Guid.NewGuid().ToString();
            Question = question;
            Answer = answer;
            TranslatedQuestion = tranlsatedQuestion;
            TranslatedAnswer = translatedAnswer;
            Helpful = helpful;
            Language = language;
        }

        public string Question { get; set; }
        public string Answer { get; set; }
        public string TranslatedQuestion { get; set; }
        public string TranslatedAnswer { get; set; }
        public int Helpful { get; set; }
        public string Language { get; set; }
    }
}
