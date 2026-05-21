using System.Diagnostics.CodeAnalysis;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;

namespace Antibait;

public static class Util
{
    public static string? GetPlayerModel(CCSPlayerController player)
        => player.PlayerPawn?.Value?.CBodyComponent?.SceneNode?.GetSkeletonInstance()?.ModelState?.ModelName;

    public static bool IsPlayerEntityValid([NotNullWhen(true)] CCSPlayerController? p)
        => p != null && p.IsValid && !p.IsHLTV;

    public static bool IsPlayerValid([NotNullWhen(true)] CCSPlayerController? p)
        => IsPlayerEntityValid(p) &&
           p.PlayerPawn != null &&
           p.PlayerPawn.IsValid &&
           p.Connected == PlayerConnectedState.PlayerConnected;

    public static List<CCSPlayerController> GetValidPlayers()
        => Utilities.GetPlayers().Where(IsPlayerValid).ToList();

    public static List<CCSPlayerController> FindPlayerMatches(string query, bool includeBots = false)
    {
        if (string.IsNullOrWhiteSpace(query)) return new();

        query = query.Trim();
        IEnumerable<CCSPlayerController> source = GetValidPlayers();
        if (!includeBots)
            source = source.Where(p => !p.IsBot);

        var candidates = source.ToList();

        var exact = candidates.Where(p => p.PlayerName.Equals(query, StringComparison.OrdinalIgnoreCase)).ToList();
        if (exact.Count > 0) return exact;

        var starts = candidates.Where(p => p.PlayerName.StartsWith(query, StringComparison.OrdinalIgnoreCase)).ToList();
        if (starts.Count > 0) return starts;

        return candidates.Where(p => p.PlayerName.Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();
    }

    public static bool TryResolveSinglePlayer(
        string query,
        [NotNullWhen(true)] out CCSPlayerController? player,
        out string error,
        bool includeBots = false)
    {
        player = null;
        error  = string.Empty;

        var matches = FindPlayerMatches(query, includeBots);

        if (matches.Count == 0)  { error = $"Joueur introuvable : '{query}'."; return false; }
        if (matches.Count  > 1)
        {
            string names = string.Join(", ", matches.Take(5).Select(p => p.PlayerName));
            if (matches.Count > 5) names += ", …";
            error = $"Plusieurs correspondances : {names}. Soyez plus précis.";
            return false;
        }

        player = matches[0];
        return true;
    }

    public static void PrintToChat(CCSPlayerController? player, string message)
    {
        if (player == null)
            Server.PrintToConsole(message);
        else
            player.PrintToChat($" {ChatColors.Green}[Antibait]{ChatColors.White} {message}");
    }

    public static void BroadcastToChat(string message)
    {
        foreach (var p in GetValidPlayers())
            PrintToChat(p, message);
    }
}
