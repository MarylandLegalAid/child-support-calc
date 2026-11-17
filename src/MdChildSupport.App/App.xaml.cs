using System.Text.Json;
using Microsoft.UI.Xaml;

namespace MdChildSupport.App;

public partial class App : Application
{
    private MainWindow? _window;

    public App()
    {
        InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        _window = new MainWindow();
        _window.Activate();
        // A path on the command line (as a file association passes it) opens on launch.
        string? path = Environment.GetCommandLineArgs().Skip(1).FirstOrDefault(File.Exists);
        if (path is not null) _window.OpenOnLaunch(path);
    }
}

/// <summary>Deployment settings read from MdChildSupport.settings.json next to the executable.</summary>
public sealed class AppSettings
{
    public string FooterLine { get; set; } = "";

    public static AppSettings Load()
    {
        try
        {
            string path = Path.Combine(AppContext.BaseDirectory, "MdChildSupport.settings.json");
            if (File.Exists(path))
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(path),
                           new JsonSerializerOptions { PropertyNameCaseInsensitive = true, ReadCommentHandling = JsonCommentHandling.Skip })
                       ?? new AppSettings();
        }
        catch (Exception) { /* a broken settings file falls back to the defaults */ }
        return new AppSettings();
    }
}
