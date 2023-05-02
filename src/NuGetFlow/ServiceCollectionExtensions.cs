namespace Microsoft.Extensions.DependencyInjection;

using Dazinator.Extensions.Options.ItemChanged;
using NuGetFlow;
using NuGetFlow.BackgroundTask;

public static class ServiceCollectionExtensions
{

    public static IServiceCollection AddNuGetFlow(this IServiceCollection services)
    {     

        services.AddOptionsChangedMonitor<NuGetPackageInstallerOptions>();
        services.AddSingleton<NuGetPackageInstaller>();
        services.AddSingleton<IPackageOptionsHashProvider, PackageOptionsHashProvider>();
        services.AddSingleton<IPackageHashStore, PackageHashStore>();
        services.AddSingleton<INuGetPackagesOptionsInstallerService, NuGetPackagesOptionsInstallerService>();

        return services;

    }

    /// <summary>
    /// Adds an <see cref="IHostedService"/> that will run a task in the background ensure all configured nuget packages are installed on host start.
    /// </summary>
    /// <param name="services"></param>
    /// <remarks>Depends on .AddNuGetPackageUpdater to be called still, to register core services. </remarks>
    public static IServiceCollection AddNuGetFlowEnsurePackagesHostedService(this IServiceCollection services)
    {
        services.AddHostedService<NuGetPackageUpdaterHostedService>();
        services.AddHostedService<QueuedHostedService>();
        services.AddSingleton<IBackgroundTaskQueue>(_ =>
        {
            var queueCapacity = 15;
            return new DefaultBackgroundTaskQueue(queueCapacity);
        });
        return services;
    }



}
