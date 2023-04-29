namespace NuGetFlow;

using System.Threading;
using System.Threading.Tasks;

public interface IPackageHashStore
{
    Task<byte[]> LoadHashAsync(string packageDirectory, CancellationToken ct);
    Task SaveHashAsync(string packageDirectory, byte[] hash, CancellationToken ct);
}