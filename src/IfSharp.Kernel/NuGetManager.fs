namespace IfSharp.Kernel

open NuGet
open NuGet.Commands

open System
open System.Collections.Generic
open System.IO
open System.Text
open System.Reflection
open Microsoft.FSharp.Compiler

/// Assembly information
type AssemblyInfo = { FileName : string; GuessedVersion : string; }

/// Represents a NuGet package
type NuGetPackage = { Package : Option<IPackage>; Assemblies : seq<IPackageAssemblyReference>; FrameworkAssemblies: seq<FrameworkAssemblyReference>; Error : string }

/// Wrapper for ErrorInfo
type CustomErrorInfo =
    {
        FileName : string
        StartLine : int
        StartColumn : int
        EndLine : int
        EndColumn : int
        Message : string
        Severity : string
        Subcategory : string
        CellNumber : int
    }
    static member From(fileName, startLine, startColumn, endLine, endColumn, message, severity, subcategory) = 
        {
            FileName = fileName
            StartLine = startLine
            StartColumn = startColumn
            EndLine = endLine
            EndColumn = endColumn
            Message = message
            Severity = severity
            Subcategory = subcategory
            CellNumber = 0
        }
    static member From(e : FSharpErrorInfo) =
        let severityString = match e.Severity with FSharpErrorSeverity.Error -> "Error" | _ -> "Warning"
        {
            FileName = e.FileName
            StartLine = e.StartLineAlternate
            StartColumn = e.StartColumn
            EndLine = e.EndLineAlternate
            EndColumn = e.EndColumn
            Message = e.Message
            Severity = severityString
            Subcategory = e.Subcategory
            CellNumber = 0
        }

/// The results from preprocessing some code
type PreprocessResults =
    {
        OriginalLines : string[];
        HelpLines : string[];
        FsiOutputLines : string[];
        NuGetLines : string[];
        FilteredLines : string[];
        Packages : NuGetPackage[];
        Errors: CustomErrorInfo[];
    }

type VersionWithType =
    {
        ReleaseType : string;
        Version : Version;
    }

/// Custom command for installing nuget packages. This is needed because of protected members.
type CustomInstallCommand() = 
    inherit InstallCommand()

    /// Finds the specified package with the specified version
    member this.FindPackage (packageId : string, version : string) =
        
        let fileSystem = this.CreateFileSystem(this.OutputDirectory)
        let packageManager = this.CreatePackageManager(fileSystem, false)
        
        if String.IsNullOrEmpty(version) then
            packageManager.LocalRepository.FindPackage(packageId)
        else
            let semanticVersion = SemanticVersion(version)
            packageManager.LocalRepository.FindPackage(packageId, semanticVersion)

/// The NuGetManager class contains methods for downloading nuget packages and such
type NuGetManager (executingDirectory : string) =

    let syncObject = Object()
    let nugetExecutable = Path.Combine(executingDirectory, "nuget.exe")
    let packagesDir = Path.Combine(executingDirectory, "packages")
    let packagesCache = Dictionary<string, NuGetPackage>()
    let packageSource = Config.DefaultNuGetSource

    // events from the NuGet.exe
    let errDataReceivedEvent = Event<_>()
    let outDataReceivedEvent = Event<_>()

    /// The directory for the packages
    member this.PackagesDir = packagesDir

    /// Gets the full assembly path of the specified package and assembly
    member this.GetFullAssemblyPath (pkg : NuGetPackage, ass : IPackageAssemblyReference) =
        let dir = pkg.Package.Value.Id + "." + pkg.Package.Value.Version.ToString()
        FileInfo(Path.Combine(packagesDir, dir, ass.Path)).FullName

    /// This event is called whenever a line is written to the error writer
    [<CLIEvent>]
    member this.StdErrDataReceived = errDataReceivedEvent.Publish

    /// This event is called whenever a line is written to the error writer
    [<CLIEvent>]
    member this.StdOutDataReceived = outDataReceivedEvent.Publish

    /// Downloads a nuget package by the specified name
    member this.DownloadNugetPackage (nugetPackage : string, version : Option<string>, prerelease : bool) =
        
        let version = defaultArg version ""
        let key = String.Format("{0}/{1}/{2}", nugetPackage, version, prerelease)

        lock (syncObject) (fun() ->

            if packagesCache.ContainsKey(key) then
            
                packagesCache.[key]
            
            else

                // build the installer
                let installer = CustomInstallCommand()
                installer.Console <- NuGet.Common.Console()
                installer.Arguments.Add(nugetPackage)
                installer.OutputDirectory <- packagesDir
                installer.Prerelease <- prerelease
                if not <| String.IsNullOrWhiteSpace(version) then
                    installer.Version <- version
                if not <| String.IsNullOrWhiteSpace(packageSource) then
                    for source in packageSource.Split([|';'|], StringSplitOptions.RemoveEmptyEntries) do
                        installer.Source.Add(source)

                // install
                let executeResults = 
                    try
                        installer.Execute()
                        None
                    with
                    | ex -> Some ex

                // start and wait for exit
                if executeResults.IsSome then
                    
                    { Package = None; Assemblies = Seq.empty; FrameworkAssemblies = Seq.empty; Error = executeResults.Value.Message }

                else

                    let pkg = installer.FindPackage(nugetPackage, version)
                    
                    if pkg.GetSupportedFrameworks().IsEmpty() then // content-only package
                        packagesCache.Add(key, { Package = Some pkg; Assemblies = Seq.empty; FrameworkAssemblies = Seq.empty; Error = ""; });
                    else
                        let getCompatibleItems targetFramework items =
                            let retval, compatibleItems = VersionUtility.TryGetCompatibleItems(targetFramework, items)
                            if retval then compatibleItems else Seq.empty

                        let maxFramework =
                            // try full framework first - if none is supported, fall back
                            let fullFrameworks = pkg.GetSupportedFrameworks() |> Seq.filter (fun x -> x.Identifier = ".NETFramework") |> Seq.toArray
                            if Array.length fullFrameworks > 0 then fullFrameworks |> Array.maxBy (fun x -> x.Version)
                            else pkg.GetSupportedFrameworks() |> Seq.maxBy (fun x -> x.Version)

                        let assemblies =
                            if not(pkg.PackageAssemblyReferences.IsEmpty()) then
                                let compatibleAssemblyReferences =
                                    getCompatibleItems maxFramework pkg.PackageAssemblyReferences
                                    |> Seq.collect (fun x -> x.References)
                                    |> Set.ofSeq
                                pkg.AssemblyReferences
                                |> Seq.filter (fun x -> compatibleAssemblyReferences.Contains x.Name && x.TargetFramework = maxFramework )
                            elif pkg.AssemblyReferences.IsEmpty() then
                                Seq.empty
                            else
                                getCompatibleItems maxFramework pkg.AssemblyReferences

                        let frameworkAssemblyReferences = getCompatibleItems maxFramework pkg.FrameworkAssemblies

                        packagesCache.Add(key, { Package = Some pkg; Assemblies = assemblies; FrameworkAssemblies = frameworkAssemblyReferences; Error = ""; })
                    
                    try
                        let name = pkg.Id + "." + pkg.Version.ToString()
                        let folder = Path.Combine(installer.OutputDirectory, name)
                        let contentDir = Path.Combine(Directory.GetCurrentDirectory(), "content")
                        let cfiles = pkg.GetContentFiles();
                        if Directory.Exists(contentDir) = false then Directory.CreateDirectory(contentDir) |> ignore
                        for f in cfiles do
                          let fullName = Path.Combine(folder, f.Path)
                          let targetName = Path.Combine(contentDir, f.EffectivePath)
                          let targetPath = Path.GetDirectoryName(targetName)
                          if Directory.Exists(targetPath) = false then Directory.CreateDirectory(targetPath) |> ignore
                          if File.Exists(targetName) then File.Delete(targetName)
                          File.Copy(fullName, targetName, true)
                        done
                    with exc -> Console.Out.WriteLine(exc.ToString())

                    packagesCache.[key]
        )

    /// Parses a 'nuget line'. Example #N "<package>[/<version>[/pre]]".
    /// Returns a tuple with the name of the package, the version, and if
    /// prerelease should be used or not.
    member this.ParseNugetLine (line : string) = 
        
        let contents = DirectivePreprocessor.parseDirectiveLine "#N" line
        if contents.Contains("/") then
            let splits = contents.Split([| '/' |])
            if splits.Length > 2 then
                (splits.[0], Some splits.[1], true)
            else
                (splits.[0], Some splits.[1], false)
        else
            (contents, None, false)

    /// Preprocesses the specified source string
    member this.Preprocess (source : string) =
        
        // split the source code into lines, then get the nuget lines
        let lines = source.Split('\n')
        let linesSplit = DirectivePreprocessor.partitionLines lines

        let orEmpty key = let opt = Map.tryFind key linesSplit
                          if opt.IsSome then opt.Value else Seq.empty

        let helpLines = DirectivePreprocessor.Line.HelpDirective |> orEmpty
        let fsiOutputLines = DirectivePreprocessor.Line.FSIOutputDirective |> orEmpty
        let nugetLines = DirectivePreprocessor.Line.NugetDirective |> orEmpty
        let otherLines = DirectivePreprocessor.Line.Other |> orEmpty

        // parse the nuget lines and then download the packages
        let nugetPackages =
            nugetLines
            |> Seq.map (fun (idx, line) -> (idx, this.ParseNugetLine(line)))
            |> Seq.map (fun (idx, line) -> (idx, this.DownloadNugetPackage(line)))
            |> Seq.toArray

        // gather errors
        let errors =
            nugetPackages
            |> Seq.filter (fun (_, package) -> String.IsNullOrEmpty(package.Error) = false)
            |> Seq.map (fun (idx, package) -> CustomErrorInfo.From("", idx, 0, idx, lines.[idx].Length, package.Error, "Error", "preprocess"))
            |> Seq.toArray

        {
            OriginalLines = lines;
            HelpLines = helpLines |> Seq.map(fun (_, line) -> line) |> Seq.toArray;
            FsiOutputLines = fsiOutputLines |> Seq.map(fun (_, line) -> line) |> Seq.toArray;
            NuGetLines = nugetLines |> Seq.map(fun (_, line) -> line) |> Seq.toArray;
            
            FilteredLines = otherLines |> Seq.map(fun (_, line) -> line) |> Seq.toArray;
            Packages = nugetPackages |> Seq.map(fun (_, package) -> package) |> Seq.toArray;
            Errors = errors;
        }
