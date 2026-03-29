using Microsoft.Extensions.ObjectPool;

namespace AeroFeed.Server.Models
{
    public record RecentChange(
        string? Type,
        //string? Title,
        string? User,
        //string? Comment, // Kinda big and we don't really need any data from this. We can just opt for a bar chart of the most contributions by wiki type or something. (a heap perhaps?)
        bool? Bot,
        bool? Minor,
        //int? Namespace,
        // long? Timestamp,
        // string? ServerName,
        string? Wiki,
        Meta Meta,
        Length? Length
        // Revision? Revision
    );

    public record Meta(
        Guid Id,
        // DateTime Dt, // Maybe for when we want to replay. Anyway, Aiven's free plan probably will not work for the amount of throughput this would require.
        long? Offset // Can use this to determine how far our producer is behind the actual Event Stream
    );

    public record Length(
        int? Old,
        int? New
    );


    /*
    public record Revision(
        long? Old,
        long? New
    ); */

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