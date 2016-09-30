#r "IfSharp.Kernel.dll"
#r "Chessie.dll"
#r "Paket.Core.dll"

open System

let private dir =
    Reflection.Assembly.GetEntryAssembly().Location
    |> IO.Path.GetDirectoryName

let deps = 
    try
        let d = Paket.Dependencies.Locate(dir)
        d.Install(false)
        d
    with _ ->
        Paket.Dependencies.Init(dir)
        Paket.Dependencies.Locate(dir)

let Package list =
    for package in list do
        deps.Add(package)

let Version list =
    for package, version in list do
        deps.Add(None, package, version)
