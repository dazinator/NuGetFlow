namespace NuGetFlow;
/// <summary>
/// Responsible for providing information about packages provided by the dotnet 6 runtime
/// Note: the package names provided by the dotnet 6 runtime seem to be the same as the dotnet 3.1 runtime, hence we derive, but in future if thta changes we'd amend this class.
/// The 6.0 packages can be found here: 
/// at https://github.com/dotnet/sdk/blob/v6.0.403/src/Tasks/Common/targets/Microsoft.NET.DefaultPackageConflictOverrides.targets  
/// </summary>
public class DotNet60SdkRuntimePackagesInfo : DotNet31SdkRuntimePackagesInfo
{


}
