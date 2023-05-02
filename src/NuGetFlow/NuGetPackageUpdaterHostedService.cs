namespace NuGetFlow;

using Dazinator.Extensions.Options.ItemChanged;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NuGetFlow.BackgroundTask;

/// <summary>
/// Hosted service that listens for changes to <see cref="NuGetPackageInstallerOptions"/> and triggers an installation of nuget packages in the background.
/// </summary>
public partial class NuGetPackageUpdaterHostedService : IHostedService
{
    private readonly INuGetPackagesOptionsInstallerService _installerService;
    private readonly IOptionsChangedMonitor<NuGetPackageInstallerOptions> _options;   
    private readonly IHostEnvironment _environment;
    private readonly ILogger<NuGetPackageUpdaterHostedService> _logger;
    private readonly IBackgroundTaskQueue _taskQueue;  
    private IDisposable _optionsListeningSubscription;

    public NuGetPackageUpdaterHostedService(
        INuGetPackagesOptionsInstallerService installerService,
        IOptionsChangedMonitor<NuGetPackageInstallerOptions> options,    
        IHostEnvironment environment,
        ILogger<NuGetPackageUpdaterHostedService> logger,
        IBackgroundTaskQueue taskQueue)
    {
        _installerService = installerService;
        _options = options;    
        _environment = environment;
        _logger = logger;
        _taskQueue = taskQueue;     
        _options = options;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("NuGet Package Updater Hosted Service running.");

        // ensure all nuget packages are installed int he background, then listen for further changes.
        await _taskQueue.QueueBackgroundWorkItemAsync((ct) => _installerService.EnsurePackagesAsync(_options.Instance, ct));

        _optionsListeningSubscription = _options.OnChange((changes) =>
        {
            //  var old = changes.Old;
            var current = changes.Current;

            //#pragma warning disable 4014
            Task.Run(async () => await _taskQueue.QueueBackgroundWorkItemAsync((ct) => {

                return _installerService.EnsurePackagesAsync(current, ct);

            })).ConfigureAwait(false);

            //#pragma warning restore 4014
        });
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("NuGet Package Updater Hosted Service stopping.");

        _optionsListeningSubscription?.Dispose();

        return Task.CompletedTask;
    }

}
