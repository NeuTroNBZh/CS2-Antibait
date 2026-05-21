using System.Drawing;
using System.Linq;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;
using Antibait.Models;

namespace Antibait.Modules;

public static class GlowModule
{
    // Slots en attente de création d'entité (évite les doublons de timer)
    private static readonly HashSet<int> _pendingSlots = new();

    // True uniquement entre EventRoundStart et EventRoundEnd.
    // EventPlayerSpawn fire AVANT EventRoundStart, pendant le scan d'entités du moteur
    // (breakerandopendoor). Créer des CDynamicProp pendant cette fenêtre provoque le crash
    // "WriteEnterPVS: GetEntServerClass failed". On bloque la création jusqu'à EventRoundStart.
    private static bool _roundInProgress = false;

    // ── CheckTransmit ────────────────────────────────────────────────────────

    public static void OnPlayerTransmit(CCheckTransmitInfo info, CCSPlayerController viewer)
    {
        if (!Util.IsPlayerValid(viewer)) return;

        foreach (var (target, data) in Globals.GlowData.ToList())
        {
            if (!Util.IsPlayerEntityValid(target) || !data.GlowEnt.IsValid || !data.ModelRelay.IsValid)
            {
                RemoveGlow(target);
                continue;
            }

            // Le joueur ne se voit pas lui-même, et les morts n'ont pas de glow actif
            if (target.Slot == viewer.Slot || !target.PawnIsAlive)
            {
                info.TransmitEntities.Remove(data.ModelRelay);
                info.TransmitEntities.Remove(data.GlowEnt);
                continue;
            }

            // Tout le monde voit le glow (sauf la cible elle-même)
            info.TransmitEntities.Add(data.ModelRelay);
            info.TransmitEntities.Add(data.GlowEnt);
        }
    }

    // ── Event handlers ───────────────────────────────────────────────────────

    public static HookResult OnRoundStart(EventRoundStart @event, GameEventInfo info)
    {
        // Reset états "dernier vivant" — les glows permanents sont conservés
        Globals.LastAliveByTeam[CsTeam.Terrorist]        = 0;
        Globals.LastAliveByTeam[CsTeam.CounterTerrorist] = 0;

        // EventRoundStart fire après les scans de breakerandopendoor : création sûre
        _roundInProgress = true;

        Globals.Plugin.AddTimer(0.5f, () =>
        {
            foreach (var player in Util.GetValidPlayers().Where(p => p.PawnIsAlive))
                ScheduleGlow(player);
        });

        return HookResult.Continue;
    }

    public static HookResult OnRoundEnd(EventRoundEnd @event, GameEventInfo info)
    {
        _roundInProgress = false;
        _pendingSlots.Clear();

        foreach (var player in Globals.GlowData.Keys.ToList())
            RemoveGlow(player);

        Globals.LastAliveByTeam[CsTeam.Terrorist]        = 0;
        Globals.LastAliveByTeam[CsTeam.CounterTerrorist] = 0;

        return HookResult.Continue;
    }

    public static HookResult OnPlayerSpawn(EventPlayerSpawn @event, GameEventInfo info)
    {
        var player = @event.Userid;
        if (!Util.IsPlayerEntityValid(player)) return HookResult.Continue;

        // Spawn en cours de round (respawn retake) : on peut créer les entités
        if (_roundInProgress)
            ScheduleGlow(player);

        return HookResult.Continue;
    }

    public static HookResult OnPlayerDeath(EventPlayerDeath @event, GameEventInfo info)
    {
        var player = @event.Userid;
        if (player == null) return HookResult.Continue;

        _pendingSlots.Remove(player.Slot);
        RemoveGlow(player);

        if (Globals.LastAliveEnabled)
            Globals.Plugin.AddTimer(0.1f, CheckLastAlive);

        return HookResult.Continue;
    }

    public static HookResult OnPlayerDisconnect(EventPlayerDisconnect @event, GameEventInfo info)
    {
        var player = @event.Userid;
        if (player == null) return HookResult.Continue;

        _pendingSlots.Remove(player.Slot);
        RemoveGlow(player);
        Globals.PermanentGlowPlayers.Remove(player.SteamID);

        if (Globals.LastAliveEnabled)
            Globals.Plugin.AddTimer(0.1f, CheckLastAlive);

        return HookResult.Continue;
    }

    // ── Logique dernier vivant ───────────────────────────────────────────────

    public static void CheckLastAlive()
    {
        foreach (var team in new[] { CsTeam.Terrorist, CsTeam.CounterTerrorist })
        {
            var alive = Util.GetValidPlayers()
                .Where(p => !p.IsBot && p.Team == team && p.PawnIsAlive)
                .ToList();

            if (alive.Count != 1)
            {
                // Plus exactement 1 vivant — retirer le highlight dernier vivant
                ClearLastAliveForTeam(team);
                continue;
            }

            var last = alive[0];
            if (Globals.LastAliveByTeam[team] == last.SteamID) continue; // Déjà traité

            ClearLastAliveForTeam(team);
            Globals.LastAliveByTeam[team] = last.SteamID;

            // Si le joueur a déjà un glow permanent, pas besoin de re-scheduler
            if (!Globals.PermanentGlowPlayers.Contains(last.SteamID) && _roundInProgress)
                ScheduleGlow(last);
        }
    }

    private static void ClearLastAliveForTeam(CsTeam team)
    {
        ulong prevId = Globals.LastAliveByTeam[team];
        if (prevId == 0) return;

        Globals.LastAliveByTeam[team] = 0;

        // Retirer le glow seulement si ce joueur n'est pas non plus permanent
        if (Globals.PermanentGlowPlayers.Contains(prevId)) return;

        var prev = Util.GetValidPlayers().FirstOrDefault(p => p.SteamID == prevId);
        if (prev != null)
            RemoveGlow(prev);
    }

    // ── Création / suppression des entités glow ─────────────────────────────

    public static void ScheduleGlow(CCSPlayerController player)
    {
        if (!NeedsGlow(player, out _)) return;

        RemoveGlow(player);

        if (!_pendingSlots.Add(player.Slot)) return;

        Globals.Plugin.AddTimer(0.5f, () =>
        {
            _pendingSlots.Remove(player.Slot);

            // Le timer peut fire pendant la fenêtre de scan du prochain round
            if (!_roundInProgress) return;
            if (!Util.IsPlayerValid(player) || !player.PawnIsAlive) return;

            RemoveGlow(player);
            CreateGlow(player);
        });
    }

    private static void CreateGlow(CCSPlayerController player)
    {
        if (Globals.GlowData.ContainsKey(player)) return;

        var pawn = player.PlayerPawn?.Value;
        if (pawn == null || !pawn.IsValid || !player.PawnIsAlive) return;

        string? model = Util.GetPlayerModel(player);
        if (string.IsNullOrWhiteSpace(model) || !model.EndsWith(".vmdl", StringComparison.OrdinalIgnoreCase))
            return;

        var modelRelay = Utilities.CreateEntityByName<CDynamicProp>("prop_dynamic");
        if (modelRelay == null) return;

        var glowEntity = Utilities.CreateEntityByName<CDynamicProp>("prop_dynamic");
        if (glowEntity == null)
        {
            modelRelay.DispatchSpawn();
            modelRelay.Remove();
            return;
        }

        // Spawnflags=256 avant DispatchSpawn (non-réseau uniquement)
        modelRelay.Spawnflags = 256;
        glowEntity.Spawnflags = 256;

        modelRelay.SetModel(model);
        glowEntity.SetModel(model);

        modelRelay.DispatchSpawn();
        glowEntity.DispatchSpawn();

        Globals.GlowData[player] = new GlowData { GlowEnt = glowEntity, ModelRelay = modelRelay };

        // Propriétés réseau appliquées dans la frame suivante (entités hors liste de staging)
        Server.NextFrame(() =>
        {
            if (!Globals.GlowData.ContainsKey(player) || !modelRelay.IsValid || !glowEntity.IsValid)
            {
                RemoveGlow(player);
                return;
            }
            if (!Util.IsPlayerValid(player) || !player.PawnIsAlive)
            {
                RemoveGlow(player);
                return;
            }
            var livePawn = player.PlayerPawn?.Value;
            if (livePawn == null || !livePawn.IsValid)
            {
                RemoveGlow(player);
                return;
            }

            if (!NeedsGlow(player, out var color))
            {
                RemoveGlow(player);
                return;
            }

            modelRelay.Render     = Color.Transparent;
            modelRelay.RenderMode = RenderMode_t.kRenderNone;
            glowEntity.Render     = Color.FromArgb(1, 0, 0, 0);

            glowEntity.Glow.GlowRange         = 0;    // portée illimitée
            glowEntity.Glow.GlowRangeMin      = 0;
            glowEntity.Glow.GlowColorOverride  = color;
            glowEntity.Glow.GlowTeam           = -1;  // visible par toutes les équipes
            glowEntity.Glow.GlowType           = 3;   // à travers les murs et les smokes

            modelRelay.AcceptInput("FollowEntity", livePawn,    modelRelay, "!activator");
            glowEntity.AcceptInput("FollowEntity", modelRelay,  glowEntity, "!activator");
        });
    }

    public static void RemoveGlow(CCSPlayerController player)
    {
        if (!Globals.GlowData.TryGetValue(player, out var data)) return;

        if (data.GlowEnt.IsValid)    data.GlowEnt.Remove();
        if (data.ModelRelay.IsValid) data.ModelRelay.Remove();

        Globals.GlowData.Remove(player);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    // Détermine si le joueur doit être en surbrillance et sa couleur
    public static bool NeedsGlow(CCSPlayerController player, out Color color)
    {
        color = Color.Transparent;

        if (Globals.PermanentGlowPlayers.Contains(player.SteamID))
        {
            var c = Globals.Config;
            color = Color.FromArgb(255, c.PermanentR, c.PermanentG, c.PermanentB);
            return true;
        }

        if (Globals.LastAliveEnabled)
        {
            bool isLastT  = Globals.LastAliveByTeam[CsTeam.Terrorist]        == player.SteamID;
            bool isLastCT = Globals.LastAliveByTeam[CsTeam.CounterTerrorist] == player.SteamID;

            if (isLastT || isLastCT)
            {
                var c = Globals.Config;
                color = isLastT
                    ? Color.FromArgb(255, c.LastAliveT_R,  c.LastAliveT_G,  c.LastAliveT_B)
                    : Color.FromArgb(255, c.LastAliveCT_R, c.LastAliveCT_G, c.LastAliveCT_B);
                return true;
            }
        }

        return false;
    }

    // ── Setup / Cleanup ──────────────────────────────────────────────────────

    public static void Setup()
    {
        Globals.Plugin.RegisterEventHandler<EventRoundStart>(OnRoundStart);
        Globals.Plugin.RegisterEventHandler<EventRoundEnd>(OnRoundEnd);
        Globals.Plugin.RegisterEventHandler<EventPlayerDeath>(OnPlayerDeath);
        Globals.Plugin.RegisterEventHandler<EventPlayerDisconnect>(OnPlayerDisconnect);
        Globals.Plugin.RegisterEventHandler<EventPlayerSpawn>(OnPlayerSpawn, HookMode.Post);

        // Hot-reload : un round est déjà en cours
        Globals.Plugin.AddTimer(0.5f, () =>
        {
            _roundInProgress = true;
            foreach (var player in Util.GetValidPlayers().Where(p => p.PawnIsAlive))
                ScheduleGlow(player);
        });
    }

    public static void Cleanup()
    {
        _roundInProgress = false;
        _pendingSlots.Clear();

        foreach (var data in Globals.GlowData.Values)
        {
            if (data.GlowEnt.IsValid)    data.GlowEnt.Remove();
            if (data.ModelRelay.IsValid) data.ModelRelay.Remove();
        }

        Globals.GlowData.Clear();
    }
}
