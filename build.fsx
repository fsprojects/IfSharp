// --------------------------------------------------------------------------------------
// FAKE build script 
// --------------------------------------------------------------------------------------

#r @"packages/FAKE/tools/FakeLib.dll"
open Fake 
open Fake.Git
open Fake.AssemblyInfoFile
open Fake.ReleaseNotesHelper
open System
open Fake.DotNet
open Fake.IO


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
Target "AssemblyInfo" (fun _ ->
  let fileName = "src/" + project + "/AssemblyInfo.fs"
  CreateFSharpAssemblyInfo fileName
      [ Attribute.Title project
        Attribute.Product project
        Attribute.Description summary
        //Attribute.Version release.AssemblyVersion
        //Attribute.FileVersion release.AssemblyVersion
      ] 
)

// --------------------------------------------------------------------------------------
// Clean build results & restore NuGet packages

Target "Clean" (fun _ ->
    CleanDirs ["bin"; "temp"]
)

Target "CleanDocs" (fun _ ->
    CleanDirs ["docs/output"]
)

// --------------------------------------------------------------------------------------
// Build library & test project
Target "Build" (fun _ ->
    DotNet.publish (fun o -> { o with Configuration = DotNet.BuildConfiguration.Release
                                      Runtime = Some "osx-x64" }) "src/IfSharp/IfSharp.fsproj"
)

Target "CopyBinaries" (fun _ -> 
  Fake.IO.Shell.copyDir (__SOURCE_DIRECTORY__ </> "bin") (__SOURCE_DIRECTORY__ </> "src" </> "IfSharp" </> "bin" </> "Release" </> "netcoreapp2.1" </> "osx-x64" </> "publish") (fun _ -> true)
)

// --------------------------------------------------------------------------------------
// Run the unit tests using test runner & kill test runner when complete
Target "xUnit" (fun _ ->
    DotNet.test (fun o -> { o with Common = { o.Common with WorkingDirectory = "tests/IfSharp.Kernel.Tests"  }
                                   Configuration = DotNet.BuildConfiguration.Release }) "IfSharp.Kernel.Tests.fsproj"
)


Target "Release" DoNothing

// --------------------------------------------------------------------------------------
// Run all targets by default. Invoke 'build <Target>' to override

Target "All" DoNothing

"Clean"
  ==> "AssemblyInfo"
  ==> "Build"
  ==> "CopyBinaries"
  ==> "All"

"All" 
  ==> "xUnit"
  ==> "Release"


RunTargetOrDefault "All"
