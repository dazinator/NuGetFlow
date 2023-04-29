namespace NuGetFlow;

using Dazinator.Extensions.Options.ItemChanged;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NuGetFlow.BackgroundTask;

/// <summary>
/// Hosted service that listens for changes to <see cref="NuGetPackageInstallerOptions"/> and triggers an installation of nuget packages in the background.
/// </summary>
public class NuGetPackageUpdaterHostedService : IHostedService
{
    private readonly IOptionsChangedMonitor<NuGetPackageInstallerOptions> _options;
    private readonly IPackageOptionsHashProvider _hashProvider;
    private readonly IPackageHashStore _hashStore;
    private readonly IHostEnvironment _environment;
    private readonly ILogger<NuGetPackageUpdaterHostedService> _logger;
    private readonly IBackgroundTaskQueue _taskQueue;
    private readonly NuGetPackageInstaller _nugetPackageInstaller;
    private IDisposable _optionsListeningSubscription;

    public NuGetPackageUpdaterHostedService(
        IOptionsChangedMonitor<NuGetPackageInstallerOptions> options,
        IPackageOptionsHashProvider hashProvider,
        IPackageHashStore hashStore,
        IHostEnvironment environment,
        ILogger<NuGetPackageUpdaterHostedService> logger,
        IBackgroundTaskQueue taskQueue,
        NuGetPackageInstaller nugetPackageInstaller)
    {
        _options = options;
        _hashProvider = hashProvider;
        _hashStore = hashStore;
        _environment = environment;
        _logger = logger;
        _taskQueue = taskQueue;
        _nugetPackageInstaller = nugetPackageInstaller;
        _options = options;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("NuGet Package Updater Hosted Service running.");

        // ensure all nuget packages are installed int he background, then listen for further changes.
        await _taskQueue.QueueBackgroundWorkItemAsync((ct) => BuildWorkItemAsync(_options.Instance, ct));


        _optionsListeningSubscription = _options.OnChange((changes) =>
        {
            //  var old = changes.Old;
            var current = changes.Current;

            //#pragma warning disable 4014
            Task.Run(async () => await _taskQueue.QueueBackgroundWorkItemAsync((ct) => BuildWorkItemAsync(current, ct))).ConfigureAwait(false);

            //#pragma warning restore 4014
        });
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("NuGet Package Updater Hosted Service stopping.");

        _optionsListeningSubscription?.Dispose();

        return Task.CompletedTask;
    }

    private async ValueTask BuildWorkItemAsync(NuGetPackageInstallerOptions options, CancellationToken cancellationToken)
    {

        // first compare current hash with persisted hash to work out if we actually have any changes - since last startup.
        var currentHash = _hashProvider.ComputeHash(options);
        var lastHash = await _hashStore.LoadHashAsync(options.PackageDirectory, cancellationToken);
        if (IsHashEqual(currentHash, lastHash))
        {
            _logger.LogInformation("Skipping nuget install. Configuration has not changed since last install.");
            return;
        }

        _logger.LogInformation("Nuget package installation is starting.");
        var baseDir = _environment.ContentRootPath;
        await _nugetPackageInstaller.InstallExtensionsAsync(options, baseDir, cancellationToken);
        _logger.LogInformation("Nuget package installation has completed.");
        await _hashStore.SaveHashAsync(options.PackageDirectory, currentHash, cancellationToken);
        _logger.LogInformation("Hash persisted.");

    }

    private bool IsHashEqual(byte[] currentHash, byte[] lastHash)
    {
        if (currentHash == null)
        {
            throw new ArgumentNullException(nameof(currentHash));
        }

        if (lastHash == null)
        {
            throw new ArgumentNullException(nameof(lastHash));
        }

        return currentHash.SequenceEqual(lastHash);
    }
}
