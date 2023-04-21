namespace Microsoft.Extensions.DependencyInjection;
using Dazinator.Extensions.Options.ItemChanged;
using Microsoft.Extensions.DependencyInjection;
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
        services.AddSingleton<NuGetPackageUpdaterHostedService>();
        services.AddSingleton<NuGetPackageInstaller>();

        return services;

    }



}
