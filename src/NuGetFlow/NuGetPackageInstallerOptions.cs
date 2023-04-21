namespace NuGetFlow;
using Microsoft.Extensions.Hosting;

public class NuGetPackageInstallerOptions
{

    //public const string DefaultPackagesSourceNugetOrg = "https://api.nuget.org/v3/index.json";

    public List<PackageSourceOptions> Sources { get; set; } = new List<PackageSourceOptions>();

    public string PackageDirectory { get; set; } = Path.Combine(Environment.CurrentDirectory, ".extensions");

    public List<PackageOptions> Packages { get; set; }

    public string DotNetRuntimeVersion { get; set; } = DotNetRuntimeVersions.SixZero;

    private Func<CancellationToken, Task> _asyncCallbackOnPackagesInstalled = null;

    public void OnPackagesInstalledAsync(Func<CancellationToken, Task> asyncCallbackOnPackagesInstalled) => _asyncCallbackOnPackagesInstalled = asyncCallbackOnPackagesInstalled;

    internal async Task InvokeCallbackOnPackagesInstalledAsync(CancellationToken cancellationToken)
    {
        if (_asyncCallbackOnPackagesInstalled != null)
        {
            await _asyncCallbackOnPackagesInstalled(cancellationToken);
        }
    }

    public IEnumerable<PackageDirectoryInfo> GetPackageDirectories()
    {
        // todo: only load assemblies from nuget poackage directories of corresponding packahes listed on NuGetPackageInstallerOptions
        var packageSubFolders = Packages.Select(a => new PackageDirectoryInfo(PackageDirectory, a.Package, a.Version, DotNetRuntimeVersion));
        return packageSubFolders;

        //return packageSubFolders.Select(a => $"{packageDirectory}\\{a}\\contentFiles\\any\\{DotNetRuntimeVersion}\\")
        //                   .Where(a => Directory.Exists(a));     

    }

    public class PackageDirectoryInfo
    {
        private readonly string _packagesAbsolutePath;

        public PackageDirectoryInfo(string packagesAbsolutePath, string packageName, string packageVersion, string targetFramework)
        {
            _packagesAbsolutePath = packagesAbsolutePath;

            DirectoryName = $"{packageName}.{packageVersion}";
            DirectoryAbsolutePath = Path.Combine(_packagesAbsolutePath, DirectoryName);

            ContentDirectory = $"/{DirectoryName}/contentFiles/any/{targetFramework}/";
            ContentDirectoryAbsolutePath = Path.Combine(_packagesAbsolutePath, $"{DirectoryName}\\contentFiles\\any\\{targetFramework}\\");

            AssembliesDirectory = $"/{DirectoryName}/lib/{targetFramework}/";
            AssembliesAbsolutePath = Path.Combine(_packagesAbsolutePath, $"{DirectoryName}\\lib\\{targetFramework}\\");
        }
        public string DirectoryName { get; }

        public string DirectoryAbsolutePath { get; }

        public string ContentDirectory { get; }

        public string ContentDirectoryAbsolutePath { get; }
        public string AssembliesDirectory { get; }

        public string AssembliesAbsolutePath { get; }
    }




}
