using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Random;

namespace CustomModifiers;

public static class SpecializedPlusConfig
{

    private class ConfigData
    {
        [JsonPropertyName("_readme")]
        public string Readme { get; set; } =
            "Add card IDs to 'guaranteedSpawn' to force them into the selection. " +
            "The other fields are reference-only — browse them to find IDs to copy. " +
            "Delete this file to regenerate the reference lists.";


        [JsonPropertyName("guaranteedSpawn")]
        public List<string> GuaranteedSpawn { get; set; } = new();
        
        [JsonPropertyName("ironclad")]
        public List<string> Ironclad { get; set; } = new();

        [JsonPropertyName("silent")]
        public List<string> Silent { get; set; } = new();

        [JsonPropertyName("defect")]
        public List<string> Defect { get; set; } = new();

        [JsonPropertyName("regent")]
        public List<string> Regent { get; set; } = new();

        [JsonPropertyName("necrobinder")]
        public List<string> Necrobinder { get; set; } = new();

        [JsonPropertyName("colorless")]
        public List<string> Colorless { get; set; } = new();

        [JsonPropertyName("curses")]
        public List<string> Curses { get; set; } = new();

        [JsonPropertyName("ancients")]
        public List<string> Ancients { get; set; } = new();

        [JsonPropertyName("status")]
        public List<string> Status { get; set; } = new();

        [JsonPropertyName("token")]
        public List<string> Token { get; set; } = new();

        [JsonPropertyName("quest")]
        public List<string> Quest { get; set; } = new();
    }


    private static readonly JsonSerializerOptions ReadOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling        = JsonCommentHandling.Skip,
        AllowTrailingCommas        = true
    };

    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        WriteIndented = true
    };


    private static string GetJsonPath()
    {
        var dir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)
                  ?? AppDomain.CurrentDomain.BaseDirectory;
        return Path.Combine(dir, "SpecializedPlus.json");
    }


    private static List<string> PoolEntries(CardPoolModel pool) =>
        pool.AllCards
            .OrderBy(c => c.Id.Entry)
            .Select(c => c.Id.Entry)
            .ToList();

    private static void TryGenerateDefaultConfig(string path)
    {
        try
        {
            var config = new ConfigData
            {
                GuaranteedSpawn = new List<string>(),
                Ironclad        = PoolEntries(ModelDb.Character<Ironclad>().CardPool),
                Silent          = PoolEntries(ModelDb.Character<Silent>().CardPool),
                Defect          = PoolEntries(ModelDb.Character<Defect>().CardPool),
                Regent          = PoolEntries(ModelDb.Character<Regent>().CardPool),
                Necrobinder     = PoolEntries(ModelDb.Character<Necrobinder>().CardPool),
                Colorless       = PoolEntries(ModelDb.CardPool<ColorlessCardPool>()),
                Curses          = PoolEntries(ModelDb.CardPool<CurseCardPool>()),
                Ancients        = PoolEntries(ModelDb.CardPool<EventCardPool>()),
                Status          = PoolEntries(ModelDb.CardPool<StatusCardPool>()),
                Token           = PoolEntries(ModelDb.CardPool<TokenCardPool>()),
                Quest           = PoolEntries(ModelDb.CardPool<QuestCardPool>()),
            };

            File.WriteAllText(path, JsonSerializer.Serialize(config, WriteOptions));
            Log.Info($"[SpecializedPlusConfig] Generated default config at: {path}");
        }
        catch (Exception ex)
        {
            Log.Warn($"[SpecializedPlusConfig] Could not write default config: {ex.Message}");
        }
    }

    

    private static ConfigData? TryReadConfig()
    {
        var path = GetJsonPath();

        if (!File.Exists(path))
        {
            Log.Info("[SpecializedPlusConfig] No config found — generating reference file for next time.");
            TryGenerateDefaultConfig(path);
            return null;
        }

        try
        {
            var json   = File.ReadAllText(path);
            var config = JsonSerializer.Deserialize<ConfigData>(json, ReadOptions);

            if (config == null)
            {
                Log.Warn("[SpecializedPlusConfig] Config deserialized to null.");
                return null;
            }

            Log.Info($"[SpecializedPlusConfig] Loaded config — guaranteedSpawn: {config.GuaranteedSpawn.Count} entries.");
            return config;
        }
        catch (Exception ex)
        {
            Log.Error($"[SpecializedPlusConfig] Failed to parse config: {ex.Message}");
            return null;
        }
    }


    private static CardModel? TryResolveCard(string entry)
    {
        var card = ModelDb.AllCards.FirstOrDefault(c =>
            string.Equals(c.Id.Entry, entry, StringComparison.OrdinalIgnoreCase));

        if (card == null)
            Log.Warn($"[SpecializedPlusConfig] Could not resolve card ID: '{entry}'");

        return card;
    }

    private static List<CardModel> ResolveCards(IEnumerable<string> entries)
    {
        var seen   = new HashSet<ModelId>();
        var result = new List<CardModel>();

        foreach (var entry in entries)
        {
            var card = TryResolveCard(entry);
            if (card != null && seen.Add(card.Id))
                result.Add(card);
        }

        return result;
    }


    /// <summary>
    /// Builds the card list shown to the player at Neow:
    /// guaranteed cards (from JSON) followed by all cards from the player's
    /// character class.  No cap — shows everything.
    /// </summary>
    public static List<CardCreationResult> BuildSelectionList(Player player)
    {
        var config    = TryReadConfig();
        var guaranteed    = config != null
            ? ResolveCards(config.GuaranteedSpawn)
            : new List<CardModel>();

        var guaranteedIds = guaranteed.Select(c => c.Id).ToHashSet();

        // All cards from the player's own class, excluding anything already in guaranteed
        var characterCards = player.Character.CardPool.AllCards
            .Where(c => !guaranteedIds.Contains(c.Id))
            .OrderBy(c => RarityOrder(c.Rarity))
            .ThenBy((c=> c.Id.Entry))
            .ToList();

        // Guaranteed first so they're prominently visible, then class cards
        return guaranteed
            .Concat(characterCards)
            .Select(c => new CardCreationResult(c))
            .ToList();
    }
    
    private static int RarityOrder(CardRarity rarity) => rarity switch
    {
        CardRarity.Basic    => 0,
        CardRarity.Common   => 1,
        CardRarity.Uncommon => 2,
        CardRarity.Rare     => 3,
        _                   => 4
    };
}