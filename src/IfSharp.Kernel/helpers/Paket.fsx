#nowarn "211"

#r "Chessie.dll"
#r "Paket.Core.dll"

open System

module Paket =
    let private dir =
        Reflection.Assembly.GetEntryAssembly().Location
        |> IO.Path.GetDirectoryName

    Paket.Dependencies.Init(dir)
    let deps = Paket.Dependencies.Locate(dir)

    let Package list =
        for package in list do
           deps.Add(package)

    let Version list =
        for package, version in list do
            deps.Add(None, package, version)
