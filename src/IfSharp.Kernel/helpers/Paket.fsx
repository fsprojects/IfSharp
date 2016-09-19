[<AutoOpen>]
module IfSharpPaket

#nowarn "211"

#r "Paket.Core.dll"

open System

let private dir =
  IO.Path.GetDirectoryName(Reflection.Assembly.GetEntryAssembly().Location)

let paket = Paket.Dependencies.Locate(dir)
