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
    public const string CommandAlias = "/pet";

    [PluginService] public static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] public static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] public static IClientState ClientState { get; private set; } = null!;
    [PluginService] public static IPlayerState PlayerState { get; private set; } = null!;
    [PluginService] public static IObjectTable ObjectTable { get; private set; } = null!;
    [PluginService] public static ICondition Condition { get; private set; } = null!;
    [PluginService] public static IFramework Framework { get; private set; } = null!;
    [PluginService] public static IDataManager DataManager { get; private set; } = null!;
    [PluginService] public static IDutyState DutyState { get; private set; } = null!;
    [PluginService] public static IChatGui ChatGui { get; private set; } = null!;
    [PluginService] public static IPluginLog Log { get; private set; } = null!;

    public static Configuration Config { get; private set; } = null!;
    public static PetSimulationService Sim { get; private set; } = null!;
    public static TutorialOverlay Tutorial { get; private set; } = null!;

    private readonly WindowSystem windowSystem = new("PetFrenzy");
    private readonly PetHudWindow hudWindow;
    private readonly DashboardWindow dashboardWindow;

    public Plugin()
    {
        Config = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        Config.Pet = PetSaveStorage.LoadOrMigrate(Config);
        Sim = new PetSimulationService();
        Tutorial = new TutorialOverlay();

        dashboardWindow = new DashboardWindow();
        hudWindow = new PetHudWindow(ToggleDashboard);

        windowSystem.AddWindow(hudWindow);
        windowSystem.AddWindow(dashboardWindow);

        CommandManager.AddHandler(CommandPrimary, new CommandInfo(OnCommand)
        {
            HelpMessage = "Open PetFrenzy Tamagotchi dashboard (or '/pet hud' to toggle floating HUD)."
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
        hudWindow.IsOpen = Config.ShowHud
            && ClientState.IsLoggedIn
            && (!Config.HideHudInCutscene || (!Condition[ConditionFlag.OccupiedInCutSceneEvent] && !Condition[ConditionFlag.WatchingCutscene]));

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
        Sim.Dispose();
        Config.Save();
    }
}
