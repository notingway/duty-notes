namespace DutyNotes;

/// <summary>
/// Curated lists of defensive cooldowns (mitigation, barriers, invulnerabilities, knockback immunity)
/// and healing skills. Matched by English skill name; a skill's whole upgrade chain counts if any version
/// is listed. The game data has no reliable "is defensive" flag, so add names here if something is missing.
/// </summary>
public static class DefensiveSkills
{
    /// <summary>Whether a skill belongs in the "defensive and healing" picker filter.</summary>
    public static bool Includes(string name) => Names.Contains(name) || Healing.Contains(name);

    public static readonly HashSet<string> Healing = new(StringComparer.OrdinalIgnoreCase)
    {
        // Role / tanks / damage jobs
        "Second Wind", "Bloodbath", "Equilibrium", "Clemency", "Curing Waltz", "Improvisation",
        "Vercure", "Star Prism", "Lux Solaris", "Improvised Finish",

        // White Mage (and Conjurer)
        "Cure", "Cure II", "Cure III", "Medica", "Medica II", "Medica III", "Regen",
        "Afflatus Solace", "Afflatus Rapture", "Benediction", "Tetragrammaton", "Assize",
        "Liturgy of the Bell", "Divine Caress",

        // Scholar (and Arcanist)
        "Physick", "Adloquium", "Succor", "Concitation", "Lustrate", "Indomitability",
        "Aetherpact", "Whispering Dawn", "Fey Blessing", "Seraphism",
        "Seraphic Veil", "Angel's Whisper", "Manifestation", "Accession",

        // Astrologian
        "Benefic", "Benefic II", "Aspected Benefic", "Helios", "Aspected Helios", "Helios Conjunction",
        "Essential Dignity", "Earthly Star", "Celestial Opposition", "Horoscope", "Synastry",

        // Sage (the Eukrasian forms are the ones used while Eukrasia is on)
        "Diagnosis", "Prognosis", "Eukrasian Diagnosis", "Eukrasian Prognosis", "Eukrasian Prognosis II",
        "Physis", "Physis II", "Soteria", "Druochole", "Ixochole", "Pepsis", "Pneuma", "Krasis",
        "Zoe", "Philosophia",
    };

    public static readonly HashSet<string> Names = new(StringComparer.OrdinalIgnoreCase)
    {
        // Role
        "Rampart", "Reprisal", "Arm's Length", "Feint", "Addle", "Surecast",

        // Tanks
        "Vengeance", "Damnation", "Thrill of Battle", "Holmgang", "Raw Intuition", "Bloodwhetting",
        "Nascent Flash", "Shake It Off", "Equilibrium",
        "Sentinel", "Guardian", "Hallowed Ground", "Bulwark", "Sheltron", "Holy Sheltron", "Intervention",
        "Cover", "Divine Veil", "Passage of Arms",
        "Shadow Wall", "Shadowed Vigil", "Dark Mind", "Living Dead", "The Blackest Night", "Oblation",
        "Dark Missionary",
        "Camouflage", "Nebula", "Great Nebula", "Heart of Stone", "Heart of Corundum", "Heart of Light",
        "Superbolide", "Aurora",

        // Healers
        "Temperance", "Divine Benison", "Aquaveil", "Plenary Indulgence", "Asylum",
        "Sacred Soil", "Fey Illumination", "Expedient", "Consolation",
        "Excogitation", "Protraction", "Seraphic Illumination",
        "Collective Unconscious", "Celestial Intersection", "Exaltation", "Neutral Sect", "Sun Sign",
        "Macrocosmos", "Microcosmos",
        "Kerachole", "Taurochole", "Haima", "Holos", "Panhaima",

        // The one limit break entry (see ActionCatalog)
        "Limit Break",

        // Melee / ranged / casters
        "Riddle of Earth", "Earth's Reply", "Shade Shift", "Third Eye", "Tengentsu", "Arcane Crest",
        "Shield Samba", "Troubadour", "Nature's Minne", "Tactician", "Dismantle",
        "Radiant Aegis", "Magick Barrier", "Tempera Coat", "Tempera Grassa", "Manaward",
    };
}
