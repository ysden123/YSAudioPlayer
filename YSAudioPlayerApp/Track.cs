namespace YSAudioPlayerApp
{
    internal record Track
    {
        public string? Title { get; init; }
        public TimeSpan? Duration { get; init; }

        public required string FileName { get; init; }

        public string? DurationToString
        {
            get
            {
                if (Duration.HasValue)
                {
                    return Duration.Value.ToString(@"hh\:mm\:ss");
                }
                else
                {
                    return null;
                }
            }
        }
    }
}
