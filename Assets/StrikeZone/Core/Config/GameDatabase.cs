using System;
using System.Collections.Generic;

namespace StrikeZone.Core
{
    /// <summary>
    /// Validated, indexed view of all balance data. Built from plain deserialised objects,
    /// so it works identically in the client, the headless server and unit tests.
    /// </summary>
    public sealed class GameDatabase
    {
        private readonly Dictionary<string, WeaponDef> _weapons = new Dictionary<string, WeaponDef>(StringComparer.Ordinal);
        private readonly Dictionary<string, SurfaceDef> _surfaces = new Dictionary<string, SurfaceDef>(StringComparer.Ordinal);
        private readonly List<WeaponDef> _weaponList = new List<WeaponDef>();

        public readonly MovementConfig Movement;
        public readonly DamageRules Damage;
        public readonly SurfaceDef DefaultSurface;
        public readonly List<string> Errors = new List<string>();

        public GameDatabase(WeaponDatabaseDef weapons, GameRulesFile rules)
        {
            Movement = rules != null && rules.movement != null ? rules.movement : new MovementConfig();
            Damage = rules != null && rules.damage != null ? rules.damage : new DamageRules();

            if (weapons != null && weapons.weapons != null)
            {
                foreach (var w in weapons.weapons)
                {
                    if (w == null) continue;
                    string error = w.Validate();
                    if (error != null)
                    {
                        Errors.Add(error);
                        continue;
                    }
                    if (_weapons.ContainsKey(w.id))
                    {
                        Errors.Add("duplicate weapon id " + w.id);
                        continue;
                    }
                    w.Resolve();
                    _weapons.Add(w.id, w);
                    _weaponList.Add(w);
                }
            }

            if (rules != null && rules.surfaces != null)
            {
                foreach (var s in rules.surfaces)
                {
                    if (s == null || string.IsNullOrEmpty(s.id)) continue;
                    if (_surfaces.ContainsKey(s.id))
                    {
                        Errors.Add("duplicate surface id " + s.id);
                        continue;
                    }
                    _surfaces.Add(s.id, s);
                }
            }

            SurfaceDef concrete;
            DefaultSurface = _surfaces.TryGetValue("concrete", out concrete)
                ? concrete
                : new SurfaceDef { id = "concrete", resistance = 4f, damageFactor = 0.75f };
        }

        public IReadOnlyList<WeaponDef> Weapons
        {
            get { return _weaponList; }
        }

        public WeaponDef GetWeapon(string id)
        {
            WeaponDef w;
            return id != null && _weapons.TryGetValue(id, out w) ? w : null;
        }

        public SurfaceDef GetSurface(string id)
        {
            SurfaceDef s;
            return id != null && _surfaces.TryGetValue(id, out s) ? s : DefaultSurface;
        }

        public IEnumerable<SurfaceDef> Surfaces
        {
            get { return _surfaces.Values; }
        }
    }
}
