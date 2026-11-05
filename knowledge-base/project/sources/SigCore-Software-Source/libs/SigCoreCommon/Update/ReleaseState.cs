using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;
using System.Collections.Generic;

namespace SigCoreCommon.Update
{
    [JsonConverter(typeof(StringEnumConverter))]
    public enum ReleaseDisposition
    {
        Staging,
        Ready,
        Good,
        Failed,
        Rejected
    }


    public class ReleaseEntry
    {
        [JsonProperty("version")]
        public string Version { get; set; } = string.Empty;

        [JsonProperty("disposition")]
        public ReleaseDisposition Disposition { get; set; }

        [JsonProperty("createdUtc")]
        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

        [JsonProperty("reason")]
        public string? Reason { get; set; }

    }

    public class ReleaseState
    {
        [JsonProperty("versions")]
        public List<ReleaseEntry> Versions { get; set; } = new();
    }
}
