// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

// Modification Copyright (c) City of Bellevue. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Bot.Builder.AI.QnA;
using Microsoft.Extensions.Configuration;

namespace Microsoft.BotBuilderSamples
{
    public class BotServices : IBotServices
    {
        public BotServices(IConfiguration configuration)
        {
            QnAMakerService = new QnAMaker(new QnAMakerEndpoint
            {
                KnowledgeBaseId = configuration["QnAKnowledgebaseId"],
                EndpointKey = configuration["QnAAuthKey"],
                Host = GetHostname(configuration["QnAEndpointHostName"])
            });

            DocDbEndPoint = configuration["DocDbEndPoint"];
            DocDbMasterKey = configuration["DocDbMasterKey"];
            DocDbContainer = configuration["DocDbContainer"];
            DocDbDatabase = configuration["DocDbDatabase"];
            DefaultNoAnswer = configuration["DefaultNoAnswer"];
            TableDbConnectionString = configuration["TableDbConnectionString"];
        }

        public QnAMaker QnAMakerService { get; private set; }

        public static string DocDbEndPoint { get; private set; }
        public static string DocDbMasterKey { get; private set; }
        public static string DocDbContainer { get; private set; }
        public static string DocDbDatabase { get; private set; }
        public static string DefaultNoAnswer { get; private set; }
        public static string TableDbConnectionString { get; private set; }
        private static string GetHostname(string hostname)
        {
            if (!hostname.StartsWith("https://"))
            {
                hostname = string.Concat("https://", hostname);
            }

            if (!hostname.EndsWith("/qnamaker"))
            {
                hostname = string.Concat(hostname, "/qnamaker");
            }

            return hostname;
        }
    }
}
