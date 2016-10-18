#r "IfSharp.Kernel.dll"
#r "Chessie.dll"
#r "Paket.Core.dll"

open System
open Paket

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

let Package list =
    for package in list do
        add package ""

    deps.Install(false)

let Version list =
    for package, version in list do
        add package version

    deps.Install(false)
