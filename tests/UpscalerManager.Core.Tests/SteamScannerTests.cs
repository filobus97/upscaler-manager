// Upscaler Manager - GPL-3.0-or-later. See repository LICENSE.
using System;
using System.IO;
using System.Linq;
using UpscalerManager.Core.Services;
using Xunit;

namespace UpscalerManager.Core.Tests;

/// <summary>One Steam library reached two ways is still one library, and a game in it one game.</summary>
public sealed class SteamScannerTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "um-steam-" + Guid.NewGuid().ToString("N"));

    public SteamScannerTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch { }
    }

    private string Library(string name, params (string AppId, string Dir)[] games)
    {
        var library = Path.Combine(_root, name);
        var steamapps = Path.Combine(library, "steamapps");
        Directory.CreateDirectory(steamapps);
        foreach (var (appId, dir) in games)
        {
            Directory.CreateDirectory(Path.Combine(steamapps, "common", dir));
            File.WriteAllText(Path.Combine(steamapps, $"appmanifest_{appId}.acf"),
                $"\"AppState\"\n{{\n\t\"appid\"\t\t\"{appId}\"\n\t\"name\"\t\t\"{dir}\"\n\t\"installdir\"\t\t\"{dir}\"\n}}\n");
        }
        return library;
    }

    [Fact]
    public void A_library_listed_with_a_trailing_slash_is_read_once()
    {
        var steam = Library("Steam", ("1086940", "Baldurs Gate 3"));
        File.WriteAllText(Path.Combine(steam, "steamapps", "libraryfolders.vdf"),
            $"\"libraryfolders\"\n{{\n\t\"0\"\n\t{{\n\t\t\"path\"\t\t\"{steam}/\"\n\t}}\n}}\n");

        var folders = SteamScanner.GetLibraryFolders(steam);

        Assert.Single(folders);
        Assert.Single(SteamScanner.GamesIn(folders));
    }

    [Fact]
    public void A_library_reached_through_a_link_is_read_once()
    {
        var steam = Library("Steam", ("1086940", "Baldurs Gate 3"));
        var link = Path.Combine(_root, "dot-steam");
        Directory.CreateSymbolicLink(link, steam);
        File.WriteAllText(Path.Combine(steam, "steamapps", "libraryfolders.vdf"),
            $"\"libraryfolders\"\n{{\n\t\"0\"\n\t{{\n\t\t\"path\"\t\t\"{steam}\"\n\t}}\n}}\n");

        Assert.Single(SteamScanner.GetLibraryFolders(link));
    }

    [Fact]
    public void A_game_seen_in_two_libraries_is_listed_once()
    {
        var first = Library("Steam", ("1086940", "Baldurs Gate 3"), ("2842040", "Outlaws"));
        var second = Library("Games", ("1086940", "Baldurs Gate 3"));

        var games = SteamScanner.GamesIn(new[] { first, second });

        Assert.Equal(new[] { "1086940", "2842040" }, games.Select(g => g.AppId).OrderBy(id => id));
    }
}
