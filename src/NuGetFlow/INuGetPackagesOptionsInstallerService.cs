namespace NuGetFlow;

using System.Threading;
using System.Threading.Tasks;

public interface INuGetPackagesOptionsInstallerService
{
    ValueTask EnsurePackagesAsync(NuGetPackageInstallerOptions options, CancellationToken cancellationToken);
}