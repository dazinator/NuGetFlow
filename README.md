# NuGetFlow

> a smooth and continuous process of downloading and integrating NuGet packages into an application


# What does it solve?

You have a `dotnet` application. You want this application to be able to download and install NuGet packages at runtime without having to restart the application.
You also want a mechanism by which you can trigger the installation fo new packages, and be alerted when new packages have been downloaded, so you can process filed from within them - for example to load plugins or extract contents etc.

## Show me

`NuGetFlow` is a small opionated library that allows you to do this.

1. In your `dotnet` application startup, register the services

```csharp
       services.AddNuGetPackageUpdater();
```

2. Configure the `NuGetPackageInstallerOptions` using the options pattern. NuGetFlow montitors changes to these options, so if you bind this to configuration for example, and then the configuration is changed, `NuGetFlow` will ensure the latest packages specified are installed. Your application also registers a callback here: `OnPackagesInstalledAsync` so it can be notified whenever new packages have been installed.

```csharp

              

               // configure the NuGetPackageInstallerOptions - this is
               var config = ctx.Configuration;
               var section = config.GetSection("NuGetFlow");
               services.Configure<NuGetPackageInstallerOptions>(section); // bind to config, 
               services.Configure<NuGetPackageInstallerOptions>((a) =>
               {
                   // add another package that should always be downloaed perhaps.
                   a.Packages.Add(new PackageOptions(){ Package="Foo", Version="1.0.0" });

                   a.OnPackagesInstalledAsync(async (ct) =>
                   {
                       Console.WriteLine("Packages Installation Complete - Initialising WebHost");

                       // trigger whatever processing you need to do now that packages are installed.
                       await ProcessMyPackagesAsync(ct);
                   });

               });
               
```


Here is an example config section:

```json

{
  "NuGetFlow": {
    "DotNetRuntimeVersion": "net6.0",
    "Sources": [
      {
        "Source": "..\\.packages"
      },
      {
        "Source": "https://api.nuget.org/v3/index.json"
      }
    ],
    "Packages": [
      {
        "Package": "DotNet.Glob",
        "Version": "3.2.0-alpha.36",
        "PreRelease": true
      },
      {
        "Package": "Plugin",
        "Version": "1.0.6", 
        "PreRelease": true  
      }
    ]
  }
}



```

Notice that we can configure nuget package sources (including local directories), as well as the list of packages that should be installed.


Once installation has completed - you can get information about the package locations and various folders that you may need to process:

```csharp

 var packagesOptionsMonitor = host.Services.GetRequiredService<IOptionsMonitor<NuGetPackageInstallerOptions>>();                           
 var packageDirectories = packagesOptionsMonitor.CurrentValue.GetPackageDirectories();
 var contentDirectories = packageDirectories.Select(a => a.ContentDirectoryAbsolutePath) // you can select other folder paths here that you mgiht be interested in, like the assemblies folder for example.

```


To test this process, whilst your application is running, update the config file and add a nuget package. You should see `NuGetFlow` kick into action, download the package, and then notify your application via the callback. 