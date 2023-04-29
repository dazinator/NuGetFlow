namespace NuGetFlow;

public interface IPackageOptionsHashProvider
{
    byte[] ComputeHash(NuGetPackageInstallerOptions options);
}