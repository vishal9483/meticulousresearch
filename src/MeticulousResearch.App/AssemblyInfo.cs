using System.Reflection;
using System.Windows;
using MeticulousResearch.Core.AppInfo;

// app-branding-icon (M6, SPEC §3.7): stamp the package's Product metadata from the single-source
// product-name constant so the window title, About screen, and package metadata never diverge. The
// auto-generated Product attribute is disabled in the csproj so this is the only one.
[assembly: AssemblyProduct(AssemblyAppInfo.ProductNameValue)]

[assembly:ThemeInfo(
    ResourceDictionaryLocation.None,            //where theme specific resource dictionaries are located
                                                //(used if a resource is not found in the page,
                                                // or application resource dictionaries)
    ResourceDictionaryLocation.SourceAssembly   //where the generic resource dictionary is located
                                                //(used if a resource is not found in the page,
                                                // app, or any theme specific resource dictionaries)
)]
