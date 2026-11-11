using StrikeZone.Core;
using UnityEngine;

namespace StrikeZone.Game
{
    /// <summary>
    /// Loads balance JSON from Resources/Config into the engine-independent GameDatabase.
    /// In the multiplayer phase the same JSON is delivered by the backend and its hash is
    /// compared between client and server.
    /// </summary>
    public static class ConfigLoader
    {
        public const string WeaponsPath = "Config/weapons";
        public const string RulesPath = "Config/rules";

        public static GameDatabase Load()
        {
            var weaponsAsset = Resources.Load<TextAsset>(WeaponsPath);
            var rulesAsset = Resources.Load<TextAsset>(RulesPath);

            WeaponDatabaseDef weapons = null;
            GameRulesFile rules = null;

            if (weaponsAsset != null) weapons = JsonUtility.FromJson<WeaponDatabaseDef>(weaponsAsset.text);
            else Debug.LogError("[StrikeZone] Missing Resources/" + WeaponsPath + ".json");

            if (rulesAsset != null) rules = JsonUtility.FromJson<GameRulesFile>(rulesAsset.text);
            else Debug.LogError("[StrikeZone] Missing Resources/" + RulesPath + ".json");

            var db = new GameDatabase(weapons, rules);
            foreach (var error in db.Errors) Debug.LogError("[StrikeZone] Config error: " + error);
            return db;
        }
    }
}
