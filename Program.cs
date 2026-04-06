using Avalonia;
using IoTHubUpdateUtility.UI;

namespace IoTHubUpdateUtility;

class Program
{
    [STAThread]
    public static void Main(string[] args) =>
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<AvaloniaApp>()
            .UsePlatformDetect()
            .LogToTrace();
}
