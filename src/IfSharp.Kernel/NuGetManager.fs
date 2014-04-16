namespace IfSharp.Kernel

open NuGet
open NuGet.Commands

open System
open System.Collections.Generic
open System.IO
open System.Text
open System.Reflection
open Microsoft.FSharp.Compiler

(** Assembly information *)
type AssemblyInfo = { FileName : string; GuessedVersion : string; }

(** Represents a NuGet package *)
type NuGetPackage = { Package : Option<IPackage>; Assemblies : seq<IPackageAssemblyReference>; Error : string }

(** Wrapper for ErrorInfo *)
type CustomErrorInfo (fileName, startLine, startColumn, endLine, endColumn, message, severity, subCategory) =

    member this.FileName = fileName;
    member this.StartLine = startLine;
    member this.StartColumn = startColumn;
    member this.EndLine = endLine;
    member this.EndColumn = endColumn;
    member this.Message = message;
    member this.Severity = severity;
    member this.Subcategory = subCategory;

    static member From(e : ErrorInfo) =
        let severityString = match e.Severity with Severity.Error -> "Error" | _ -> "Warning"
        CustomErrorInfo(e.FileName, e.StartLineAlternate, e.StartColumn, e.EndLineAlternate, e.EndColumn, e.Message, severityString, e.Subcategory)

(** The results from preprocessing some code *)
type PreprocessResults =
    {
        OriginalLines : string[];
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

(** Custom command for installing nuget packages. This is needed because of protected members. *)
type CustomInstallCommand() = 
    inherit InstallCommand()

    (** Finds the specified package with the specified version *)
    member this.FindPackage (packageId : string, version : string) =
        
        let fileSystem = this.CreateFileSystem(this.OutputDirectory)
        let packageManager = this.CreatePackageManager(fileSystem, false)
        
        if String.IsNullOrEmpty(version) then
            packageManager.LocalRepository.FindPackage(packageId)
        else
            let semanticVersion = SemanticVersion(version)
            packageManager.LocalRepository.FindPackage(packageId, semanticVersion)

module NuGetManagerInternals =

    (** Separates a list of lines between into two partitions, the first list are the directive lines, second list is the other lines *)
    let partitionLines(directive) (lines : string[]) =
        lines
        |> Seq.mapi (fun (idx) (line) -> (idx, line))
        |> Seq.toList
        |> List.partition (fun (idx, line) -> line.StartsWith(directive))

    (** Separates a list of lines between into two partitions, the first list are the directive lines, second list is the other lines *)
    let partitionSource(directive) (source : string) =
        let delimiters = [|"\r\n"; "\n"; "\r";|]
        partitionLines directive (source.Split(delimiters, StringSplitOptions.None))

    (**
     * Parses a directive line. Example: #N "FSharp.Compiler.dll"
     *)
    let parseDirectiveLine (prefix : string) (line : string) = 
        line.Substring(prefix.Length + 1).Trim().Trim('"')

(** The NuGetManager class contains methods for downloading nuget packages and such *)
type NuGetManager (executingDirectory : string) =

    let syncObject = Object()
    let nugetExecutable = Path.Combine(executingDirectory, "nuget.exe")
    let packagesDir = Path.Combine(executingDirectory, "packages")
    let packagesCache = Dictionary<string, NuGetPackage>()
    let packageSource = Config.DefaultNuGetSource

    // events from the NuGet.exe
    let errDataReceivedEvent = Event<_>()
    let outDataReceivedEvent = Event<_>()

    (** The directory for the packages *)
    member this.PackagesDir = packagesDir

    (** Gets the full assembly path of the specified package and assembly *)
    member this.GetFullAssemblyPath (pkg : NuGetPackage, ass : IPackageAssemblyReference) =
        let dir = pkg.Package.Value.Id + "." + pkg.Package.Value.Version.ToString()
        FileInfo(Path.Combine(packagesDir, dir, ass.Path)).FullName

    (** This event is called whenever a line is written to the error writer *)
    [<CLIEvent>]
    member this.StdErrDataReceived = errDataReceivedEvent.Publish

    (** This event is called whenever a line is written to the error writer *)
    [<CLIEvent>]
    member this.StdOutDataReceived = outDataReceivedEvent.Publish

    (** Downloads a nuget package by the specified name *)
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
                if not (String.IsNullOrWhiteSpace(version)) then
                    installer.Version <- version
                if not (String.IsNullOrWhiteSpace(packageSource)) then
                    installer.Source.Add(packageSource)

                // install
                let executeResults = 
                    try
                        installer.Execute()
                        None
                    with
                    | ex -> Some ex

                // start and wait for exit
                if executeResults.IsSome then
                    
                    { Package = None; Assemblies = Seq.empty; Error = executeResults.Value.Message }

                else

                    let pkg = installer.FindPackage(nugetPackage, version)
                    let maxFramework = 
                        pkg.AssemblyReferences
                        |> Seq.map (fun x -> x.TargetFramework)
                        |> Seq.filter (fun x -> x.Identifier = ".NETFramework")
                        |> Seq.maxBy (fun x -> x.Version)

                    let assemblies =
                        pkg.AssemblyReferences 
                        |> Seq.filter (fun x -> x.TargetFramework = maxFramework)

                    packagesCache.Add(key, { Package = Some pkg; Assemblies = assemblies; Error = ""; })
                    packagesCache.[key]

        )

    (**
     * Parses a 'nuget line'. Example #N "<package>[/<version>[/pre]]".
     * Returns a tuple with the name of the package, the version, and if
     * prerelease should be used or not.
     *)
    member this.ParseNugetLine (line : string) = 
        
        let contents = NuGetManagerInternals.parseDirectiveLine "#N" line
        if contents.Contains("/") then
            let splits = contents.Split([| '/' |])
            if splits.Length > 2 then
                (splits.[0], Some splits.[1], true)
            else
                (splits.[0], Some splits.[1], false)
        else
            (contents, None, false)

    (** Preprocesses the specified source string *)
    member this.Preprocess (source : string) =
        
        // split the source code into lines, then get the nuget lines
        let lines = source.Split('\n')
        let (nugetLines, otherLines) = NuGetManagerInternals.partitionLines "#N" lines

        // parse the nuget lines and then download the packages
        let nugetPackages =
            nugetLines
            |> Seq.map (fun (idx, line) -> (idx, this.ParseNugetLine(line)))
            |> Seq.map (fun (idx, line) -> (idx, this.DownloadNugetPackage(line)))
            |> Seq.toArray

        // gather errors
        let errors =
            nugetPackages
            |> Seq.filter (fun (idx, package) -> String.IsNullOrEmpty(package.Error) = false)
            |> Seq.map (fun (idx, package) -> CustomErrorInfo("", idx, 0, idx, lines.[idx].Length, package.Error, "Error", ""))
            |> Seq.toArray

        {
            OriginalLines = lines;
            NuGetLines = nugetLines |> Seq.map(fun (idx, line) -> line) |> Seq.toArray;
            FilteredLines = otherLines |> Seq.map(fun (idx, line) -> line) |> Seq.toArray;
            Packages = nugetPackages |> Seq.map(fun (idx, package) -> package) |> Seq.toArray;
            Errors = errors;
        }
