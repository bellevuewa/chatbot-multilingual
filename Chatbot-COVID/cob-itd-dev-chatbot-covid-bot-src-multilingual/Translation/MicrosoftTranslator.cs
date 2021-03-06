﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.BotBuilderSamples.Translation.Model;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using QnABot.Translation;

namespace Microsoft.BotBuilderSamples.Translation
{
    public class MicrosoftTranslator
    {
        private const string Host = "https://api-nam.cognitive.microsofttranslator.com";
        private const string Path = "/translate?api-version=3.0";
        private const string UriParams = "&to=";
        
        private static HttpClient _client = new HttpClient();

        private readonly string _key;


        public MicrosoftTranslator(IConfiguration configuration)
        {
            var key = configuration["TranslatorTextKey"];
            _key = key ?? throw new ArgumentNullException(nameof(key));
        }

        public async Task<string> DetectTextRequestAsync(string inputText, string[] preferredLanguages)
        //public async Task DetectTextRequestAsync(string inputText)
        {
            System.Object[] body = new System.Object[] { new { Text = inputText } };
            var requestBody = JsonConvert.SerializeObject(body);
            using (var client = new HttpClient())
            using (var request = new HttpRequestMessage())
            {
                // Build the request.
                request.Method = HttpMethod.Post;
                var endpoint = Host;
                string route = "/detect?api-version=3.0";
                // Construct the URI and add headers.
                request.RequestUri = new Uri(endpoint + route);
                request.Content = new StringContent(requestBody, Encoding.UTF8, "application/json");
                request.Headers.Add("Ocp-Apim-Subscription-Key", _key);

                // Send the request and get response.
                HttpResponseMessage response = await client.SendAsync(request).ConfigureAwait(false);
                // Read response as a string.
                string result = await response.Content.ReadAsStringAsync();
                // Deserialize the response using the classes created earlier.
                DetectResult[] deserializedOutput = JsonConvert.DeserializeObject<DetectResult[]>(result);
                // Iterate over the deserialized response.
                string languageWithMaxScore = null;
                double maxScore = -1;
                foreach (DetectResult o in deserializedOutput)
                {
                    if (o.Score > maxScore && preferredLanguages.Contains(o.Language))
                    {
                        maxScore = o.Score;
                        languageWithMaxScore = o.Language;
                    }
                    // Iterate through alternatives. Use counter for alternative number.
                    if (o.Alternatives != null)
                    {
                        foreach (AltTranslations a in o.Alternatives)
                        {
                            if (a.Score > maxScore && preferredLanguages.Contains(a.Language))
                            {
                                maxScore = a.Score;
                                languageWithMaxScore = a.Language;
                            }
                        }
                    }
                }
                return languageWithMaxScore;
            }
        }

        public async Task<string> TranslateAsync(string text, string targetLocale, CancellationToken cancellationToken = default(CancellationToken))
        {
            // From Cognitive Services translation documentation:
            // https://docs.microsoft.com/en-us/azure/cognitive-services/translator/quickstart-csharp-translate
            var body = new object[] { new { Text = text } };
            var requestBody = JsonConvert.SerializeObject(body);

            using (var request = new HttpRequestMessage())
            {
                var uri = Host + Path + UriParams + targetLocale;
                request.Method = HttpMethod.Post;
                request.RequestUri = new Uri(uri);
                request.Content = new StringContent(requestBody, Encoding.UTF8, "application/json");
                request.Headers.Add("Ocp-Apim-Subscription-Key", _key);

                var response = await _client.SendAsync(request, cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    throw new Exception($"The call to the translation service returned HTTP status code {response.StatusCode}.");
                }

                var responseBody = await response.Content.ReadAsStringAsync();
                var result = JsonConvert.DeserializeObject<TranslatorResponse[]>(responseBody);

                return result?.FirstOrDefault()?.Translations?.FirstOrDefault()?.Text;
            }
        }
    }
}
