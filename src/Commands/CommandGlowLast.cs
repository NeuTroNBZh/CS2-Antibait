using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Commands;
using Antibait.Modules;

namespace Antibait.Commands;

public static class CommandGlowLast
{
    public static void OnCommand(CCSPlayerController? caller, CommandInfo command)
    {
        if (!CheckPermission(caller)) return;
        ToggleLastAlive(caller);
    }

    public static void ToggleLastAlive(CCSPlayerController? admin)
    {
        Globals.LastAliveEnabled = !Globals.LastAliveEnabled;
        string state = Globals.LastAliveEnabled ? "ACTIVÉ" : "DÉSACTIVÉ";

        if (!Globals.LastAliveEnabled)
        {
            // Retirer les glows "dernier vivant" des joueurs qui ne sont pas permanents
            foreach (var team in new[] {
                CounterStrikeSharp.API.Modules.Utils.CsTeam.Terrorist,
                CounterStrikeSharp.API.Modules.Utils.CsTeam.CounterTerrorist })
            {
                ulong lastId = Globals.LastAliveByTeam[team];
                if (lastId == 0 || Globals.PermanentGlowPlayers.Contains(lastId)) continue;

                var player = Util.GetValidPlayers().FirstOrDefault(p => p.SteamID == lastId);
                if (player != null)
                    GlowModule.RemoveGlow(player);

                Globals.LastAliveByTeam[team] = 0;
            }
        }
        else
        {
            // Appliquer immédiatement si un dernier vivant existe déjà
            GlowModule.CheckLastAlive();
        }

        Util.PrintToChat(admin, $"Glow dernier vivant : {state}.");
    }

    private static bool CheckPermission(CCSPlayerController? caller)
    {
        if (caller == null) return true;
        if (!AdminManager.PlayerHasPermissions(caller, Globals.Config.AdminPermission))
        {
            Util.PrintToChat(caller, "Permission refusée.");
            return false;
        }
        return true;
    }
}
