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

let RootPath =
    deps.RootPath

let private remove_quiet packageName =
    deps.Remove(
        None,
        packageName,
        force = false,
        interactive = false,
        installAfter = false)

let private add package version =
    remove_quiet package
    deps.Add(
        None,
        package,
        version,
        force = false,
        withBindingRedirects = false,
        cleanBindingRedirects = false,
        createNewBindingFiles = false,
        interactive = false,
        installAfter = false,
        semVerUpdateMode = SemVerUpdateMode.NoRestriction,
        touchAffectedRefs = false)

let Package list =
    for package in list do
        add package ""

    deps.Install(false)
    ()


type GitHubDependency = {
    Repo: string
    Branch: string option
    File: string option
}

type OrdinaryGitDependency = {
    Repo: string
    Branch: string option
}

type GitDependency =
    | GitHub of GitHubDependency
    | OrdinaryGit of OrdinaryGitDependency

let gitHubRepo repoName =
    GitHub
        {
            Repo = repoName
            Branch = None
            File = None
        }

let gitRepo repoName =
    OrdinaryGit
        {
            Repo = repoName
            Branch = None
        }

let chooseBranch branchName gitDependency =
    match gitDependency with
        | GitHub gitHub -> GitHub {gitHub with Branch = Some branchName}
        | OrdinaryGit git -> OrdinaryGit {git with Branch = Some branchName}
        
let singleFile fileName gitDependency =
    match gitDependency with
        | GitHub gitHub -> GitHub {gitHub with File = Some fileName}
        | OrdinaryGit git -> failwith "Git dependency doesn't support fetching single file"

let private addGitHub repo version file options =
    remove_quiet repo
    deps.AddGithub(
        Some "GitHub",
        repo,
        file,
        version,
        options)
        
let private addOrdinaryGit repo version options =
    deps.AddGit(
        Some "Git",
        repo,
        version,
        options)

let private addGit =
    function
        | GitHub gitHub -> 
            let branch =
                match gitHub.Branch with
                | Some branch -> branch
                | None -> ""
            let file =
                match gitHub.File with
                | Some file -> file
                | None -> ""
            addGitHub gitHub.Repo branch file InstallerOptions.Default
        | OrdinaryGit git -> 
            let branch =
                match git.Branch with
                | Some branch -> branch
                | None -> ""
            addOrdinaryGit git.Repo branch InstallerOptions.Default

let Git seq =
    Seq.iter addGit seq
    deps.Install(false)

let Version list =
    for package, version in list do
        add package version

    deps.Install(false)
    ()

let Clear() =
    deps.GetInstalledPackages() |> List.iter (fun (_, package, _) -> remove_quiet package)
    add "FSharp.Core" "= 4.3.4"
    ()