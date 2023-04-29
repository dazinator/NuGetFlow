namespace NuGetFlow;

using System.Security.Cryptography;

public class PackageOptionsHashProvider : IPackageOptionsHashProvider
{
    public byte[] ComputeHash(NuGetPackageInstallerOptions options)
    {
        var bytes = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes<NuGetPackageInstallerOptions>(options);
        var hash = MD5.HashData(bytes);
        return hash;
    }
}
