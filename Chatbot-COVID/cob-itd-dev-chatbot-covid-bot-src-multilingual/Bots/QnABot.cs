// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Documents.Client;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Schema;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Linq;
using QnABot.Util;
using Microsoft.BotBuilderSamples.Middleware;
using System.Diagnostics;
using QnABot.Model;

namespace Microsoft.BotBuilderSamples.Bots
{
    public class QnABot<T> : ActivityHandler where T : Microsoft.Bot.Builder.Dialogs.Dialog
    {
        protected readonly BotState ConversationState;
        protected readonly Microsoft.Bot.Builder.Dialogs.Dialog Dialog;
        protected readonly BotState UserState;

        public QnABot(ConversationState conversationState, UserState userState, T dialog)
        {
            ConversationState = conversationState;
            UserState = userState;
            Dialog = dialog;

       }

        public override async Task OnTurnAsync(ITurnContext turnContext, CancellationToken cancellationToken = default)
        {
            await base.OnTurnAsync(turnContext, cancellationToken);
            
                // Save any state changes that might have occured during the turn.
                await ConversationState.SaveChangesAsync(turnContext, false, cancellationToken);
            await UserState.SaveChangesAsync(turnContext, false, cancellationToken);
        }

        protected override async Task OnMessageActivityAsync(ITurnContext<IMessageActivity> turnContext, CancellationToken cancellationToken)
        {
            var txt = turnContext.Activity.Text;
            dynamic val = turnContext.Activity.Value;
            // Check if the activity came from a submit action
            if (string.IsNullOrEmpty(txt) && val != null)
            {
                JToken resultToken = JToken.Parse(turnContext.Activity.Value.ToString());
                if (null != resultToken["Action"] && "languageselector" == resultToken["Action"].Value<string>())
                {
                    var selectedLang = resultToken["choiceset"].Value<string>();
                    var attachment = Cards.GetCard(selectedLang, CardType.Intro);
                    if (attachment != null)
                    {
                        var reply = MessageFactory.Attachment(attachment);
                        await turnContext.SendActivityAsync(reply, cancellationToken);
                    }
                    else // debug;
                    {
                        //await turnContext.SendActivityAsync(s_ErrorMsg);
                    }
                }
                else if (null != resultToken["Consent"] && 1 == resultToken["Consent"].Value<int>())
                {
                    var lang = resultToken["Language"].Value<string>();
                    var attachment = Cards.GetCard(lang, CardType.AfterAgreeing);
                    if (attachment != null)
                    {
                        var reply = MessageFactory.Attachment(attachment);
                        await turnContext.SendActivityAsync(reply, cancellationToken);
                    }
                    else // debug;
                    {
                        //await turnContext.SendActivityAsync(s_ErrorMsg);
                    }
                }
                else if (Util.IsRequestingUserFeedback)
                {
                    if (null != resultToken["helpful"])
                    {
                        var helpful = resultToken["helpful"].Value<int>();
                        var language = resultToken["language"].Value<string>();
                        string msg = "";
                        if (1 == helpful)
                        {
                            msg = Util.GetMessageAfterFeedbackHelpful(language);
                            
                        }
                        else if (0 == helpful)
                        {
                            msg = Util.GetMessageAfterFeedbackNotHelpful(language);
                        }
                        await turnContext.SendActivityAsync(msg);
                        var question = resultToken["question"].Value<string>();
                        var answer = resultToken["answer"].Value<string>();
                        var translatedQuestion = resultToken["translatedQuestion"].Value<string>();
                        var translatedAnswer = resultToken["translatedAnswer"].Value<string>();
                        var langRecord = resultToken["language"].Value<string>();
                        await Util.RecordQuestionAnswerFeedback(
                            BotServices.TableDbConnectionString,
                            question,
                            answer,
                            translatedQuestion,
                            translatedAnswer,
                            helpful,
                            langRecord);
                    }
                }
            }
            else
            {
                await Dialog.RunAsync(turnContext, ConversationState.CreateProperty<DialogState>(nameof(DialogState)), cancellationToken);
            }
            
        }

        protected override async Task OnMembersAddedAsync(IList<ChannelAccount> membersAdded, ITurnContext<IConversationUpdateActivity> turnContext, CancellationToken cancellationToken)
        {
            foreach (var member in membersAdded)
            {
                if (member.Id != turnContext.Activity.Recipient.Id)
                {
                    if (Util.IsSupportingMultiLingual)
                    {
                        await AskPreferredLanguageCardAsync(turnContext, cancellationToken);
                    }
                    else
                    {
                        // Display the intro card
                        var attachment = Cards.GetCard("en", CardType.Intro);
                        var reply = MessageFactory.Attachment(attachment);
                        await turnContext.SendActivityAsync(reply, cancellationToken);
                    }
                }
            }
        }

        private static async Task AskPreferredLanguageCardAsync(ITurnContext turnContext, CancellationToken cancellationToken)
        {
            var languageCard = Cards.GetSelectLanguageCard();
            var response = MessageFactory.Attachment(languageCard);
            await turnContext.SendActivityAsync(response, cancellationToken);
        }
    }
}
