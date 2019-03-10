open Fake.Runtime.Trace
open Fake.MSBuildHelper
// --------------------------------------------------------------------------------------
// FAKE build script 
// --------------------------------------------------------------------------------------

#r @"packages/FAKE/tools/FakeLib.dll"
open Fake
open Fake.SystemHelper
open Fake.Git
open Fake.DotNet
open Fake.IO
open Fake.AssemblyInfoFile
open Fake.ReleaseNotesHelper
open Fake.Core.TargetOperators
open Fake.IO.Globbing.Operators
open System
open System.IO

// --------------------------------------------------------------------------------------
// START TODO: Provide project-specific details below
// --------------------------------------------------------------------------------------

// Information about the project are used
//  - for version and project name in generated AssemblyInfo file
//  - to run tests and to publish documentation on GitHub gh-pages
//  - for documentation, you also need to edit info in "docs/tools/generate.fsx"

// The name of the project 
// (used by attributes in AssemblyInfo, name of a NuGet package and directory in 'src')
let project = "IfSharp"

// Short summary of the project
// (used as description in AssemblyInfo and as a short summary for NuGet package)
let summary = "F# kernel for Jupyter Notebooks."

// Git configuration (used for publishing documentation in gh-pages branch)
// The profile where the project is posted 
let gitHome = "https://github.com/fsprojects/"
// The name of the project on GitHub
let gitName = "IfSharp"

// --------------------------------------------------------------------------------------
// END TODO: The rest of the file includes standard build steps 
// --------------------------------------------------------------------------------------

// Read additional information from the release notes document
Environment.CurrentDirectory <- __SOURCE_DIRECTORY__
//let release = parseReleaseNotes (IO.File.ReadAllLines "RELEASE_NOTES.md")

// Generate assembly info files with the right version & up-to-date information
Fake.Core.Target.create "AssemblyInfo" (fun _ ->
    let fileName = "src/" + project + "/AssemblyInfo.fs"
    AssemblyInfoFile.createFSharp
        fileName
        [
            //TODO: switch to FAKE5

            //Attribute.Title project
            //Attribute.Product project
            //Attribute.Description summary
            //Attribute.Version release.AssemblyVersion
            //Attribute.FileVersion release.AssemblyVersion
        ]  
)

// --------------------------------------------------------------------------------------
// Clean build results & restore NuGet packages

Fake.Core.Target.create "Clean" (fun _ ->
    Fake.IO.Shell.cleanDirs ["bin"; "temp"]
)

Fake.Core.Target.create "CleanDocs" (fun _ ->
    Fake.IO.Shell.cleanDirs ["docs/output"]
)

// --------------------------------------------------------------------------------------
// Build library & test project
Fake.Core.Target.create "BuildNetFramework" (fun _ ->
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
    Fake.DotNet.MSBuild.build setParams "IfSharp.sln"
)

Fake.Core.Target.create "BuildNetCore" (fun _ ->
    let workingDir = Path.getFullName "src/IfSharpNetCore"
    let result =
        DotNet.exec (DotNet.Options.withWorkingDirectory __SOURCE_DIRECTORY__) "build" "IfSharpNetCore.sln"
    if result.ExitCode <> 0 then failwithf "'dotnet %s' failed in %s messages: %A" "build" workingDir result.Messages
)

// --------------------------------------------------------------------------------------
// Run all targets by default. Invoke 'build <Target>' to override

Fake.Core.Target.create "All" ignore

"Clean"
  ==> "BuildNetCore"

"Clean"
  ==> "BuildNetFramework"

"BuildNetCore"
  ==> "All"

"BuildNetFramework"
  ==> "All"


Fake.Core.Target.runOrDefault "All"
