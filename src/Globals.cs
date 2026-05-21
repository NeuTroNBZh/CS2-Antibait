using System;
using CounterStrikeSharp.API.Core;
using Antibait.Models;

namespace Antibait;

public static class Globals
{
    private static AntibaitPlugin? _plugin;
    public static AntibaitPlugin Plugin
    {
        get => _plugin ?? throw new InvalidOperationException("Globals.Plugin not initialized");
        set => _plugin = value;
    }

    private static AntibaitConfig? _config;
    public static AntibaitConfig Config
    {
        get => _config ?? throw new InvalidOperationException("Globals.Config not initialized");
        set => _config = value;
    }

    // SteamID64 des joueurs avec glow permanent (persiste entre les rounds)
    public static HashSet<ulong> PermanentGlowPlayers { get; } = new();

    // SteamID64 des joueurs surveillés pour le "dernier T vivant" (persiste entre les rounds)
    public static HashSet<ulong> LastAliveWatched { get; } = new();

    // SteamID du joueur actuellement surbrillancé comme dernier T vivant (0 = aucun, reset chaque round)
    public static ulong LastAliveHighlighted { get; set; } = 0;

    // Entités CDynamicProp actives (glow) par joueur — recréées à chaque round
    public static Dictionary<CCSPlayerController, GlowData> GlowData { get; } = new();

    // Nettoyage complet (hot-reload)
    public static void Reset()
    {
        PermanentGlowPlayers.Clear();
        LastAliveWatched.Clear();
        LastAliveHighlighted = 0;
        GlowData.Clear();
    }
}
