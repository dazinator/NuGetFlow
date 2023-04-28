namespace NuGetFlow;
using Microsoft.Extensions.DependencyModel;
using Microsoft.Extensions.Logging;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Frameworks;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Packaging.Signing;
using NuGet.Protocol.Core.Types;
using NuGet.Resolver;
using NuGet.Versioning;
using ILogger = NuGet.Common.ILogger;

public class NuGetPackageInstaller
{
    // private readonly IOptionsChangedMonitor<NuGetPackageInstallerOptions> _options;
    private const string _framework = "net6.0";
    private readonly ILogger<NuGetPackageInstaller> _logger;

    public NuGetPackageInstaller(ILogger<NuGetPackageInstaller> logger) => _logger = logger;

    /// <summary>
    /// Download and install nuget packages specified as per the options.
    /// </summary>
    /// <param name="options"></param>
    /// <param name="packageSourcesBasePath">If package sources are specified as a relative path, then the path on disk will be relative to this base path.</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    public async Task InstallExtensionsAsync(NuGetPackageInstallerOptions options, string packageSourcesBasePath, CancellationToken cancellationToken)
    {

        // todo: https://martinbjorkstrom.com/posts/2018-09-19-revisiting-nuget-client-libraries

        if (options == null)
        {
            _logger.LogWarning("Null options provided.");
            return;
        }

        if (!options.Sources?.Any() ?? false)
        {
            _logger.LogWarning("No sources configured, packages will not be installed.");
            // no sources configured.
            return;
        }

        if (!options.Packages?.Any() ?? false)
        {
            _logger.LogInformation("No packages configured for installation.");
            return;
        }

        var sources = options.Sources.Select(a =>
        {

            var sourceUri = a.Source;
            if (sourceUri.StartsWith('.'))
            {
                // convert relative path to full path
                sourceUri = Path.Combine(packageSourcesBasePath, sourceUri);
            }
            var source = new PackageSource(sourceUri);
            if (!string.IsNullOrWhiteSpace(a.Username) || !string.IsNullOrWhiteSpace(a.Password))
            {
                source.Credentials = new PackageSourceCredential(sourceUri, a.Username, a.Password, true, null);
            }

            return source;

        });

        // Define a source provider, with nuget, plus my own feed.
        var sourceProvider = new PackageSourceProvider(NullSettings.Instance, sources);

        // Establish the source repository provider; the available providers come from our custom settings.
        var sourceRepositoryProvider = new SourceRepositoryProvider(sourceProvider, Repository.Provider.GetCoreV3());

        // Get the list of repositories.
        var repositories = sourceRepositoryProvider.GetRepositories();

        // Disposable source cache.
        using var sourceCacheContext = new SourceCacheContext();

        // You should use an actual logger here, this is a NuGet ILogger instance.
        var logger = new NullLogger();

        // The framework we're using.
        var targetFramework = NuGetFramework.ParseFolder(_framework);
        var allPackages = new HashSet<SourcePackageDependencyInfo>();

        var dependencyContext = DependencyContext.Default;
        var runtimePackageProvider = GetRuntimePackagesInfo(options.DotNetRuntimeVersion);


        foreach (var ext in options.Packages)
        {
            var packageIdentity = await GetPackageIdentity(ext, sourceCacheContext, logger, repositories, cancellationToken);

            if (packageIdentity is null)
            {
                throw new InvalidOperationException($"Cannot find package {packageIdentity}.");
            }

            await GetPackageDependencies(packageIdentity, sourceCacheContext, targetFramework, logger, repositories, dependencyContext, allPackages, runtimePackageProvider, cancellationToken);
        }

        var packagesToInstall = GetPackagesToInstall(sourceRepositoryProvider, logger, options.Packages, allPackages);

        // Where do we want to install our packages?
        var packageDirectory = options.PackageDirectory;
        var nugetSettings = Settings.LoadDefaultSettings(packageDirectory);

        var newPackagesExtracted = await InstallPackages(sourceCacheContext, logger, packagesToInstall, packageDirectory, nugetSettings, skipPackage: (p) =>
        {
            // we assume if a directory exists then the package is good, we dont redownload.
            // if there was a partial extraction this could cause problems, would have to manually delete the directory.
            // however at the moment nuget isn't giving us a way to extract a package and get back only files that didn't already exist, so we have no easy way to
            /// determing if any true modifications were made - nuget package extarctor will blindly overwrite the files every time, then return the list of all the files.
            /// Where as we only want to raise a callback when there were new package contents extarcted. This is a cheap and dirty way to achieve this by assumign if the folder
            /// exists then its immutable and we'll skip overwriting it again.
            var packageName = $"{p.Id}.{p.Version.ToNormalizedString()}";
            if (Directory.Exists(Path.Combine(packageDirectory, packageName)))
            {
                /// true means skip extracting this package.
                return true;
            }

        }, cancellationToken);
        if (newPackagesExtracted)
        {
            await options.InvokeCallbackOnPackagesInstalledAsync(cancellationToken);
        }

    }

    private IRuntimePackagesInfo GetRuntimePackagesInfo(string dotnetRuntimeVersion)
    {
        switch (dotnetRuntimeVersion)
        {
            case DotNetRuntimeVersions.ThreeOne:
                return new DotNet31SdkRuntimePackagesInfo();
            case DotNetRuntimeVersions.SixZero:
                return new DotNet60SdkRuntimePackagesInfo();
            default:
                throw new NotSupportedException("Unsupported dotnetRuntimeVersion: " + (dotnetRuntimeVersion ?? string.Empty));
        }
    }

    private async Task<bool> InstallPackages(SourceCacheContext sourceCacheContext, ILogger logger,
                                       IEnumerable<SourcePackageDependencyInfo> packagesToInstall, string rootPackagesDirectory,
                                       ISettings nugetSettings, Func<SourcePackageDependencyInfo, bool> skipPackage, CancellationToken cancellationToken)
    {
        var packagePathResolver = new PackagePathResolver(rootPackagesDirectory, true);
        var packageExtractionContext = new PackageExtractionContext(
            PackageSaveMode.Defaultv3,
            XmlDocFileSaveMode.Skip,
            ClientPolicyContext.GetClientPolicy(nugetSettings, logger),
            logger);

        var globalPackagesFolder = SettingsUtility.GetGlobalPackagesFolder(nugetSettings);

        bool any = false;

        foreach (var package in packagesToInstall)
        {
          
            if (skipPackage?.Invoke(package) ?? false)
            {
                _logger.LogInformation("Skipping install of package {packageName}.", package);
                continue;
            }

            var downloadResource = await package.Source.GetResourceAsync<DownloadResource>(cancellationToken);

            // check if folder extracted already and if so, don't re-download.


            // Download the package (might come from the shared package cache).
            _logger.LogInformation("Installing package {packageName}", package);
            var downloadResult = await downloadResource.GetDownloadResourceResultAsync(
                package,
                new PackageDownloadContext(sourceCacheContext),
                globalPackagesFolder,
                logger,
                cancellationToken);

            _logger.LogDebug("{packageName} - {downloadStatus}", package, downloadResult.Status);

            // Extract the package into the target directory.
            var results = await PackageExtractor.ExtractPackageAsync(
                  downloadResult.PackageSource,
                  downloadResult.PackageStream,
                  packagePathResolver,
                  packageExtractionContext,
                  cancellationToken);

            foreach (var item in results)
            {
                any = true;
                _logger.LogDebug("extracted - {item}", item);
            }

        }

        return any;
    }

    private IEnumerable<SourcePackageDependencyInfo> GetPackagesToInstall(SourceRepositoryProvider sourceRepositoryProvider,
                                                                          ILogger logger, IEnumerable<PackageOptions> extensions,
                                                                          HashSet<SourcePackageDependencyInfo> allPackages)
    {
        // Create a package resolver context (this is used to help figure out which actual package versions to install).
        var resolverContext = new PackageResolverContext(
               DependencyBehavior.Lowest,
               extensions.Select(x => x.Package),
               Enumerable.Empty<string>(),
               Enumerable.Empty<PackageReference>(),
               Enumerable.Empty<PackageIdentity>(),
               allPackages,
               sourceRepositoryProvider.GetRepositories().Select(s => s.PackageSource),
               logger);

        var resolver = new PackageResolver();

        // Work out the actual set of packages to install.
        var packagesToInstall = resolver.Resolve(resolverContext, CancellationToken.None)
                                        .Select(p => allPackages.Single(x => PackageIdentityComparer.Default.Equals(x, p)));
        return packagesToInstall;
    }

    private async Task<PackageIdentity> GetPackageIdentity(
      PackageOptions extConfig, SourceCacheContext cache, ILogger nugetLogger,
      IEnumerable<SourceRepository> repositories, CancellationToken cancelToken)
    {
        // Go through each repository.
        // If a repository contains only pre-release packages (e.g. AutoStep CI), and 
        // the configuration doesn't permit pre-release versions,
        // the search will look at other ones (e.g. NuGet).
        foreach (var sourceRepository in repositories)
        {
            // Get a 'resource' from the repository.
            var findPackageResource = await sourceRepository.GetResourceAsync<FindPackageByIdResource>();

            // Get the list of all available versions of the package in the repository.
            var allVersions = await findPackageResource.GetAllVersionsAsync(extConfig.Package, cache, nugetLogger, cancelToken);

            NuGetVersion selected;

            // Have we specified a version range?
            if (extConfig.Version != null)
            {
                if (!VersionRange.TryParse(extConfig.Version, out var range))
                {
                    throw new InvalidOperationException("Invalid version range provided.");
                }

                // Find the best package version match for the range.
                // Consider pre-release versions, but only if the extension is configured to use them.
                var bestVersion = range.FindBestMatch(allVersions.Where(v => extConfig.PreRelease || !v.IsPrerelease));

                selected = bestVersion;
            }
            else
            {
                // No version; choose the latest, allow pre-release if configured.
                selected = allVersions.LastOrDefault(v => v.IsPrerelease == extConfig.PreRelease);
            }

            if (selected is object)
            {
                return new PackageIdentity(extConfig.Package, selected);
            }
        }

        return null;
    }

    /// <summary>
    /// Searches the package dependency graph for the chain of all packages to install.
    /// </summary>
    private async Task GetPackageDependencies(PackageIdentity package, SourceCacheContext cacheContext, NuGetFramework framework,
                                              ILogger logger, IEnumerable<SourceRepository> repositories, DependencyContext hostDependencies,
                                              ISet<SourcePackageDependencyInfo> availablePackages, IRuntimePackagesInfo runtimePackageProvider, CancellationToken cancelToken)
    {
        // Don't recurse over a package we've already seen.
        if (availablePackages.Contains(package))
        {
            return;
        }

        foreach (var sourceRepository in repositories)
        {
            // Get the dependency info for the package.
            var dependencyInfoResource = await sourceRepository.GetResourceAsync<DependencyInfoResource>();
            var dependencyInfo = await dependencyInfoResource.ResolvePackage(
                package,
                framework,
                cacheContext,
                logger,
                cancelToken);

            // No info for the package in this repository.
            if (dependencyInfo == null)
            {
                continue;
            }


            // Filter the dependency info.
            // Don't bring in any dependencies that are provided by the host.
            var actualSourceDep = new SourcePackageDependencyInfo(
                dependencyInfo.Id,
                dependencyInfo.Version,
                dependencyInfo.Dependencies.Where(dep => !DependencySuppliedByHost(hostDependencies, dep, runtimePackageProvider)),
                dependencyInfo.Listed,
                dependencyInfo.Source);

            availablePackages.Add(actualSourceDep);

            // Recurse through each package.
            foreach (var dependency in actualSourceDep.Dependencies)
            {
                await GetPackageDependencies(
                    new PackageIdentity(dependency.Id, dependency.VersionRange.MinVersion),
                    cacheContext,
                    framework,
                    logger,
                    repositories,
                    hostDependencies,
                    availablePackages,
                    runtimePackageProvider,
                    cancelToken);
            }

            break;
        }
    }

    private bool DependencySuppliedByHost(DependencyContext hostDependencies, PackageDependency dep, IRuntimePackagesInfo runtimePackageProvider)
    {
        //hostDependencies.Target.Runtime
        if (runtimePackageProvider.IsPackageProvidedByRuntime(dep.Id))
        {
            return true;
        }

        // See if a runtime library with the same ID as the package is available in the host's runtime libraries.
        var runtimeLib = hostDependencies.RuntimeLibraries.FirstOrDefault(r => r.Name == dep.Id);

        if (runtimeLib is object)
        {
            // What version of the library is the host using?
            var parsedLibVersion = NuGetVersion.Parse(runtimeLib.Version);

            if (parsedLibVersion.IsPrerelease)
            {
                // Always use pre-release versions from the host, otherwise it becomes
                // a nightmare to develop across multiple active versions.
                return true;
            }
            else
            {
                // Does the host version satisfy the version range of the requested package?
                // If so, we can provide it; otherwise, we cannot.
                return dep.VersionRange.Satisfies(parsedLibVersion);
            }
        }

        return false;
    }
}
