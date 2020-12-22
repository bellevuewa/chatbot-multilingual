// Copyright (c) City of Bellevue. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Azure.Cosmos.Table;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace QnABot.Util
{
    public class TranslationEntity : TableEntity
    {
        public TranslationEntity()
        {
        }

        public string Phrase { get; set; }
        public string Language { get; set; }
        public string English { get; set; }
    }
}
