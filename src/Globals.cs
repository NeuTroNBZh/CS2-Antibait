using System;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;
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

    // Toggle "dernier vivant" (persiste entre les rounds)
    public static bool LastAliveEnabled { get; set; } = false;

    // SteamID du joueur actuellement mis en évidence comme dernier vivant, par équipe
    // 0 = aucun
    public static Dictionary<CsTeam, ulong> LastAliveByTeam { get; } = new()
    {
        [CsTeam.Terrorist]        = 0,
        [CsTeam.CounterTerrorist] = 0,
    };

    // Entités CDynamicProp actives (glow) par joueur — recréées à chaque round
    public static Dictionary<CCSPlayerController, GlowData> GlowData { get; } = new();

    // Nettoyage complet (hot-reload)
    public static void Reset()
    {
        PermanentGlowPlayers.Clear();
        LastAliveEnabled = false;
        LastAliveByTeam[CsTeam.Terrorist]        = 0;
        LastAliveByTeam[CsTeam.CounterTerrorist] = 0;
        GlowData.Clear();
    }
}
