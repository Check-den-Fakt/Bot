using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System.Globalization;

namespace Bot.Models
{
    public partial class SearchResponse
    {
        [JsonProperty("document")]
        public Document Document { get; set; }

        [JsonProperty("@search.score")]
        public double SearchScore { get; set; }

        [JsonProperty("@search.highlights")]
        public object SearchHighlights { get; set; }
    }

    public partial class Document
    {
        [JsonProperty("Content")]
        public string Content { get; set; }

        [JsonProperty("ApprovedByModerator")]
        public bool ApprovedByModerator { get; set; }

        [JsonProperty("Votes")]
        public long Votes { get; set; }

        [JsonProperty("AmountOfVotes")]
        public long AmountOfVotes { get; set; }
    }
}