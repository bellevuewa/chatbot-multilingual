// Copyright (c) City of Bellevue. All rights reserved.
// Licensed under the MIT License.

using Microsoft.AspNetCore.Identity.UI.Pages.Internal.Account.Manage;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Schema;
using Microsoft.BotBuilderSamples.Bots;
using Microsoft.BotBuilderSamples.Dialog;
using Microsoft.EntityFrameworkCore.ValueGeneration.Internal;
using QnABot.Model;
using QnABot.Util;
using Remotion.Linq.Parsing.Structure.IntermediateModel;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.BotBuilderSamples.Middleware
{
    public class FeedbackMiddleware : IMiddleware
    {
        protected static string s_CardAfterAgreeing;
        public static string s_strCardFeedback;

        ConversationState _conversationState;

        public FeedbackMiddleware(ConversationState conversationState)
        {
            _conversationState = conversationState;
        }

        /// <summary>
        /// Processes an incoming activity.
        /// </summary>
        /// <param name="turnContext">Context object containing information for a single turn of conversation with a user.</param>
        /// <param name="next">The delegate to call to continue the bot middleware pipeline.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public async Task OnTurnAsync(ITurnContext turnContext, NextDelegate next, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (Util.IsRequestingUserFeedback)
            {
                if (turnContext != null)
                {
                    turnContext.OnSendActivities(async (newContext, activities, nextSend) =>
                    {
                        var count = activities.Count;
                        if (count > 0)
                        {
                            for (int idx = count - 1; idx >= 0; idx--)
                            {
                                var activity = activities[idx];
                                if (activity.Type == ActivityTypes.Message && !String.IsNullOrEmpty(activity.Text))
                                {
                                    if (!String.IsNullOrEmpty(newContext.Activity.Text)) // Otherwise it can be error message.
                                {
                                        var conversationStateAccessors = _conversationState.CreateProperty<ConversationData>(nameof(ConversationData));
                                        var conversationData = await conversationStateAccessors.GetAsync(turnContext, () => new ConversationData());
                                        string userLanguage = conversationData.LanguagePreference;
                                        if (activity.Text != Util.GetDefaultNoAnswer(userLanguage))
                                        {
                                            string question = conversationData.UserQuestion;
                                            string answer = activity.Text;
                                            var card = Cards.GetFeedbackCard(userLanguage, question, answer, conversationData.TranslatedQuestion, conversationData.TranslatedAnswer);

                                            activity.Attachments = new List<Attachment>() { card };
                                        }
                                    }
                                }
                            }
                        }

                        return await nextSend();
                    });

                }

            }

            await next(cancellationToken).ConfigureAwait(false);
        }
    }
}
