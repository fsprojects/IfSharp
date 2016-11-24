namespace IfSharp.Kernel

module DirectivePreprocessor =

    type Line =
        | HelpDirective
        | FSIOutputDirective
        | NugetDirective
        | Other

    let determineLineType (idx, (line:string)) =
        match line.ToLower() with
        | line when line.StartsWith "#n" -> NugetDirective
        | line when line.StartsWith "#help" -> HelpDirective
        | line when line.StartsWith "#fsioutput" -> FSIOutputDirective
        | _ -> Other

    /// Separates into map of directive types
    let partitionLines(lines : string[]) =
        lines
        |> Seq.mapi (fun (idx) (line) -> (idx, line))
        |> Seq.groupBy determineLineType
        |> Map.ofSeq

    /// Parses a directive line. Example: #N "Deedle"
    let parseDirectiveLine (prefix : string) (line : string) = 
        line.Substring(prefix.Length + 1).Trim().Trim('"')
