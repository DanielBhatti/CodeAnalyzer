using Avalonia;
using Avalonia.Headless;
using Avalonia.Headless.NUnit;
using CodeAnalyzer.UI;

[assembly: AvaloniaTestApplication(typeof(CodeAnalyzer.Test.TestAppBuilder))]

namespace CodeAnalyzer.Test;

public static class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>().UseSkia()
        .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
}