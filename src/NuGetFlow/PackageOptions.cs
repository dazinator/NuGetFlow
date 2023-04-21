namespace NuGetFlow;

/// <summary>
/// Represents the configuration for a single package to install.
/// </summary>
public class PackageOptions
{
    public string Package { get; set; }
    public string Version { get; set; }
    public bool PreRelease { get; set; }
}
