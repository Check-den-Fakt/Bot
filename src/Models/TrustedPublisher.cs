using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Bot.Models
{
    public class TrustedPublisher
    {
        [JsonProperty("url")]
        public string Url { get; set; }

        [JsonProperty("trustScore")]
        public double TrustScore { get; set; }

        [JsonProperty("reason")]
        public string Reason { get; set; }

        [JsonProperty("partitionKey")]
        public string PartitionKey { get; set; }

        [JsonProperty("rowKey")]
        public string RowKey { get; set; }

        [JsonProperty("timestamp")]
        public DateTimeOffset Timestamp { get; set; }

        [JsonProperty("eTag")]
        public string ETag { get; set; }
    }
}
