namespace NuGetFlow;

using Microsoft.Extensions.Logging;


public class NuGetPackagesOptionsInstallerService : INuGetPackagesOptionsInstallerService
{
    private readonly IPackageOptionsHashProvider _hashProvider;
    private readonly IPackageHashStore _hashStore;   
    private readonly ILogger<NuGetPackagesOptionsInstallerService> _logger;
    private readonly NuGetPackageInstaller _nugetPackageInstaller;

    public NuGetPackagesOptionsInstallerService(
    IPackageOptionsHashProvider hashProvider,
    IPackageHashStore hashStore,  
    ILogger<NuGetPackagesOptionsInstallerService> logger,
    NuGetPackageInstaller nugetPackageInstaller)
    {
        _hashProvider = hashProvider;
        _hashStore = hashStore;      
        _logger = logger;
        _nugetPackageInstaller = nugetPackageInstaller;
    }

    public async ValueTask EnsurePackagesAsync(NuGetPackageInstallerOptions options, CancellationToken cancellationToken)
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
        await _nugetPackageInstaller.InstallExtensionsAsync(options, cancellationToken);
        _logger.LogInformation("Nuget package installation has completed.");
        await _hashStore.SaveHashAsync(options.PackageDirectory, currentHash, cancellationToken);
        _logger.LogInformation("Hash persisted.");
        await options.InvokeCallbackOnOptionsChangedAsync(cancellationToken);
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

