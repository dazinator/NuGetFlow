namespace NuGetFlow;

public interface IRuntimePackagesInfo
{
    bool IsPackageProvidedByRuntime(string packageId);
}
