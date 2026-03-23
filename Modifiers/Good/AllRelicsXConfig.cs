using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Random;

namespace CustomModifiers;

public static class AllRelicsXConfig
{
    private const int SelectionSize = 5;

    private class ConfigData
    {
        [JsonPropertyName("_readme")]
        public string Readme { get; set; } =
            "Add relic IDs to 'guaranteedSpawn' to force them into the selection. " +
            "Use spawnPool as reference IDs, you can remove these IDs if you want them to stop spawning as options. " +
            "Delete this file to regenerate the reference lists.";
        
        [JsonPropertyName("guaranteedSpawn")]
        public List<string> GuaranteedSpawn { get; set; } = new();

        [JsonPropertyName("spawnPool")]
        public List<string> SpawnPool { get; set; } = new();
    }

    private static readonly JsonSerializerOptions ReadOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        WriteIndented = true
    };


    private static string GetJsonPath()
    {
        var dir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)
                  ?? AppDomain.CurrentDomain.BaseDirectory;
        return Path.Combine(dir, "AllRelicsX.json");
    }


    /// Writes a default config to disk with every relic in spawnPool and an
    /// empty guaranteedSpawn list.  Players can then edit the file freely.
    private static void TryGenerateDefaultConfig(string path)
    {
        try
        {
            var allEntries = ModelDb.AllRelics
                .OrderBy(r => r.Id.Entry)
                .Select(r => r.Id.Entry)
                .ToList();

            var defaultConfig = new ConfigData
            {
                GuaranteedSpawn = new List<string>(),
                SpawnPool = allEntries
            };

            File.WriteAllText(path, JsonSerializer.Serialize(defaultConfig, WriteOptions));
            Log.Info($"[AllRelicsXConfig] Generated default config at: {path}");
        }
        catch (Exception ex)
        {
            Log.Warn($"[AllRelicsXConfig] Could not write default config: {ex.Message}");
        }
    }


    private static ConfigData? TryReadConfig()
    {
        var path = GetJsonPath();

        if (!File.Exists(path))
        {
            Log.Info("[AllRelicsXConfig] No config found — generating default for next time.");
            TryGenerateDefaultConfig(path);
            return null;
        }

        try
        {
            var json = File.ReadAllText(path);
            var config = JsonSerializer.Deserialize<ConfigData>(json, ReadOptions);

            if (config == null)
            {
                Log.Warn("[AllRelicsXConfig] Config file deserialized to null.");
                return null;
            }

            Log.Info($"[AllRelicsXConfig] Loaded — guaranteed: {config.GuaranteedSpawn.Count}, " +
                     $"pool: {config.SpawnPool.Count}");
            return config;
        }
        catch (Exception ex)
        {
            Log.Error($"[AllRelicsXConfig] Failed to parse config: {ex.Message}");
            return null;
        }
    }


    private static RelicModel? TryResolveRelic(string entry)
    {
        var relic = ModelDb.AllRelics.FirstOrDefault(r =>
            string.Equals(r.Id.Entry, entry, StringComparison.OrdinalIgnoreCase));

        if (relic == null)
            Log.Warn($"[AllRelicsXConfig] Could not resolve relic ID: '{entry}'");

        return relic;
    }

    /// <summary>Resolves a list of string IDs to RelicModels, skipping nulls and duplicates.</summary>
    private static List<RelicModel> ResolveRelics(IEnumerable<string> entries)
    {
        var seen   = new HashSet<ModelId>();
        var result = new List<RelicModel>();

        foreach (var entry in entries)
        {
            var relic = TryResolveRelic(entry);
            if (relic != null && seen.Add(relic.Id))
                result.Add(relic);
        }

        return result;
    }
    
    /// <summary>
    /// Builds the five-relic list shown to the player.
    /// <para>
    /// • Guaranteed relics fill the first slots (capped at SelectionSize).<br/>
    /// • Remaining slots are filled with shuffled pool relics.<br/>
    /// • If the pool is too small the remainder comes from allRelics.<br/>
    /// • Falls back to a random shuffle of allRelics when config is absent or empty.
    /// </para>
    /// </summary>
    public static List<RelicModel> BuildSelectionPool(Rng rng)
    {
        var allRelics = ModelDb.AllRelics.ToList();
        var config    = TryReadConfig();

        if (config == null)
            return Fallback(rng, allRelics);

        var guaranteed = ResolveRelics(config.GuaranteedSpawn);
        var pool       = ResolveRelics(config.SpawnPool);

        if (guaranteed.Count == 0 && pool.Count == 0)
        {
            Log.Warn("[AllRelicsXConfig] Both lists resolved to empty — falling back.");
            return Fallback(rng, allRelics);
        }

        // Start with guaranteed relics (up to the selection cap)
        var selected    = guaranteed.Take(SelectionSize).ToList();
        var selectedIds = selected.Select(r => r.Id).ToHashSet();

        int needed = SelectionSize - selected.Count;
        if (needed > 0)
        {
            // Remove already-selected entries from the pool
            var remaining = pool
                .Where(r => !selectedIds.Contains(r.Id))
                .ToList();

            // If the pool can't fill the remaining slots, supplement from allRelics
            if (remaining.Count < needed)
            {
                Log.Warn($"[AllRelicsXConfig] Pool only has {remaining.Count} usable entries " +
                         $"but {needed} slots remain — supplementing from allRelics.");

                var alreadyInRemaining = remaining.Select(r => r.Id).ToHashSet();
                var supplement = allRelics
                    .Where(r => !selectedIds.Contains(r.Id) && !alreadyInRemaining.Contains(r.Id))
                    .ToList();
                rng.Shuffle(supplement);
                remaining.AddRange(supplement);
            }

            rng.Shuffle(remaining);
            selected.AddRange(remaining.Take(needed));
        }
        return selected;
    }

    /// <summary>
    /// Picks one relic for the daily run.
    /// Uses <c>spawnPool</c> when configured; ignores <c>guaranteedSpawn</c>.
    /// Falls back to allRelics if the pool is absent or empty.
    /// </summary>
    public static RelicModel? PickDailyRelic(Rng rng)
    {
        var config = TryReadConfig();

        if (config != null)
        {
            var pool = ResolveRelics(config.SpawnPool);
            if (pool.Count > 0)
            {
                var pick = rng.NextItem(pool);
                Log.Info($"[AllRelicsXConfig] Daily relic picked from pool: {pick?.Id}");
                return pick;
            }
            Log.Warn("[AllRelicsXConfig] spawnPool resolved empty for daily — falling back.");
        }

        return rng.NextItem(ModelDb.AllRelics.ToList());
    }


    private static List<RelicModel> Fallback(Rng rng, List<RelicModel> allRelics)
    {
        var copy = allRelics.ToList();
        rng.Shuffle(copy);
        return copy.Take(SelectionSize).ToList();
    }
}