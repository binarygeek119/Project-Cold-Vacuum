using System.Text;

namespace Space.Core
{
    /// <summary>
    /// Deterministic name generation for celestial bodies.
    /// All names derived from seed - no global state.
    /// </summary>
    public static class NameGen
    {
        // Syllable banks for procedural generation
        private static readonly string[] Prefixes = {
            "Al", "Bel", "Cor", "Dra", "El", "Fal", "Gor", "Hal", "Ix", "Jor",
            "Kal", "Lum", "Mor", "Nar", "Or", "Pal", "Quar", "Ril", "Sol", "Tal",
            "Ul", "Val", "Wyr", "Xan", "Yar", "Zor"
        };

        private static readonly string[] Middles = {
            "an", "ar", "en", "er", "in", "on", "or", "un", "ur", "al",
            "el", "il", "ol", "ul", "ax", "ex", "ix", "ox", "ux"
        };

        private static readonly string[] Suffixes = {
            "a", "ae", "ai", "an", "ar", "as", "ax", "e", "ea", "en",
            "er", "es", "i", "ia", "is", "ix", "o", "on", "or", "os",
            "ox", "u", "us", "ux", "yx", "za", "ze", "zo"
        };

        /// <summary>
        /// Generate a name for a celestial body from seed.
        /// Names are 1-3 syllables, max 24 chars, ASCII only.
        /// </summary>
        public static string GenerateName(ulong seed)
        {
            SeededRng rng = new SeededRng(Seeds.Hash64(seed, Tags.Name));
            StringBuilder sb = new StringBuilder();

            // Decide syllable count: 1-3 syllables
            int syllables = rng.Range(1, 4); // 1, 2, or 3

            // First syllable: prefix
            sb.Append(rng.Pick(Prefixes));

            // Middle syllables (if any)
            for (int i = 1; i < syllables - 1; i++)
            {
                sb.Append(rng.Pick(Middles));
            }

            // Last syllable: suffix (if more than 1 syllable)
            if (syllables > 1)
            {
                sb.Append(rng.Pick(Suffixes));
            }

            // Ensure max 24 chars and ASCII
            string name = sb.ToString();
            if (name.Length > 24)
                name = name.Substring(0, 24);

            // Verify ASCII
            foreach (char c in name)
            {
                if (c < 32 || c > 126)
                    return "Unknown"; // Fallback (should never happen)
            }

            return name;
        }

        /// <summary>
        /// Generate a star system name
        /// </summary>
        public static string GenerateStarName(ulong starSeed)
        {
            return GenerateName(starSeed);
        }

        /// <summary>
        /// Generate a planet name (often numbered relative to star)
        /// </summary>
        public static string GeneratePlanetName(ulong planetSeed, int index)
        {
            // 50% chance of being named, 50% chance of being numbered
            SeededRng rng = new SeededRng(Seeds.Hash64(planetSeed, Tags.Planet));
            if (rng.Bool(0.5f))
            {
                return GenerateName(planetSeed);
            }
            else
            {
                string starName = GenerateName(Seeds.Hash64(planetSeed, Tags.Star));
                return $"{starName} {index + 1}";
            }
        }

        /// <summary>
        /// Generate a moon name
        /// </summary>
        public static string GenerateMoonName(ulong moonSeed, string planetName, int index)
        {
            return $"{planetName} {IndexToRoman(index + 1)}";
        }

        /// <summary>
        /// Generate a station name
        /// </summary>
        public static string GenerateStationName(ulong stationSeed)
        {
            SeededRng rng = new SeededRng(Seeds.Hash64(stationSeed, Tags.Station));
            
            string[] prefixes = { "Station", "Outpost", "Port", "Base", "Hub" };
            string prefix = rng.Pick(prefixes);
            string name = GenerateName(stationSeed);
            
            return $"{prefix} {name}";
        }

        /// <summary>
        /// Convert integer to Roman numeral (I, II, III, IV, V, etc.)
        /// </summary>
        private static string IndexToRoman(int num)
        {
            if (num < 1 || num > 39) return num.ToString(); // Fallback for large numbers

            string[] thousands = { "", "M", "MM", "MMM" };
            string[] hundreds = { "", "C", "CC", "CCC", "CD", "D", "DC", "DCC", "DCCC", "CM" };
            string[] tens = { "", "X", "XX", "XXX", "XL", "L", "LX", "LXX", "LXXX", "XC" };
            string[] ones = { "", "I", "II", "III", "IV", "V", "VI", "VII", "VIII", "IX" };

            string roman = "";
            roman += thousands[num / 1000];
            roman += hundreds[(num % 1000) / 100];
            roman += tens[(num % 100) / 10];
            roman += ones[num % 10];

            return roman;
        }
    }
}
