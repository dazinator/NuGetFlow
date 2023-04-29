namespace Microsoft.Extensions.DependencyInjection;
using Dazinator.Extensions.Options.ItemChanged;
using NuGetFlow;
using NuGetFlow.BackgroundTask;

public static class ServiceCollectionExtensions
{

    public static IServiceCollection AddNuGetPackageUpdater(this IServiceCollection services)
    {
        services.AddHostedService<NuGetPackageUpdaterHostedService>();
        services.AddHostedService<QueuedHostedService>();
        services.AddSingleton<IBackgroundTaskQueue>(_ =>
        {
            var queueCapacity = 15;
            return new DefaultBackgroundTaskQueue(queueCapacity);
        });

        services.AddOptionsChangedMonitor<NuGetPackageInstallerOptions>();       
        services.AddSingleton<NuGetPackageInstaller>();
        services.AddSingleton<IPackageOptionsHashProvider, PackageOptionsHashProvider>();
        services.AddSingleton<IPackageHashStore, PackageHashStore>();

        

        return services;

    }



}
