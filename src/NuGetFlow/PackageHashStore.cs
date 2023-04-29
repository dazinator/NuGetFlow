namespace NuGetFlow;

public sealed class PackageHashStore : IPackageHashStore
{
    public async Task<byte[]> LoadHashAsync(string packageDirectory, CancellationToken ct)
    {
        var hashFilePath = Path.Combine(packageDirectory, "hash.md5");
        if (File.Exists(hashFilePath))
        {
            return await File.ReadAllBytesAsync(hashFilePath, ct);
        }

        return Array.Empty<byte>();

    }

    public async Task SaveHashAsync(string packageDirectory, byte[] hash, CancellationToken ct)
    {
        var hashFilePath = Path.Combine(packageDirectory, "hash.md5");
        await File.WriteAllBytesAsync(hashFilePath, hash, ct);
    }

}
