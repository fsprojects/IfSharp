open System
open IfSharp.Kernel
open System.Globalization
open System.IO
open Trinet.Core.IO.Ntfs

//This hidden info can be left behind depending how you unzip a release on Windows, which in turn can break notebook's use of Paket
let ClearAlternativeStreamsWindows() =
    let path = System.Reflection.Assembly.GetEntryAssembly().Location
    if path <> null then
        for filePath in (FileInfo(path).Directory.GetFileSystemInfos()) do
            filePath.DeleteAlternateDataStream("Zone.Identifier") |> ignore

[<EntryPoint>]
let main args =
    printfn "IFSharp on .NET Core is experimental! It has known issues."

    //This is really useful if you need debug the start-up process
    //System.Diagnostics.Debugger.Launch() |> ignore

    if (Environment.OSVersion.Platform <> PlatformID.Unix && Environment.OSVersion.Platform <> PlatformID.MacOSX) then
        ClearAlternativeStreamsWindows()
    CultureInfo.DefaultThreadCurrentCulture <- CultureInfo.InvariantCulture
    CultureInfo.DefaultThreadCurrentUICulture <- CultureInfo.InvariantCulture
    App.Start args Config.NetCore
    0