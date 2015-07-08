(*** hide ***)
// This block of code is omitted in the generated HTML documentation. Use 
// it to define helpers that you do not want to show in the documentation.
#I "../../bin"
#r "IFSharp.Kernel"
#r "System.Data.dll"
#r "System.Windows.Forms.DataVisualization.dll"
#r "FSharp.Data.TypeProviders.dll"
#r "FSharp.Charting.dll"
#r "NetMQ.dll"

open FSharp.Charting
open IfSharp.Kernel
open IfSharp.Kernel.Globals

(**
NuGet integration
=================
IfSharp offers built-in NuGet integration by using preprocessor-like directives. Use the `#N`
directive to automatically download packages and reference them. Example: `#N "Newtonsoft.Json"`. 
This will get the latest version of the [Newtonsoft.Json](http://www.nuget.org/packages/Newtonsoft.Json) 
package. 

![NuGet Example](img/NuGet-1.png "NuGet example")

Specifying version and pre-release
----------------------------------
The version can be specified by adding additional information to the string of the preprocessor directive.

The full format is as follows: `#N "<packageName>[/<version>[/pre]]"`.

Version example: `#N "Newtonsoft.Json/5.0.1"`. This will download version 5.0.1 of the Newtonsoft.Json package.

Prerelease example: `#N "FSharp.Compiler.Service/0.0.1-beta/pre"`.

Not supported
-------------
Currently, dependencies are not automatically referenced, however they are downloaded.
*)