#r "IfSharp.Kernel.dll"
#r "Chessie.dll"
#r "Paket.Core.dll"

open System
open Paket
open Paket.LoadingScripts.ScriptGeneration

let deps =
    let dir =
        Reflection.Assembly.GetEntryAssembly().Location
        |> IO.Path.GetDirectoryName

    let d =
        try
            Dependencies.Locate(dir)
        with _ ->
            Dependencies.Init(dir)
            Dependencies.Locate(dir)

    d.Restore(false)
    d

let private add package version =
    deps.Add(None, package, version, force = false,
        withBindingRedirects = false, cleanBindingRedirects = false,
        createNewBindingFiles = false, interactive = false,
        installAfter = false, semVerUpdateMode = SemVerUpdateMode.NoRestriction,
        touchAffectedRefs = false)

let private generateScripts() =
    //The framework version needs to be kept in sync with the kernel
    generateScriptsForRootFolder FSharp (Paket.FrameworkIdentifier.DotNetFramework Paket.FrameworkVersion.V4_5_1) (System.IO.DirectoryInfo __SOURCE_DIRECTORY__)

let Package list =
    for package in list do
        add package ""

    deps.Install(false)

    generateScripts() |> ignore

let Version list =
    for package, version in list do
        add package version

    deps.Install(false)

    generateScripts() |> ignore