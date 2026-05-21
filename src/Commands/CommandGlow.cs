using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Commands;
using Antibait.Modules;

namespace Antibait.Commands;

public static class CommandGlow
{
    public static void OnCommand(CCSPlayerController? caller, CommandInfo command)
    {
        if (!CheckPermission(caller)) return;

        string query = command.ArgString.Trim();
        if (string.IsNullOrWhiteSpace(query))
        {
            Util.PrintToChat(caller, "Usage : !antibait_glow <nom>");
            return;
        }

        if (!Util.TryResolveSinglePlayer(query, out var target, out var error))
        {
            Util.PrintToChat(caller, error);
            return;
        }

        ToggleGlow(caller, target);
    }

    public static void ToggleGlow(CCSPlayerController? admin, CCSPlayerController target)
    {
        string adminName = admin?.PlayerName ?? "Console";

        if (Globals.PermanentGlowPlayers.Remove(target.SteamID))
        {
            // Retirer le glow sauf si ce joueur est encore surbrillancé comme dernier T vivant
            if (Globals.LastAliveHighlighted != target.SteamID)
                GlowModule.RemoveGlow(target);

            Util.PrintToChat(admin, $"Glow permanent désactivé pour {target.PlayerName}.");
            Util.PrintToChat(target, $"{adminName} a désactivé votre glow permanent.");
        }
        else
        {
            Globals.PermanentGlowPlayers.Add(target.SteamID);
            GlowModule.ScheduleGlow(target);
            Util.PrintToChat(admin, $"Glow permanent activé pour {target.PlayerName}.");
            Util.PrintToChat(target, $"{adminName} a activé votre glow permanent.");
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
