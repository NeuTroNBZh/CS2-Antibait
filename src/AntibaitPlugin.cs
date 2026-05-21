using System;
using System.Text.Json.Serialization;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes;
using CounterStrikeSharp.API.Core.Capabilities;
using Microsoft.Extensions.Logging;
using Antibait.Commands;
using Antibait.Modules;

namespace Antibait;

// ── Config ───────────────────────────────────────────────────────────────────

public class AntibaitConfig : BasePluginConfig
{
    [JsonPropertyName("AdminPermission")]
    public string AdminPermission { get; set; } = "@css/cheats";

    // Couleur du glow permanent
    [JsonPropertyName("PermanentGlow_R")] public byte PermanentR  { get; set; } = 255;
    [JsonPropertyName("PermanentGlow_G")] public byte PermanentG  { get; set; } = 50;
    [JsonPropertyName("PermanentGlow_B")] public byte PermanentB  { get; set; } = 50;

    // Couleur du glow "dernier T vivant"
    [JsonPropertyName("LastAlive_T_R")]   public byte LastAliveT_R  { get; set; } = 255;
    [JsonPropertyName("LastAlive_T_G")]   public byte LastAliveT_G  { get; set; } = 130;
    [JsonPropertyName("LastAlive_T_B")]   public byte LastAliveT_B  { get; set; } = 0;

}

// ── Plugin principal ─────────────────────────────────────────────────────────

[MinimumApiVersion(228)]
public class AntibaitPlugin : BasePlugin, IPluginConfig<AntibaitConfig>
{
    public override string ModuleName        => "Antibait";
    public override string ModuleVersion     => "1.2.0";
    public override string ModuleAuthor      => "NeuTroNBZh";
    public override string ModuleDescription => "Glow highlight (wallhack) pour joueurs ciblés — visible à travers murs et smokes.";

    public AntibaitConfig Config { get; set; } = new();

    public void OnConfigParsed(AntibaitConfig config)
    {
        Config         = config;
        Globals.Config = config;
    }

    public override void Load(bool hotReload)
    {
        Globals.Plugin = this;
        Globals.Config = Config;

        if (hotReload)
            Globals.Reset();

        RegisterListener<Listeners.CheckTransmit>(OnCheckTransmit);

        AddCommand("css_antibait_glow",
            "Toggle glow permanent (wallhack) sur un joueur.",
            CommandGlow.OnCommand);

        AddCommand("css_antibait_last",
            "Toggle glow automatique pour le dernier vivant de chaque équipe.",
            CommandGlowLast.OnCommand);

        GlowModule.Setup();

        Logger.LogInformation(
            "[Antibait] v{Version} chargé | Permission: {Perm}",
            ModuleVersion, Config.AdminPermission);
    }

    public override void Unload(bool hotReload)
    {
        GlowModule.Cleanup();
    }

    public override void OnAllPluginsLoaded(bool hotReload)
    {
        TryHookSimpleAdmin();
    }

    private void OnCheckTransmit(CCheckTransmitInfoList infoList)
    {
        foreach ((CCheckTransmitInfo info, CCSPlayerController? viewer) in infoList)
        {
            if (!Util.IsPlayerValid(viewer)) continue;
            GlowModule.OnPlayerTransmit(info, viewer!);
        }
    }

    // ── Intégration CS2-SimpleAdmin (optionnelle, via reflection) ────────────

    private void TryHookSimpleAdmin()
    {
        try
        {
            Type? apiType = null;
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    var t = asm.GetType("CS2_SimpleAdminApi.ICS2_SimpleAdminApi");
                    if (t != null) { apiType = t; break; }
                }
                catch { }
            }

            if (apiType == null)
            {
                Logger.LogInformation("[Antibait] CS2-SimpleAdmin non détecté : intégration menu désactivée.");
                return;
            }

            var capType    = typeof(PluginCapability<>).MakeGenericType(apiType);
            var capInst    = Activator.CreateInstance(capType, "simpleadmin:api");
            var getMethod  = capType.GetMethod("Get");
            var api        = getMethod?.Invoke(capInst, null);

            if (api == null)
            {
                Logger.LogWarning("[Antibait] Capability simpleadmin:api introuvable.");
                return;
            }

            RegisterSimpleAdminMenu(api);
            Logger.LogInformation("[Antibait] Intégration CS2-SimpleAdmin activée.");
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "[Antibait] Échec de l'intégration CS2-SimpleAdmin.");
        }
    }

    private void RegisterSimpleAdminMenu(object api)
    {
        dynamic dapi = api;

        dapi.RegisterMenuCategory("antibait", "Antibait", Config.AdminPermission);

        // Entrée 1 : glow permanent sur un joueur sélectionné
        Func<CCSPlayerController, object> glowFactory = (admin) =>
        {
            var filter = new Func<CCSPlayerController, bool>(p =>
                p != null && p.IsValid && !p.IsBot);

            var onSelect = new Action<CCSPlayerController, CCSPlayerController>((adminSel, target) =>
            {
                CommandGlow.ToggleGlow(adminSel, target);
                try { dapi.LogCommand(adminSel, $"css_antibait_glow {target.PlayerName}"); } catch { }
            });

            return dapi.CreateMenuWithPlayers("Glow permanent", "antibait", admin, filter, onSelect);
        };
        dapi.RegisterMenu("antibait", "antibait_glow", "Glow permanent sur joueur",
                          glowFactory, Config.AdminPermission, "css_antibait_glow");

        // Entrée 2 : surveiller un joueur pour le glow "dernier T vivant"
        Func<CCSPlayerController, object> lastFactory = (admin) =>
        {
            var filter = new Func<CCSPlayerController, bool>(p =>
                p != null && p.IsValid && !p.IsBot);

            var onSelect = new Action<CCSPlayerController, CCSPlayerController>((adminSel, target) =>
            {
                CommandGlowLast.ToggleLastAlive(adminSel, target);
                try { dapi.LogCommand(adminSel, $"css_antibait_last {target.PlayerName}"); } catch { }
            });

            return dapi.CreateMenuWithPlayers("Suivi dernier T vivant", "antibait", admin, filter, onSelect);
        };
        dapi.RegisterMenu("antibait", "antibait_last", "Suivi dernier T vivant (toggle)",
                          lastFactory, Config.AdminPermission, "css_antibait_last");
    }
}
