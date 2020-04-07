using Microsoft.Azure.CognitiveServices.Knowledge.QnAMaker;
using Microsoft.Azure.CognitiveServices.Knowledge.QnAMaker.Models;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Bot
{
    public class Backend
    {
        private RestSharp.RestClient client;
        private QnAMakerRuntimeClient qnAMakerClient;
        private readonly IConfiguration configuration;

        public Backend(IConfiguration configuration)
        {
            this.configuration = configuration;
        }

        public async Task<List<Models.SearchResponse>> GetFakeNewsDb(string message)
        {
            try
            {
                client = new RestSharp.RestClient(configuration.GetValue<string>("ApimBaseUrl"));

                RestSharp.RestRequest restRequest = new RestSharp.RestRequest("/we-factsearch-fa/Search", RestSharp.Method.POST);

                restRequest.AddHeader("Ocp-Apim-Subscription-Key", configuration.GetValue<string>("ApimKey"));
                restRequest.AddHeader("Content-Type", "application/json");

                bool result = Uri.TryCreate(message, UriKind.Absolute, out Uri uriResult)
                    && uriResult.Scheme == Uri.UriSchemeHttp;

                Models.Request requestObject;
                if (result)
                {
                    requestObject = new Models.Request
                    {
                        url = message
                    };
                }
                else
                {
                    requestObject = new Models.Request
                    {
                        text = message
                    };
                }

                restRequest.AddJsonBody(requestObject);

                var response = await client.ExecuteAsync(restRequest).ConfigureAwait(false);

                return JsonConvert.DeserializeObject<List<Models.SearchResponse>>(response.Content);
            }
            catch (Exception ex)
            {

                throw;
            }
        }

        public async Task<IList<QnASearchResult>> GetQnAResponse(string question)
        {
            var subscriptionKey = configuration["QnAMakerAPIKey"];
            qnAMakerClient = new QnAMakerRuntimeClient(new EndpointKeyServiceClientCredentials(subscriptionKey)) { RuntimeEndpoint = configuration["QnAMakerEndpoint"] };

            var result = await qnAMakerClient.Runtime.GenerateAnswerAsync("5a60db98-441c-44b4-bbc0-59f70e960d54", new QueryDTO { Question = question });

            return result.Answers;
        }

        public async Task ReportMessage(Models.ReportDetails details)
        {
            client = new RestSharp.RestClient(configuration.GetValue<string>("ApimBaseUrl"));
            RestSharp.RestRequest restRequest = new RestSharp.RestRequest("we-fakenews-func/Insert", RestSharp.Method.POST);

            restRequest.AddHeader("Ocp-Apim-Subscription-Key", configuration.GetValue<string>("ApimKey"));
            restRequest.AddHeader("Content-Type", "application/json");
            restRequest.AddJsonBody(details);

            await client.ExecuteAsync(restRequest).ConfigureAwait(false);
        }
    }
}
