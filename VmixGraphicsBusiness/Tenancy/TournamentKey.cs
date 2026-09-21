#nullable enable
using System;
using System.Security.Cryptography;
using System.Text;

namespace VmixGraphicsBusiness.Tenancy
{
    /// <summary>
    /// The kinds of credential this system issues. Each kind carries its own prefix so that a
    /// string found in a log line, a screenshot or a support ticket can be identified — and
    /// revoked — without guessing what it opens.
    /// </summary>
    public enum TournamentKeyKind
    {
        /// <summary>
        /// Goes in the <c>/overlay/{token}</c> URL that gets pasted into vMix as a Browser Source.
        /// A vMix Browser Source cannot log in, so this is a capability URL: whoever holds it can
        /// watch that tournament's live overlay. It must therefore be unguessable and rotatable,
        /// and it must NOT be the tournament id (which appears in logs and support tickets).
        /// </summary>
        Overlay,

        /// <summary>Pairs one ingest agent install to one tournament. Sent as <c>X-Agent-Key</c>.</summary>
        Agent,

        /// <summary>Dashboard / API access for a human operator.</summary>
        Dashboard
    }

    /// <summary>
    /// Generation, parsing, masking and hashing of tournament credentials.
    ///
    /// Format: <c>{prefix}_{body}_{check}</c> — e.g. <c>ovl_7Kq2mZ...x9_4Bf</c>
    ///  * prefix — 3 chars, identifies the kind
    ///  * body   — 27 base62 chars, ~160 bits from a cryptographic RNG
    ///  * check  — 3 base62 chars derived from the body, so a mistyped or truncated key is
    ///             rejected locally instead of turning into a lookup miss that looks identical
    ///             to "revoked".
    ///
    /// Nothing here is reversible: <see cref="Hash"/> is what gets stored for agent and dashboard
    /// keys. Overlay tokens are the deliberate exception — they live in a URL the operator has to
    /// be able to re-copy from the dashboard for the life of the tournament, so the registry keeps
    /// their plaintext. That is a considered trade-off: an overlay token grants read-only view of
    /// one tournament's graphics, while an agent key grants write access to its live state.
    /// </summary>
    public static class TournamentKey
    {
        public const string OverlayPrefix   = "ovl";
        public const string AgentPrefix     = "agt";
        public const string DashboardPrefix = "dsh";

        private const string Alphabet   = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";
        private const int    BodyChars  = 27;
        private const int    CheckChars = 3;

        public static string PrefixFor(TournamentKeyKind kind) => kind switch
        {
            TournamentKeyKind.Overlay   => OverlayPrefix,
            TournamentKeyKind.Agent     => AgentPrefix,
            TournamentKeyKind.Dashboard => DashboardPrefix,
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };

        public static bool TryKindFromPrefix(string prefix, out TournamentKeyKind kind)
        {
            switch (prefix)
            {
                case OverlayPrefix:   kind = TournamentKeyKind.Overlay;   return true;
                case AgentPrefix:     kind = TournamentKeyKind.Agent;     return true;
                case DashboardPrefix: kind = TournamentKeyKind.Dashboard; return true;
                default:              kind = default;                     return false;
            }
        }

        /// <summary>Issues a new credential. The returned string is the only time the full value exists.</summary>
        public static string Generate(TournamentKeyKind kind)
        {
            var prefix = PrefixFor(kind);
            var body = new char[BodyChars];
            for (int i = 0; i < BodyChars; i++)
            {
                // GetInt32 is rejection-sampled internally, so this is unbiased across the alphabet.
                body[i] = Alphabet[RandomNumberGenerator.GetInt32(0, Alphabet.Length)];
            }
            var bodyStr = new string(body);
            return prefix + "_" + bodyStr + "_" + Checksum(prefix, bodyStr);
        }

        /// <summary>
        /// Validates shape and checksum only. A true result means "this is a well-formed key of
        /// that kind", never "this key is valid" — that is the registry's job.
        /// </summary>
        public static bool TryParse(string? candidate, out TournamentKeyKind kind)
        {
            kind = default;
            if (string.IsNullOrWhiteSpace(candidate)) return false;

            var parts = candidate!.Trim().Split('_');
            if (parts.Length != 3) return false;
            if (!TryKindFromPrefix(parts[0], out kind)) return false;
            if (parts[1].Length != BodyChars || parts[2].Length != CheckChars) return false;

            for (int i = 0; i < parts[1].Length; i++)
                if (Alphabet.IndexOf(parts[1][i]) < 0) return false;

            return string.Equals(Checksum(parts[0], parts[1]), parts[2], StringComparison.Ordinal);
        }

        /// <summary>What gets persisted for agent and dashboard keys.</summary>
        public static string Hash(string key)
        {
            if (key == null) throw new ArgumentNullException(nameof(key));
            using var sha = SHA256.Create();
            var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(key.Trim()));
            return "sha256:" + Convert.ToBase64String(bytes);
        }

        /// <summary>Constant-time comparison, so a wrong key cannot be narrowed down by timing.</summary>
        public static bool HashMatches(string? presentedKey, string? storedHash)
        {
            if (string.IsNullOrEmpty(presentedKey) || string.IsNullOrEmpty(storedHash)) return false;
            var computed = Encoding.UTF8.GetBytes(Hash(presentedKey!));
            var stored   = Encoding.UTF8.GetBytes(storedHash!);
            if (computed.Length != stored.Length) return false;
            return CryptographicOperations.FixedTimeEquals(computed, stored);
        }

        /// <summary>
        /// Display form, safe to log and safe to show in a UI list: <c>agt_…x9c2_4Bf</c>.
        /// Keeps enough tail to let an operator tell two keys apart when deciding which to revoke.
        /// </summary>
        public static string Mask(string? key)
        {
            if (string.IsNullOrWhiteSpace(key)) return "(none)";
            var k = key!.Trim();
            var parts = k.Split('_');
            if (parts.Length != 3) return k.Length <= 6 ? "…" : k.Substring(0, 3) + "…";
            var body = parts[1];
            var tail = body.Length <= 4 ? body : body.Substring(body.Length - 4);
            return parts[0] + "_…" + tail + "_" + parts[2];
        }

        private static string Checksum(string prefix, string body)
        {
            using var sha = SHA256.Create();
            var digest = sha.ComputeHash(Encoding.UTF8.GetBytes(prefix + ":" + body));
            // 16 bits of the digest, rendered as 3 base62 chars (62^3 = 238,328 > 65,536).
            int value = (digest[0] << 8) | digest[1];
            var chars = new char[CheckChars];
            for (int i = CheckChars - 1; i >= 0; i--)
            {
                chars[i] = Alphabet[value % Alphabet.Length];
                value /= Alphabet.Length;
            }
            return new string(chars);
        }
    }
}
