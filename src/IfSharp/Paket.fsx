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

let private addGitHub repo file version options =
    remove_quiet repo
    deps.AddGithub(
        Some "GitHub",
        repo,
        file,
        version,
        options)
    
let private GitHubString gitHubRepoString =
    let GitHubRepoStringCheck =
        System.Text.RegularExpressions.Regex("^[a-zA-Z\d]+(-[a-zA-Z\d]+)*/[a-zA-Z\d\.]+(-[a-zA-Z\d\.]+)*(:[a-zA-Z\d\.]+(-[a-zA-Z\d\.]+)*)?( [a-zA-Z\d\.]+(-[a-zA-Z\d\.]+)*(/[a-zA-Z\d\.]+(-[a-zA-Z\d\.]+)*)*)*$")
    let GitHubRepoStringCheckIsValid (s:string) = GitHubRepoStringCheck.IsMatch s

    if not(GitHubRepoStringCheckIsValid gitHubRepoString)
    then raise (System.ArgumentException("GitHub repository string should match the pattern: user/repo[:version][ file]"))
    
    let mutable file = ""
    let mutable version = ""

    let splitBy delimiter (line:string)  = Seq.toList (line.Split delimiter)
    let splitByColon = splitBy [| ':' |]
    let splitBySpace = splitBy [| ' ' |]

    let splitedBySpace = splitBySpace gitHubRepoString
        
    let splitedByColon = 
        if splitedBySpace.Length > 1
        then
            file <- splitedBySpace.[1]
            splitByColon splitedBySpace.[0]
        else splitByColon gitHubRepoString

    let repo =
        if splitedByColon.Length > 1
        then
            version <- splitedByColon.[1]
            splitedByColon.[0]
        else splitedByColon.[0]
        
    addGitHub repo file version InstallerOptions.Default
    deps.Install(false)
    ()

let GitHub list =
    for repo in list do
        GitHubString repo
    deps.Install(false)
    ()

let Version list =
    for package, version in list do
        add package version

    deps.Install(false)
    ()

let Clear() =
    deps.GetInstalledPackages() |> List.iter (fun (_, package, _) -> remove_quiet package)
    add "FSharp.Core" "= 4.3.4"
    ()