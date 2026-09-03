using System.Text.Json.Serialization;

namespace AkilliMetinDuzenleyici.Models
{
    public class PromptItem
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("content")]
        public string Content { get; set; } = string.Empty;

        public override string ToString() => Name;
    }
}
