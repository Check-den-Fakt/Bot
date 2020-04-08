using Newtonsoft.Json;

namespace Bot.Models
{
    public class ScraperRequest
    {
        [JsonProperty("url")]
        public string url { get; set; }
    }
}
