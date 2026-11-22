using Microsoft.Build.Locator;

namespace CodeAnalyzer;

public static class BuildEnvironment
{
    private static readonly object RegistrationLock = new();

    // Call before entering methods that use MSBuild types, so assembly resolution is ready for the JIT.
    public static void Initialize()
    {
        lock(RegistrationLock)
        {
            if(!MSBuildLocator.IsRegistered)
                MSBuildLocator.RegisterDefaults();
        }
    }
}
