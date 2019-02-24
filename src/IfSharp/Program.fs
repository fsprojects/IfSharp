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

//Move SystemNet so that the mono one is used instead https://github.com/dotnet/corefx/issues/19914
let MoveSystemNetHttp() =
    if File.Exists("System.Net.Http.dll") then
        printfn("Moving System.Net.Http.dll to Hide.System.Net.Http.dll to workaround https://github.com/dotnet/corefx/issues/19914")
        File.Move("System.Net.Http.dll", "Hide.System.Net.Http.dll")

[<EntryPoint>]
let main args =

    //This is really useful if you need debug the start-up process
    //System.Diagnostics.Debugger.Launch() |> ignore

    if (Environment.OSVersion.Platform <> PlatformID.Unix && Environment.OSVersion.Platform <> PlatformID.MacOSX) then
        ClearAlternativeStreamsWindows()
    if Type.GetType ("Mono.Runtime") <> null then
        MoveSystemNetHttp()
    CultureInfo.DefaultThreadCurrentCulture <- CultureInfo.InvariantCulture
    CultureInfo.DefaultThreadCurrentUICulture <- CultureInfo.InvariantCulture
    App.Start args Config.NetFramework
    0