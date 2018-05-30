namespace IfSharp.Kernel

open System
open System.Collections.Generic
open System.IO
open System.Text
open System.Reflection
open Microsoft.FSharp.Compiler

/// Assembly information
type AssemblyInfo = { FileName : string; GuessedVersion : string; }


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
    static member From(e : Microsoft.FSharp.Compiler.SourceCodeServices.FSharpErrorInfo) =
        let severityString = match e.Severity with Microsoft.FSharp.Compiler.SourceCodeServices.FSharpErrorSeverity.Error -> "Error" | _ -> "Warning"
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
        Errors: CustomErrorInfo[];
    }

type VersionWithType =
    {
        ReleaseType : string;
        Version : Version;
    }

/// The NuGetManager class contains methods for downloading nuget packages and such
type NuGetManager (executingDirectory : string) =

    let syncObject = Object()
    let packagesDir = Path.Combine(executingDirectory, "packages")
    let packageSource = Config.DefaultNuGetSource

    // events from the NuGet.exe
    let errDataReceivedEvent = Event<_>()
    let outDataReceivedEvent = Event<_>()

    /// The directory for the packages
    member this.PackagesDir = packagesDir

    /// This event is called whenever a line is written to the error writer
    [<CLIEvent>]
    member this.StdErrDataReceived = errDataReceivedEvent.Publish

    /// This event is called whenever a line is written to the error writer
    [<CLIEvent>]
    member this.StdOutDataReceived = outDataReceivedEvent.Publish

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

        //NuGet broke, we've replaced it with Paket: https://github.com/fsprojects/IfSharp/issues/106

        {
            OriginalLines = lines;
            HelpLines = helpLines |> Seq.map(fun (_, line) -> line) |> Seq.toArray;
            FsiOutputLines = fsiOutputLines |> Seq.map(fun (_, line) -> line) |> Seq.toArray;
            NuGetLines = nugetLines |> Seq.map(fun (_, line) -> line) |> Seq.toArray;
            FilteredLines = otherLines |> Seq.map(fun (_, line) -> line) |> Seq.toArray;
            Errors = Array.empty //errors;
        }
