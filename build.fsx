
#r "paket: groupref Build //"
#load "./.fake/build.fsx/intellisense.fsx"

open Fake.Core
open Fake.DotNet
open Fake.IO
open Fake.IO.FileSystemOperators
open Fake.IO.Globbing.Operators
open Fake.Core.TargetOperators

Target.create "Clean" (fun _ ->
    Shell.cleanDirs ["bin"; "temp"]
)

Target.create "CleanDocs" (fun _ ->
    Shell.cleanDirs ["docs/output"]
)

// --------------------------------------------------------------------------------------
// Build library & test project
Target.create "BuildNetFramework" (fun _ ->
    //Need to restore for .NET Standard
    let workingDir = Path.getFullName "src/IfSharp.Kernel"
    let result =
        DotNet.exec (DotNet.Options.withWorkingDirectory workingDir) "restore" ""
    if result.ExitCode <> 0 then failwithf "'dotnet %s' failed in %s messages: %A" "restore" __SOURCE_DIRECTORY__ result.Messages

    let setParams (defaults:MSBuildParams) =
        { defaults with
            Verbosity = Some(MSBuildVerbosity.Detailed)
            Targets = ["Build"]
            Properties =
                [
                    "Optimize", "True"
                    "DebugSymbols", "True"
                    "Configuration", "Release"
                ]
         }
    MSBuild.build setParams "IfSharp.sln"
)

Target.create "BuildNetCore" (fun _ ->
    let workingDir = Path.getFullName "src/IfSharpNetCore"
    let result =
        DotNet.exec (DotNet.Options.withWorkingDirectory __SOURCE_DIRECTORY__) "build" "IfSharpNetCore.sln"
    if result.ExitCode <> 0 then failwithf "'dotnet %s' failed in %s messages: %A" "build" workingDir result.Messages
)

// --------------------------------------------------------------------------------------
// Run all targets by default. Invoke 'build <Target>' to override

Target.create "All" ignore

"Clean"
  ==> "BuildNetCore"

"Clean"
  ==> "BuildNetFramework"

"BuildNetCore"
  ==> "All"

"BuildNetFramework"
  ==> "All"


Fake.Core.Target.runOrDefault "All"
