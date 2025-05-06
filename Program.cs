using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace WorldStatsFinder
{
    // Model matching the JSON structure.
    public class StatsFile
    {
        public Dictionary<string, Dictionary<string, int>> Stats { get; set; }
        public int DataVersion { get; set; }
    }

    // Wraps a StatsFile and its source file path.
    public class StatsFileEntry
    {
        public StatsFile Stats { get; set; }
        public string FilePath { get; set; }
    }

    // Represents a single stat record for table-like querying.
    public class StatRecord
    {
        public string WorldName { get; set; }
        public string UUID { get; set; }
        public string StatCategory { get; set; }
        public string Item { get; set; }
        public int Value { get; set; }
        public string FilePath { get; set; } // Added file path
    }

    class Program
    {
        // Hard-coded target location.
        private const string baseDirectory = @"\\MINECRAFTSERVER\Minecraft Server";

        // Set to true to limit processing during testing.
        private const bool IsTestMode = false;
        private const int TestLimit = 5;

        static void Main(string[] args)
        {
            if (!Directory.Exists(baseDirectory))
            {
                Console.WriteLine("The specified directory does not exist or is unreachable.");
                return;
            }

            Console.WriteLine("Searching for JSON files in 'stats' directories...");

            // Build query for JSON files in folders named "stats".
            var allStatsFilesQuery = Directory.EnumerateFiles(baseDirectory, "*.json", SearchOption.AllDirectories)
                .Where(file =>
                {
                    var parentDir = new DirectoryInfo(Path.GetDirectoryName(file));
                    return string.Equals(parentDir.Name, "stats", StringComparison.OrdinalIgnoreCase);
                });

            List<string> statsFilePaths;
            if (IsTestMode)
            {
                statsFilePaths = allStatsFilesQuery.Take(TestLimit).ToList();
                Console.WriteLine($"Test mode active: processing first {TestLimit} JSON file(s).");
            }
            else
            {
                statsFilePaths = allStatsFilesQuery.ToList();
            }
            Console.WriteLine($"Found {statsFilePaths.Count} JSON file(s).");

            // Build nested data: World -> UUID -> StatsFileEntry.
            var worldStatsData = new Dictionary<string, Dictionary<string, StatsFileEntry>>(StringComparer.OrdinalIgnoreCase);
            foreach (var file in statsFilePaths)
            {
                try
                {
                    string json = File.ReadAllText(file);
                    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    StatsFile stats = JsonSerializer.Deserialize<StatsFile>(json, options);
                    if (stats == null)
                        continue;

                    var statsDir = new DirectoryInfo(Path.GetDirectoryName(file));
                    string worldName = statsDir.Parent?.Name ?? "Unknown";
                    string uuid = Path.GetFileNameWithoutExtension(file);

                    if (!worldStatsData.ContainsKey(worldName))
                    {
                        worldStatsData[worldName] = new Dictionary<string, StatsFileEntry>(StringComparer.OrdinalIgnoreCase);
                    }
                    worldStatsData[worldName][uuid] = new StatsFileEntry { Stats = stats, FilePath = file };
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error processing file {file}: {ex.Message}");
                }
            }
            Console.WriteLine("File processing complete.");

            // Flatten the structure into a list of StatRecord.
            var flatRecords = new List<StatRecord>();
            foreach (var worldEntry in worldStatsData)
            {
                string worldName = worldEntry.Key;
                foreach (var uuidEntry in worldEntry.Value)
                {
                    string uuid = uuidEntry.Key;
                    StatsFileEntry entry = uuidEntry.Value;
                    if (entry.Stats.Stats == null)
                        continue;

                    foreach (var statCategory in entry.Stats.Stats)
                    {
                        if (statCategory.Value == null)
                            continue;

                        foreach (var stat in statCategory.Value)
                        {
                            flatRecords.Add(new StatRecord
                            {
                                WorldName = worldName,
                                UUID = uuid,
                                StatCategory = statCategory.Key,
                                Item = stat.Key,
                                Value = stat.Value,
                                FilePath = entry.FilePath
                            });
                        }
                    }
                }
            }

            // Filter for a specific item, e.g. "minecraft:cobblestone"
            string targetItem = "minecraft:lantern";
            var filteredRecords = flatRecords
                .Where(r => string.Equals(r.Item, targetItem, StringComparison.OrdinalIgnoreCase))
                .ToList();

            // Group the filtered records by category.
            var groupedRecords = filteredRecords
                .GroupBy(r => r.StatCategory)
                .OrderBy(g => g.Key);

            // Output the grouped results, limiting to the top 5 records per category.
            Console.WriteLine($"\nResults for item: {targetItem}");
            foreach (var group in groupedRecords)
            {
                Console.WriteLine($"\nCategory: {group.Key}");
                Console.WriteLine("{0,-20} {1,-36} {2,-20} {3,-20} {4,-8} {5}", "World", "UUID", "Category", "Item", "Value", "File Path");
                Console.WriteLine(new string('-', 160));

                var topRecords = group.OrderByDescending(r => r.Value).Take(10000);
                foreach (var record in topRecords)
                {
                    Console.WriteLine("{0,-20} {1,-36} {2,-20} {3,-20} {4,-8} {5}",
                        record.WorldName, record.UUID, record.StatCategory, record.Item, record.Value, record.FilePath);
                }
            }
            Console.WriteLine($"\nTotal records for {targetItem}: {filteredRecords.Count}");
        }
    }
}
