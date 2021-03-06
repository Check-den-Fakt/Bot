﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Schema;
using Microsoft.Extensions.Logging;

using Bot.CognitiveModels;
using System.Text;
using Newtonsoft.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;

namespace Bot.Dialogs
{
    public class MainDialog : ComponentDialog
    {
        private readonly FactCheckRecognizer luisRecognizer;
        protected readonly ILogger Logger;
        private IConfiguration configuration;
        private Backend backend;

        public MainDialog(FactCheckRecognizer luisRecognizer, CheckFactDialog checkFactDialog, QnADialog qnADialog, ReportDialog reportDialog,
            ILogger<MainDialog> logger, IConfiguration configuration) : base(nameof(MainDialog))
        {
            this.luisRecognizer = luisRecognizer;
            this.configuration = configuration;
            Logger = logger;

            AddDialog(new TextPrompt(nameof(TextPrompt)));
            AddDialog(checkFactDialog);
            AddDialog(qnADialog);
            AddDialog(reportDialog);
            AddDialog(new WaterfallDialog(nameof(WaterfallDialog), new WaterfallStep[]
            {
                IntroStepAsync,
                ActStepAsync,
                FinalStepAsync,
            }));

            // The initial child Dialog to run.
            InitialDialogId = nameof(WaterfallDialog);
        }

        private async Task<DialogTurnResult> IntroStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            if (!luisRecognizer.IsConfigured)
            {
                await stepContext.Context.SendActivityAsync(
                    MessageFactory.Text("NOTE: LUIS is not configured. To enable all capabilities, add 'LuisAppId', 'LuisAPIKey' and 'LuisAPIHostName' to the appsettings.json file.", inputHint: InputHints.IgnoringInput), cancellationToken).ConfigureAwait(false);

                return await stepContext.NextAsync(null, cancellationToken).ConfigureAwait(false);
            }

            // Use the text provided in FinalStepAsync or the default if it is the first time.
            var messageText = stepContext.Options?.ToString() ?? "Hallo, wie kann ich dir helfen?";
            var promptMessage = MessageFactory.Text(messageText, messageText, InputHints.ExpectingInput);
            return await stepContext.PromptAsync(nameof(TextPrompt), new PromptOptions { Prompt = promptMessage }, cancellationToken).ConfigureAwait(false);
        }

        private async Task<DialogTurnResult> ActStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            if (!luisRecognizer.IsConfigured)
            {
                // LUIS is not configured, we just run the BookingDialog path with an empty BookingDetailsInstance.
                return await stepContext.BeginDialogAsync(nameof(CheckFactDialog), new Models.FactDetails(), cancellationToken).ConfigureAwait(false);
            }

            // Call LUIS and gather any potential booking details. (Note the TurnContext has the response to the prompt.)
            var luisResult = await luisRecognizer.RecognizeAsync<ChatIntents>(stepContext.Context, cancellationToken).ConfigureAwait(false);
            switch (luisResult.TopIntent().intent)
            {
                case ChatIntents.Intent.Help:

                    string helpText = $"Aktuell kannst du bei uns Fakten prüfen und melden. Gib hierzu einfach zum Beispiel \"prüfe meinen fakt ein\"";
                    var helpMessage = MessageFactory.Text(helpText, helpText, InputHints.IgnoringInput);
                    await stepContext.Context.SendActivityAsync(helpMessage, cancellationToken).ConfigureAwait(false);
                    break;

                case ChatIntents.Intent.Check:

                    var factDetails = new Models.FactDetails()
                    {

                    };

                    return await stepContext.BeginDialogAsync(nameof(CheckFactDialog), factDetails, cancellationToken).ConfigureAwait(false);
                case ChatIntents.Intent.Report:
                    var reportDetails = new Models.ReportDetails();

                    return await stepContext.BeginDialogAsync(nameof(ReportDialog), reportDetails, cancellationToken).ConfigureAwait(false);

                case ChatIntents.Intent.Welcome:
                    string welcomeText = $"Hallo, willkommen bei Check-den-Fakt.de";

                    var welcomeMessage = MessageFactory.Text(welcomeText, welcomeText, InputHints.IgnoringInput);
                    await stepContext.Context.SendActivityAsync(welcomeMessage, cancellationToken).ConfigureAwait(false);
                    break;

                case ChatIntents.Intent.FAQ:

                    var qnaDetails = new Models.QnADetails()
                    {
                    };

                    return await stepContext.BeginDialogAsync(nameof(QnADialog), qnaDetails, cancellationToken).ConfigureAwait(false);

                default:
                    // Catch all for unhandled intents
                    var didntUnderstandMessageText = $"Sorry, ich habe dich nicht verstanden :(";
                    var didntUnderstandMessage = MessageFactory.Text(didntUnderstandMessageText, didntUnderstandMessageText, InputHints.IgnoringInput);
                    await stepContext.Context.SendActivityAsync(didntUnderstandMessage, cancellationToken).ConfigureAwait(false);
                    break;
            }

            return await stepContext.NextAsync(null, cancellationToken).ConfigureAwait(false);
        }

        private async Task<DialogTurnResult> FinalStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            // If the child dialog ("CheckFactDialog") was cancelled, the user failed to confirm or if the intent wasn't BookFlight
            // the Result here will be null.

            if (stepContext.Result is Models.FactDetails result)
            {
                await stepContext.Context.SendActivityAsync(MessageFactory.Text("Einen Moment bitte, ich befrage unsere Datenbank")).ConfigureAwait(false);
                Activity typing = new Activity
                {
                    Type = ActivityTypes.Typing,
                    Text = null
                };
                await stepContext.Context.SendActivityAsync(typing).ConfigureAwait(false);

                bool isUrl = Uri.TryCreate(result.Question.ToLowerInvariant(), UriKind.Absolute, out Uri uriResult);

                string questionText = result.Question.ToLowerInvariant();
                backend = new Backend(configuration);
                Models.TrustedPublisher publisher;

                if (isUrl)
                {
                    // Trusted Publisher
                    publisher = await backend.GetTrustedPublisher(questionText);
                    string publisherMessageText = string.Empty;

                    if (publisher != null)
                    {
                        publisherMessageText = $"Die Überprüfung des Publishers hat folgendes ergeben\nDer Publisher ist zu {Math.Round(publisher.TrustScore * 100)}% vertrauenswürdig.\n\nBegründung:\n{publisher.Reason}";
                    }
                    else
                    {
                        publisherMessageText = "Der Publisher konnte leider nicht validiert werden";
                    }

                    var publisherMessage = MessageFactory.Text(publisherMessageText, publisherMessageText, InputHints.IgnoringInput);
                    await stepContext.Context.SendActivityAsync(publisherMessage, cancellationToken).ConfigureAwait(false);
                    
                    await stepContext.Context.SendActivityAsync(typing).ConfigureAwait(false);

                    var responseObject = await backend.GetWebScraperResult(questionText).ConfigureAwait(false);
                    questionText = responseObject.Text;
                }

                var reply = await backend.GetFakeNewsDb(questionText).ConfigureAwait(false);
                StringBuilder stringBuilder = new StringBuilder();
                stringBuilder.AppendLine("In unserer Fake Datenbank haben wir folgendes Ergebnis gefunden:");

                double searchScore = 0;
                foreach (var item in reply)
                {
                    if (item.SearchScore > searchScore)
                    {
                        searchScore = item.SearchScore;
                    }
                }

                stringBuilder.AppendLine($"Deine Nachricht hat eine Übereinstimmung bis zu {Math.Round(searchScore * 100)}% ergeben. Unsere Suche hat bis zu {reply.Count} Ergebnisse gefunden.");
                string messageText = stringBuilder.ToString();

                var message = MessageFactory.Text(messageText, messageText, InputHints.IgnoringInput);
                await stepContext.Context.SendActivityAsync(message, cancellationToken).ConfigureAwait(false);
            }


            if (stepContext.Result is Models.QnADetails qnaResult)
            {
                string qnaMessage = string.Empty;

                backend = new Backend(configuration);

                var response = await backend.GetQnAResponse(qnaResult.Question).ConfigureAwait(false);

                qnaMessage += "Ich habe folgende Ergebnisse in unserer FAQ gefunden:\n";

                if (response.Count != 0)
                {
                    foreach (var item in response)
                    {
                        if (item.Score > 30)
                        {
                            qnaMessage += item.Answer + "\n";
                        }
                    }
                }

                if (qnaMessage == "Ich habe folgende Ergebnisse in unserer FAQ gefunden:\n")
                {
                    qnaMessage = "Tut mir leid, ich habe leider nichts gefunden :(";
                }


                var qnaMessageText = MessageFactory.Text(qnaMessage, qnaMessage, InputHints.IgnoringInput);
                await stepContext.Context.SendActivityAsync(qnaMessageText, cancellationToken).ConfigureAwait(false);
            }

            if (stepContext.Result is Models.ReportDetails reportDetails)
            {
                string reportMessage = "Vielen Dank für deine Meldung";

                await stepContext.Context.SendActivityAsync(MessageFactory.Text("Einen Moment bitte")).ConfigureAwait(false);

                Activity typing = new Activity
                {
                    Type = ActivityTypes.Typing,
                    Text = null
                };
                await stepContext.Context.SendActivityAsync(typing).ConfigureAwait(false);

                backend = new Backend(configuration);
                await backend.ReportMessage(reportDetails).ConfigureAwait(false);

                var reportMessageText = MessageFactory.Text(reportMessage, reportMessage, InputHints.IgnoringInput);
                await stepContext.Context.SendActivityAsync(reportMessageText, cancellationToken).ConfigureAwait(false);
            }

            // Restart the main dialog with a different message the second time around
            var promptMessage = "Was kann ich sonst noch für dich tun?";
            return await stepContext.ReplaceDialogAsync(InitialDialogId, promptMessage, cancellationToken).ConfigureAwait(false);
        }
    }
}
