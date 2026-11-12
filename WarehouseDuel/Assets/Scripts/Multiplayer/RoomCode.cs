using System.Collections.Generic;
using System.Text;

namespace WarehouseDuel.Multiplayer
{
    /// <summary>
    /// Private room codes. A code is 5 random characters (the Photon session name) plus one character
    /// that encodes the host's Photon region, shown as "K7QPM-E".
    /// Photon sessions only exist inside one region; encoding it lets a friend on a different network
    /// (whose "best region" may differ) still find the room.
    /// The alphabet leaves out 0/O, 1/I/L so codes are easy to read aloud.
    /// </summary>
    public static class RoomCode
    {
        public const string Alphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";
        public const int SessionLength = 5;

        private static readonly Dictionary<char, string> RegionByChar = new Dictionary<char, string>
        {
            ['A'] = "asia", ['U'] = "au", ['C'] = "cae", ['E'] = "eu", ['H'] = "hk", ['N'] = "in",
            ['J'] = "jp", ['K'] = "kr", ['S'] = "sa", ['T'] = "tr", ['D'] = "uae", ['M'] = "us",
            ['W'] = "usw", ['R'] = "ussc", ['Z'] = "za",
        };

        private static readonly System.Random Random = new System.Random();

        /// <summary>A fresh random session name (5 characters).</summary>
        public static string NewSessionName()
        {
            var sb = new StringBuilder(SessionLength);
            lock (Random)
            {
                for (int i = 0; i < SessionLength; i++) sb.Append(Alphabet[Random.Next(Alphabet.Length)]);
            }
            return sb.ToString();
        }

        /// <summary>Display code "SESSN-R" for a session name and a Photon region token (e.g. "eu").</summary>
        public static string ToDisplay(string sessionName, string region)
        {
            foreach (KeyValuePair<char, string> pair in RegionByChar)
            {
                if (string.Equals(pair.Value, region, System.StringComparison.OrdinalIgnoreCase))
                {
                    return $"{sessionName}-{pair.Key}";
                }
            }
            return sessionName; // unknown region: joiner falls back to its best region
        }

        /// <summary>
        /// Parses user input ("k7qpm-e", "K7QPME", " k7qpm ") into a session name and optional region.
        /// Returns false with a user-facing message when the format is wrong.
        /// </summary>
        public static bool TryParse(string input, out string sessionName, out string region, out string error)
        {
            sessionName = null;
            region = null;
            error = null;

            var clean = new StringBuilder();
            foreach (char c in (input ?? string.Empty).ToUpperInvariant())
            {
                if (c == '-' || char.IsWhiteSpace(c)) continue;
                clean.Append(c);
            }

            string code = clean.ToString();
            if (code.Length != SessionLength && code.Length != SessionLength + 1)
            {
                error = "Room codes have 5 characters plus a region letter, e.g. K7QPM-E.";
                return false;
            }

            foreach (char c in code)
            {
                if (Alphabet.IndexOf(c) < 0)
                {
                    error = $"'{c}' can't appear in a room code. Check the code and try again.";
                    return false;
                }
            }

            sessionName = code.Substring(0, SessionLength);
            if (code.Length == SessionLength + 1)
            {
                if (!RegionByChar.TryGetValue(code[SessionLength], out region))
                {
                    error = "The last letter of the code (the region) is not valid.";
                    return false;
                }
            }
            return true;
        }
    }
}
