using System.Text.Json;

namespace StrikeZone.Core
{
    /// <summary>
    /// Parses the balance JSON (config/weapons.json, config/rules.json) into a validated
    /// GameDatabase. Lives in the engine-free core so the client, the dedicated server and
    /// the unit tests all read balance data through exactly the same code.
    /// </summary>
    public static class ConfigJson
    {
        private static readonly JsonSerializerOptions Options = new JsonSerializerOptions
        {
            IncludeFields = true,
            PropertyNameCaseInsensitive = false,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        };

        public static GameDatabase Parse(string weaponsJson, string rulesJson)
        {
            WeaponDatabaseDef weapons = string.IsNullOrWhiteSpace(weaponsJson)
                ? null
                : JsonSerializer.Deserialize<WeaponDatabaseDef>(weaponsJson, Options);
            GameRulesFile rules = string.IsNullOrWhiteSpace(rulesJson)
                ? null
                : JsonSerializer.Deserialize<GameRulesFile>(rulesJson, Options);
            var db = new GameDatabase(weapons, rules);
            if (weapons == null) db.Errors.Add("weapons.json is missing or empty");
            if (rules == null) db.Errors.Add("rules.json is missing or empty");
            return db;
        }
    }
}
