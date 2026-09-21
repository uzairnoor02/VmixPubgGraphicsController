#nullable enable
using System;
using System.Collections.Generic;

namespace VmixGraphicsBusiness.Tenancy
{
    /// <summary>
    /// One issued credential. Kept as a record rather than a bare string column so that rotation
    /// is expressible: a rotated key is retired with a grace window instead of deleted, because
    /// the alternative is rotating a key mid-broadcast and taking the graphics dark.
    /// </summary>
    public sealed class IssuedKey
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public TournamentKeyKind Kind { get; set; }

        /// <summary>
        /// Only ever populated for <see cref="TournamentKeyKind.Overlay"/> — see the note on
        /// <see cref="TournamentKey"/> for why overlay tokens are stored in the clear and agent
        /// keys are not.
        /// </summary>
        public string? Plaintext { get; set; }

        /// <summary>SHA-256 of the key. The only stored form for agent and dashboard keys.</summary>
        public string Hash { get; set; } = string.Empty;

        /// <summary>Safe-to-display form, for UI lists and log lines.</summary>
        public string Mask { get; set; } = string.Empty;

        public string? Label { get; set; }
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
        public DateTime? LastUsedAtUtc { get; set; }

        /// <summary>Set when the key is rotated or revoked. Null means current.</summary>
        public DateTime? RetiredAtUtc { get; set; }

        /// <summary>
        /// How long after retirement the key keeps working. Rotation uses a non-zero window so a
        /// vMix Browser Source or a running agent does not fail the moment someone clicks Rotate;
        /// an explicit revoke sets this to zero.
        /// </summary>
        public int GraceSeconds { get; set; }

        public bool IsCurrent => RetiredAtUtc == null;

        public bool IsUsableAt(DateTime utcNow)
        {
            if (RetiredAtUtc == null) return true;
            return utcNow <= RetiredAtUtc.Value.AddSeconds(GraceSeconds);
        }

        /// <summary>True once the key can never be used again — safe to prune from the snapshot.</summary>
        public bool IsExpired(DateTime utcNow) => RetiredAtUtc != null && !IsUsableAt(utcNow);
    }

    /// <summary>
    /// A tenant. Today one tournament is one tenant; when organisations arrive,
    /// <see cref="OrgId"/> becomes the billing/ownership boundary above this.
    /// </summary>
    public sealed class TournamentRecord
    {
        /// <summary>
        /// Stable internal identifier. This is the value that appears in logs, metrics and support
        /// tickets, which is exactly why it must never be the thing that grants access.
        /// </summary>
        public string Id { get; set; } = Guid.NewGuid().ToString("N");

        public string Name { get; set; } = string.Empty;
        public string? OrgId { get; set; }
        public bool Enabled { get; set; } = true;
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// When this tournament is scheduled to be on air. Two uses: the dashboard can warn that a
        /// deploy is about to land during someone's live event, and an expired tournament can be
        /// disabled without a human remembering to.
        /// </summary>
        public DateTime? ScheduledStartUtc { get; set; }
        public DateTime? ScheduledEndUtc { get; set; }

        public List<IssuedKey> Keys { get; set; } = new List<IssuedKey>();

        /// <summary>The token an operator should currently be pasting into vMix.</summary>
        public IssuedKey? CurrentKey(TournamentKeyKind kind)
        {
            for (int i = Keys.Count - 1; i >= 0; i--)
                if (Keys[i].Kind == kind && Keys[i].IsCurrent) return Keys[i];
            return null;
        }

        /// <summary>True while the scheduled window is open (or when no window was set).</summary>
        public bool IsLiveWindow(DateTime utcNow)
        {
            if (ScheduledStartUtc.HasValue && utcNow < ScheduledStartUtc.Value) return false;
            if (ScheduledEndUtc.HasValue && utcNow > ScheduledEndUtc.Value) return false;
            return true;
        }
    }
}
