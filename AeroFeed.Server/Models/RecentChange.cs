using Microsoft.Extensions.ObjectPool;

namespace AeroFeed.Server.Models
{
    public record RecentChange(
        string? Type,
        string? Title,
        string? User,
        string? Comment,
        bool? Bot,
        bool? Minor,
        int? Namespace,
        long? Timestamp,
        string? ServerName,
        string? Wiki,
        Meta Meta,
        Length? Length,
        Revision? Revision
    );

    public record Meta(
        string Id,
        DateTime Dt,
        string Stream,
        string? Domain,
        string? Uri
    );

    public record Length(
        int? Old,
        int? New
    );

    public record Revision(
        long? Old,
        long? New
    );

    public class RecentChangeAnalytics // These properties are converted to camelCase when we send it over signalR
    {
        public long NetLength { get; set; } = 0;

        public Dictionary<string, int> TypeCounts { get; set; } = new Dictionary<string, int>() // https://www.mediawiki.org/wiki/Manual:Recentchanges_table
        {
            ["edit"] = 0,
            ["new"] = 0,
            ["log"] = 0,
            ["categorize"] = 0,
            ["external"] = 0
        };

        public int Bots { get; set; } = 0;

        public int NonBots { get; set; } = 0;
    };

}