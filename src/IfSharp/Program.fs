open System
open IfSharp.Kernel
open System.Globalization
open System.IO
open Trinet.Core.IO.Ntfs

//This hidden info can be leftover depending how you unzip a release on Windows
let ClearAlternativeStreamsWindows() =
    let path = System.Reflection.Assembly.GetEntryAssembly().Location
    if path <> null then
        for filePath in (FileInfo(path).Directory.GetFileSystemInfos()) do
            filePath.DeleteAlternateDataStream("Zone.Identifier") |> ignore

[<EntryPoint>]
let main args = 
    if (Environment.OSVersion.Platform <> PlatformID.Unix && Environment.OSVersion.Platform <> PlatformID.MacOSX) then
        ClearAlternativeStreamsWindows();
    CultureInfo.DefaultThreadCurrentCulture <- CultureInfo.InvariantCulture
    CultureInfo.DefaultThreadCurrentUICulture <- CultureInfo.InvariantCulture
    App.Start(args)
    0