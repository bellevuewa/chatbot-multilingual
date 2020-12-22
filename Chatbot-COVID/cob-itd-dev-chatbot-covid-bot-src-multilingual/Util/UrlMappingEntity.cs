// Copyright (c) City of Bellevue. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Azure.Cosmos.Table;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace QnABot.Util
{
    public class UrlMappingEntity : TableEntity
    {
        //"en", "es", "ko", "zh-Hant", "vi", "ru"
        public string en { get; set; }
        public string es { get; set; }
        public string ko { get; set; }
        public string zh { get; set; }
        public string vi { get; set; }
        public string ru { get; set; }
    }
}
