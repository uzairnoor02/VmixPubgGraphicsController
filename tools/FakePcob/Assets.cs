using System.Drawing;
using System.Drawing.Drawing2D;
using System.Text.Json;

namespace FakePcob;

public sealed record FakeTeam(int TeamId, string Name, string Tag, Color Color);
public sealed record FakePlayer(long UId, string Name, int TeamId);

/// Task 8 - `assets --out out [--from-seed m1]`: fake team logos, player photos and a teams.json
/// in the exact Load-Teams format, so a tester can stand up a full 16-team roster with images
/// without touching anything under real customer image folders.
public static class AssetsCommand
{
    private static readonly string[] Adjectives = { "Crimson", "Silent", "Iron", "Shadow", "Northern", "Rogue", "Frozen", "Golden", "Wild", "Savage", "Lunar", "Rapid", "Ghost", "Ember", "Storm", "Vortex" };
    private static readonly string[] Nouns = { "Wolves", "Falcons", "Reapers", "Titans", "Vipers", "Raiders", "Knights", "Hunters", "Phantoms", "Panthers", "Dragons", "Rangers", "Outlaws", "Sentinels", "Nomads", "Marauders" };
    private static readonly string[] FirstNames = { "Ari", "Bo", "Cai", "Dex", "Emi", "Fen", "Gio", "Hux", "Ira", "Jax", "Kai", "Lio", "Miko", "Nox", "Omi", "Pax" };

    public static Task<int> RunAsync(Args opts)
    {
        var outDir = opts.Get("out", "out");
        var fromSeed = opts.GetOrNull("from-seed");
        var imagesDir = Path.Combine(outDir, "images");
        Directory.CreateDirectory(imagesDir);

        List<FakeTeam> teams;
        List<FakePlayer> players;

        if (fromSeed is not null)
        {
            var seedPlayers = Seeds.Load(fromSeed);
            var rngColor = new Random(1234);
            teams = seedPlayers.GroupBy(p => p.TeamId).OrderBy(g => g.Key)
                .Select((g, i) => new FakeTeam(g.Key, g.First().TeamName, TagFromName(g.First().TeamName), HueColor(i, Math.Max(1, seedPlayers.Select(p => p.TeamId).Distinct().Count()))))
                .ToList();
            players = seedPlayers.Select(p => new FakePlayer(p.UId, p.PlayerName, p.TeamId)).ToList();
            Console.WriteLine($"assets --from-seed {fromSeed}: {teams.Count} real teams / {players.Count} real players (names/ids only - no other seed data read).");
        }
        else
        {
            var rng = new Random(7);
            teams = new List<FakeTeam>();
            var usedTags = new HashSet<string>();
            for (int i = 0; i < 16; i++)
            {
                var name = $"{Adjectives[rng.Next(Adjectives.Length)]} {Nouns[rng.Next(Nouns.Length)]}";
                var tag = TagFromName(name);
                var suffix = 0;
                var uniqueTag = tag;
                while (!usedTags.Add(uniqueTag)) { suffix++; uniqueTag = tag + suffix; }
                teams.Add(new FakeTeam(i + 1, name, uniqueTag, HueColor(i, 16)));
            }
            players = new List<FakePlayer>();
            long uid = 9000000000;
            foreach (var team in teams)
            {
                for (int m = 0; m < 4; m++)
                {
                    var pname = $"{FirstNames[(uid % FirstNames.Length + m) % FirstNames.Length]}{team.TeamId}{m}";
                    players.Add(new FakePlayer(uid++, pname, team.TeamId));
                }
            }
        }

        foreach (var team in teams)
        {
            using var bmp = RenderTeamLogo(team);
            bmp.Save(Path.Combine(imagesDir, $"{team.TeamId}.png"), System.Drawing.Imaging.ImageFormat.Png);
        }
        foreach (var player in players)
        {
            var team = teams.First(t => t.TeamId == player.TeamId);
            using var bmp = RenderPlayerPhoto(player, team.Color);
            bmp.Save(Path.Combine(imagesDir, $"{player.UId}.png"), System.Drawing.Imaging.ImageFormat.Png);
        }

        var teamsJson = new
        {
            tournament_name = "FAKE TEST CUP",
            stages = new[]
            {
                new
                {
                    stage_name = "Test Stage",
                    teams = teams.Select(t => new { team_id = t.TeamId.ToString(), team_name = t.Name }).ToArray(),
                },
            },
        };
        File.WriteAllText(Path.Combine(outDir, "teams.json"), JsonSerializer.Serialize(teamsJson, new JsonSerializerOptions { WriteIndented = true }));

        Console.WriteLine($"Wrote {teams.Count} team logos and {players.Count} player photos to {imagesDir}/, plus {outDir}/teams.json");
        Console.WriteLine("Copy images/* to the folders Recon Task 1 §10 found (PlayerImages / TeamLogosImages) - never write there directly.");
        return Task.FromResult(0);
    }

    private static string TagFromName(string name)
    {
        var words = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var tag = words.Length >= 2 ? (words[0][..Math.Min(2, words[0].Length)] + words[1][..Math.Min(2, words[1].Length)]) : name[..Math.Min(4, name.Length)];
        return tag.ToUpperInvariant();
    }

    private static Color HueColor(int index, int total)
    {
        var hue = (float)(index * 360.0 / Math.Max(1, total));
        return HsvToRgb(hue, 0.65f, 0.85f);
    }

    private static Color HsvToRgb(float h, float s, float v)
    {
        int hi = (int)(h / 60) % 6;
        float f = h / 60 - (int)(h / 60);
        float p = v * (1 - s), q = v * (1 - f * s), t = v * (1 - (1 - f) * s);
        (float r, float g, float b) = hi switch
        {
            0 => (v, t, p), 1 => (q, v, p), 2 => (p, v, t), 3 => (p, q, v), 4 => (t, p, v), _ => (v, p, q),
        };
        return Color.FromArgb(255, (int)(r * 255), (int)(g * 255), (int)(b * 255));
    }

    private static Bitmap RenderTeamLogo(FakeTeam team)
    {
        var bmp = new Bitmap(256, 256);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
        using var path = RoundedRect(new Rectangle(8, 8, 240, 240), 36);
        using var brush = new SolidBrush(team.Color);
        g.FillPath(brush, path);

        var fontSize = team.Tag.Length > 3 ? 56 : 68;
        using var font = new Font("Arial", fontSize, System.Drawing.FontStyle.Bold, GraphicsUnit.Pixel);
        using var textBrush = new SolidBrush(Color.White);
        var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        g.DrawString(team.Tag, font, textBrush, new RectangleF(0, 0, 256, 256), sf);
        return bmp;
    }

    private static Bitmap RenderPlayerPhoto(FakePlayer player, Color teamColor)
    {
        var bmp = new Bitmap(256, 256);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var dark1 = ControlPaint.Dark(teamColor, 0.3f);
        var dark2 = ControlPaint.Dark(teamColor, 0.7f);
        using (var gradBrush = new LinearGradientBrush(new Rectangle(0, 0, 256, 256), dark1, dark2, 45f))
        {
            g.FillRectangle(gradBrush, 0, 0, 256, 256);
        }
        // Simple silhouette: head circle + shoulder ellipse, a shade darker than the background.
        var silhouette = ControlPaint.Dark(dark2, 0.2f);
        using (var sBrush = new SolidBrush(silhouette))
        {
            g.FillEllipse(sBrush, 88, 50, 80, 80);
            g.FillEllipse(sBrush, 48, 150, 160, 140);
        }
        var initials = string.Concat(player.Name.Where(char.IsLetterOrDigit).Take(2)).ToUpperInvariant();
        if (initials.Length == 0) initials = "??";
        using var font = new Font("Arial", 40, System.Drawing.FontStyle.Bold, GraphicsUnit.Pixel);
        using var textBrush = new SolidBrush(Color.FromArgb(230, 255, 255, 255));
        var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Far };
        g.DrawString(initials, font, textBrush, new RectangleF(0, 0, 256, 246), sf);
        return bmp;
    }

    private static GraphicsPath RoundedRect(Rectangle bounds, int radius)
    {
        var path = new GraphicsPath();
        var d = radius * 2;
        path.AddArc(bounds.X, bounds.Y, d, d, 180, 90);
        path.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 90);
        path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
        path.AddArc(bounds.X, bounds.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }
}
