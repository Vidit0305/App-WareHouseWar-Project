using System.Text;

namespace WarehouseWar.Multiplayer
{
    /// <summary>
    /// Private room codes: a 6-digit number such as "482915". The first five digits are random (the Photon
    /// session name) and the last digit says which Photon region the host is in, so a friend on a different
    /// network (whose "best region" may differ) still finds the room.
    /// Hosts whose best region has no digit are moved to the nearest region that has one (see <see cref="Encodable"/>).
    /// </summary>
    public static class RoomCode
    {
        public const int SessionLength = 5;
        public const int CodeLength = SessionLength + 1;

        /// <summary>Region for each final digit (0-9).</summary>
        private static readonly string[] RegionByDigit = { "in", "asia", "eu", "us", "usw", "jp", "kr", "au", "sa", "uae" };

        private static readonly System.Random Random = new System.Random();

        /// <summary>A fresh random session name (5 digits).</summary>
        public static string NewSessionName()
        {
            var sb = new StringBuilder(SessionLength);
            lock (Random)
            {
                for (int i = 0; i < SessionLength; i++) sb.Append((char)('0' + Random.Next(10)));
            }
            return sb.ToString();
        }

        /// <summary>True if <paramref name="region"/> has a code digit.</summary>
        public static bool Encodable(string region) => DigitOf(region) >= 0;

        /// <summary>Nearest region that has a code digit, for hosts whose best region has none.</summary>
        public static string NearestEncodable(string region)
        {
            switch ((region ?? string.Empty).ToLowerInvariant())
            {
                case "hk": return "asia";
                case "tr": return "eu";
                case "za": return "eu";
                case "cae": return "us";
                case "ussc": return "us";
                default: return Encodable(region) ? region : "in";
            }
        }

        /// <summary>6-digit display code for a session name and a Photon region token (e.g. "in").</summary>
        public static string ToDisplay(string sessionName, string region)
        {
            int digit = DigitOf(region);
            return digit >= 0 ? sessionName + (char)('0' + digit) : sessionName;
        }

        private static int DigitOf(string region)
        {
            for (int i = 0; i < RegionByDigit.Length; i++)
            {
                if (string.Equals(RegionByDigit[i], region, System.StringComparison.OrdinalIgnoreCase)) return i;
            }
            return -1;
        }

        /// <summary>
        /// Parses user input ("482915", "482 915", "482-915") into a session name and region.
        /// Returns false with a user-facing message when the format is wrong.
        /// </summary>
        public static bool TryParse(string input, out string sessionName, out string region, out string error)
        {
            sessionName = null;
            region = null;
            error = null;

            var clean = new StringBuilder();
            foreach (char c in input ?? string.Empty)
            {
                if (c == '-' || char.IsWhiteSpace(c)) continue;
                if (c < '0' || c > '9')
                {
                    error = "Room codes are 6 digits, e.g. 482915.";
                    return false;
                }
                clean.Append(c);
            }

            string code = clean.ToString();
            if (code.Length != CodeLength)
            {
                error = "Room codes are 6 digits, e.g. 482915.";
                return false;
            }

            sessionName = code.Substring(0, SessionLength);
            region = RegionByDigit[code[SessionLength] - '0'];
            return true;
        }
    }
}
