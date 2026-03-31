using Serilog;
using System.Text.Json;

namespace YSAudioPlayerApp
{
    public record LastPosition
    {
        private static readonly ILogger _logger = Log.ForContext<LastPosition>();

        public string? FilePath { get; set; }
        public TimeSpan? CurrentTime { get; set; }

        public string? FolderName
        {
            get
            {
                return System.IO.Path.GetDirectoryName(FilePath);
            }
        }

        public string ToJson()
        {
            try
            {
                return JsonSerializer.Serialize(this);
            }
            catch (JsonException ex)
            {
                _logger.Error(ex, "Failed to serialize LastPosition to JSON: {LastPosition}", this);
                return string.Empty;
            }
        }

        public static LastPosition? FromJson(string? json)
        {
            if (string.IsNullOrEmpty(json))
            {
                return null;
            }

            try
            {
                return JsonSerializer.Deserialize<LastPosition>(json);
            }
            catch (JsonException ex)
            {
                _logger.Error(ex, "Failed to deserialize LastPosition from JSON: {Json}", json);
                return null;
            }
        }
    }
}
