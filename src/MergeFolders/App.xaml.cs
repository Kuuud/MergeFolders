using MergeFolders.Infrastructure;

namespace MergeFolders;

public partial class App : System.Windows.Application
{
    public static IReadOnlyList<string> InitialPaths { get; internal set; } = Array.Empty<string>();
    public static SingleInstanceCoordinator? Coordinator { get; private set; }

    protected override async void OnStartup(System.Windows.StartupEventArgs e)
    {
        base.OnStartup(e);

        var paths = e.Args
            .Select(x => x.Trim().Trim('"'))
            .Where(Directory.Exists)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        Coordinator = new SingleInstanceCoordinator();
        var result = await Coordinator.StartOrForwardAsync(paths);
        if (!result.IsPrimary)
        {
            Shutdown();
            return;
        }

        InitialPaths = result.Paths;
        var window = new UI.MainWindow(InitialPaths);
        MainWindow = window;
        window.Show();
    }

    protected override void OnExit(System.Windows.ExitEventArgs e)
    {
        Coordinator?.Dispose();
        base.OnExit(e);
    }
}
