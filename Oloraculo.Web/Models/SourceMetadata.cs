namespace Oloraculo.Web.Models
{
    public sealed record class SourceMetadata(string Name, string Kind,
        DateTimeOffset? FetchedAt = null, string? Notes = null)
    {
        override public string ToString() => $"{Name} ({Kind}) {(FetchedAt is null ? "" : "Fetched: "
            + FetchedAt.Value.ToString("O"))} {(Notes is null ? "" : "Notes: " + Notes)}";
        public static SourceMetadata FifaRankings => new SourceMetadata("FIFA Rankings", "ranking", Notes: "https://www.fifa.com/fifa-world-ranking/");
        public static SourceMetadata EloRatings => new SourceMetadata("Elo Ratings", "ranking", Notes: "https://eloratings.net/");
        public static SourceMetadata ApiFootball => new SourceMetadata("API-Football", "api", Notes: "https://www.api-football.com/documentation-v3");
        public static SourceMetadata AvailabilityNews => new SourceMetadata("Availability News", "news", Notes: "Curated news sources classified with OpenRouter");
        public static SourceMetadata HistoricalResultsCsv => new SourceMetadata("Historical Results CSV", "file", Notes: "https://raw.githubusercontent.com/martj42/international_results/master/results.csv");
    }
}
