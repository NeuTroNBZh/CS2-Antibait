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

        string query = command.ArgString.Trim();
        if (string.IsNullOrWhiteSpace(query))
        {
            Util.PrintToChat(caller, "Usage : !antibait_last <nom>");
            return;
        }

        if (!Util.TryResolveSinglePlayer(query, out var target, out var error))
        {
            Util.PrintToChat(caller, error);
            return;
        }

        ToggleLastAlive(caller, target);
    }

    public static void ToggleLastAlive(CCSPlayerController? admin, CCSPlayerController target)
    {
        string adminName = admin?.PlayerName ?? "Console";

        if (Globals.LastAliveWatched.Remove(target.SteamID))
        {
            // Si ce joueur était actuellement en surbrillance "dernier T", on retire le glow
            if (Globals.LastAliveHighlighted == target.SteamID)
            {
                Globals.LastAliveHighlighted = 0;
                if (!Globals.PermanentGlowPlayers.Contains(target.SteamID))
                    GlowModule.RemoveGlow(target);
            }

            Util.PrintToChat(admin,   $"Suivi dernier T désactivé pour {target.PlayerName}.");
            Util.PrintToChat(target,  $"{adminName} a désactivé votre suivi dernier T.");
        }
        else
        {
            Globals.LastAliveWatched.Add(target.SteamID);
            Util.PrintToChat(admin,   $"Suivi dernier T activé pour {target.PlayerName}.");
            Util.PrintToChat(target,  $"{adminName} a activé votre suivi dernier T.");

            // Appliquer immédiatement si ce joueur est déjà le seul T vivant
            GlowModule.CheckLastAlive();
        }
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
