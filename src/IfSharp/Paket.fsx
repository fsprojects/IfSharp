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
        None,
        repo,
        file,
        version,
        options)
        
let private getPartOrDefault delimiter s =  
    let splitBy delimiter (line:string)  = Seq.toList (line.Split delimiter)
    let splitedByDelimiter = splitBy [|delimiter|] s
    if splitedByDelimiter.Length > 1 then            
        splitedByDelimiter.[0], splitedByDelimiter.[1]
    else
        splitedByDelimiter.[0], ""
 
let private GitHubString gitHubRepoString =
    //This RegEx may be overly restrictive a prevent characters, is there are more sustainable way? Does Paket itself have a checker?
    let GitHubRepoStringCheck =
        System.Text.RegularExpressions.Regex("^[_a-zA-Z\d]+(-[_a-zA-Z\d]+)*/[_a-zA-Z\d\.]+(-[_a-zA-Z\d\.]+)*(:[_a-zA-Z\d\.]+(-[_a-zA-Z\d\.]+)*)?( [_a-zA-Z\d\.]+(-[_a-zA-Z\d\.]+)*(/[_a-zA-Z\d\.]+(-[_a-zA-Z\d\.]+)*)*)*$")
    let GitHubRepoStringCheckIsValid (s:string) = GitHubRepoStringCheck.IsMatch s

    if not(GitHubRepoStringCheckIsValid gitHubRepoString)
    then raise (System.ArgumentException("GitHub repository string should match the pattern: user/repo[:version][ file]", "GitHubRepoString"))
     
        
    let repo, file, version =
        let repoVersion, file =
            getPartOrDefault ' ' gitHubRepoString
        let repo, version =
            getPartOrDefault ':' repoVersion
        repo, file, version

    addGitHub repo file version InstallerOptions.Default
    deps.Install(false)
    ()

let GitHub list =
    for repo in list do
        GitHubString repo
    deps.Install(false)
    ()

let private addGit repo version options =
    printfn "addGit repo=%s version=%s" repo version
    deps.AddGit(
        None,
        repo,
        version,
        options)
    
let private GitString gitRepoString =
    let GitRepoStringCheck =
        System.Text.RegularExpressions.Regex("^\S*(\s+\S*)?$")
    let GitRepoStringCheckIsValid (s:string) = GitRepoStringCheck.IsMatch s

    if not(GitRepoStringCheckIsValid gitRepoString)
    then raise (System.ArgumentException("Git repository string should match the pattern: repo[ version]", "GitRepoString"))
    
    let repo, version = getPartOrDefault ' ' gitRepoString
    addGit repo version InstallerOptions.Default
    
    deps.Install(false)
    ()

let Git list =
    for repo in list do
        GitString repo
    deps.Install(false)
    ()

let Version list =
    for package, version in list do
        add package version

    deps.Install(false)
    ()

let Clear() =
    //TODO: this currently doesn't remove GitHub packages. Can't find a corresponding Paket api
    deps.GetInstalledPackages() |> List.iter (fun (_, package, _) -> remove_quiet package)
    add "FSharp.Core" "= 4.6.2"
    ()
