namespace NuGetFlow.Tests;

using Microsoft.Extensions.DependencyInjection;
using Shouldly;

public class NuGetPackagesOptionsInstallerServiceTests
{
    [Fact]
    public async Task Can_Install_NuGetPackage()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddNuGetFlow();

        var options = new NuGetPackageInstallerOptions();
        options.Packages.Add(new PackageOptions() { Package = "dotnet.glob", Version = "3.1.2" });
        options.Sources.Add(new PackageSourceOptions()
        {
            // Source = "C:\\Users\\Daz Work\\.nuget\\packages",
            Source = "https://api.nuget.org/v3/index.json",
        });

        bool signalled = false;
        options.PackageDirectory = Path.Combine(options.PackageDirectory, Guid.NewGuid().ToString("N"));
        options.OnPackagesInstalledAsync(async (ct) =>
        {
            Console.WriteLine("Packages Installation Complete - Initialising WebHost");

            signalled = true;
            // trigger whatever processing you need to do now that packages are installed.
            //  await ProcessMyPackagesAsync(ct);
        });

        var sp = services.BuildServiceProvider();
        var sut = sp.GetRequiredService<INuGetPackagesOptionsInstallerService>();
        await sut.EnsurePackagesAsync(options, default);
        signalled.ShouldBeTrue();
    }
}
