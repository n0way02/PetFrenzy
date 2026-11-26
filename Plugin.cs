using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using PetFrenzy.Services;
using PetFrenzy.UI;

namespace PetFrenzy;

public sealed class Plugin : IDalamudPlugin
{
    public const string CommandPrimary = "/petfrenzy";
    public const string CommandAlias = "/pf";

    [PluginService] public static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] public static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] public static IClientState ClientState { get; private set; } = null!;
    [PluginService] public static IPlayerState PlayerState { get; private set; } = null!;
    [PluginService] public static IObjectTable ObjectTable { get; private set; } = null!;
    [PluginService] public static ICondition Condition { get; private set; } = null!;
    [PluginService] public static IFramework Framework { get; private set; } = null!;
    [PluginService] public static IDataManager DataManager { get; private set; } = null!;
    [PluginService] public static IDutyState DutyState { get; private set; } = null!;
    [PluginService] public static IGameGui GameGui { get; private set; } = null!;
    [PluginService] public static IPartyList PartyList { get; private set; } = null!;
    [PluginService] public static IChatGui ChatGui { get; private set; } = null!;
    [PluginService] public static IPluginLog Log { get; private set; } = null!;

    public static Configuration Config { get; private set; } = null!;
    public static PetSimulationService Sim { get; private set; } = null!;
    public static PetMultiplayerService Multiplayer { get; private set; } = null!;
    public static TutorialOverlay Tutorial { get; private set; } = null!;

    private readonly WindowSystem windowSystem = new("PetFrenzy");
    private readonly PetHudWindow hudWindow;
    private readonly DashboardWindow dashboardWindow;
    private readonly MultiplayerWorldOverlay worldOverlay;
    private readonly PartyPetHudWindow[] partyHudWindows;

    public Plugin()
    {
        Config = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        Config.Pet = PetSaveStorage.LoadOrMigrate(Config);
        Sim = new PetSimulationService();
        Multiplayer = new PetMultiplayerService();
        Tutorial = new TutorialOverlay();
        worldOverlay = new MultiplayerWorldOverlay();

        dashboardWindow = new DashboardWindow();
        hudWindow = new PetHudWindow(ToggleDashboard);

        windowSystem.AddWindow(hudWindow);
        windowSystem.AddWindow(dashboardWindow);

        partyHudWindows = new PartyPetHudWindow[PartyPetHudWindow.MaxPartySlots];
        for (var i = 0; i < partyHudWindows.Length; i++)
        {
            partyHudWindows[i] = new PartyPetHudWindow(i);
            windowSystem.AddWindow(partyHudWindows[i]);
        }

        CommandManager.AddHandler(CommandPrimary, new CommandInfo(OnCommand)
        {
            HelpMessage = "Open PetFrenzy Tamagotchi dashboard (or '/pf hud' to toggle floating HUD)."
        });

        CommandManager.AddHandler(CommandAlias, new CommandInfo(OnCommand)
        {
            HelpMessage = "Open PetFrenzy Tamagotchi dashboard."
        });

        PluginInterface.UiBuilder.Draw += DrawUI;
        PluginInterface.UiBuilder.OpenConfigUi += ToggleDashboard;
        PluginInterface.UiBuilder.OpenMainUi += ToggleDashboard;
    }

    private void OnCommand(string command, string args)
    {
        if (args.Trim().Equals("hud", System.StringComparison.OrdinalIgnoreCase))
        {
            Config.ShowHud = !Config.ShowHud;
            Config.Save();
            return;
        }

        ToggleDashboard();
    }

    private void ToggleDashboard()
    {
        dashboardWindow.IsOpen = !dashboardWindow.IsOpen;
    }

    private void DrawUI()
    {
        var hideForCutscene = Config.HideHudInCutscene
            && (Condition[ConditionFlag.OccupiedInCutSceneEvent] || Condition[ConditionFlag.WatchingCutscene]);

        hudWindow.IsOpen = Config.ShowHud && ClientState.IsLoggedIn && !hideForCutscene;

        // Bind party pet HUD slots
        var allowPartyHud = Config.MultiplayerEnabled
            && Config.MultiplayerShowPartyPets
            && ClientState.IsLoggedIn
            && !hideForCutscene;

        var partyPeers = allowPartyHud ? Multiplayer.GetPartyPeersSnapshot() : System.Array.Empty<RemotePetPeer>();
        for (var i = 0; i < partyHudWindows.Length; i++)
        {
            var peer = i < partyPeers.Count ? partyPeers[i] : null;
            partyHudWindows[i].UpdateBinding(peer, allowPartyHud);
        }

        // Draw other zone players' pets above their in-game nameplates
        worldOverlay.Draw();

        Tutorial.BeginFrame();
        windowSystem.Draw();
        Tutorial.Draw(dashboardWindow.IsOpen);
    }

    public void Dispose()
    {
        PluginInterface.UiBuilder.Draw -= DrawUI;
        PluginInterface.UiBuilder.OpenConfigUi -= ToggleDashboard;
        PluginInterface.UiBuilder.OpenMainUi -= ToggleDashboard;

        CommandManager.RemoveHandler(CommandPrimary);
        CommandManager.RemoveHandler(CommandAlias);

        windowSystem.RemoveAllWindows();
        Multiplayer.Dispose();
        Sim.Dispose();
        Config.Save();
    }
}
