module Config

open System
open System.Configuration
open System.IO

/// Convenience method for getting a setting with a default value
let defaultConfig (name : string, defaultValue) =
    let value = ConfigurationManager.AppSettings.[name]
    if value = null then defaultValue else value

// the configuration properties
let DefaultNuGetSource = defaultConfig("DefaultNuGetSource", "")

let KernelDir = 
  let thisExecutable = System.Reflection.Assembly.GetEntryAssembly().Location
  let userDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
  let appData =  
    match Environment.OSVersion.Platform with
      | PlatformID.Win32Windows | PlatformID.Win32NT -> Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)
      | PlatformID.MacOSX -> Path.Combine(userDir, "Library")
      | _ -> Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) // PlatformID.Unix
  let jupyterDir = 
    match Environment.OSVersion.Platform with 
      | PlatformID.Unix -> Path.Combine(appData, "jupyter")
      | _ -> Path.Combine(appData, "Jupyter")
  let kernelsDir = Path.Combine(jupyterDir, "kernels")
  let kernelDir = Path.Combine(kernelsDir, "ifsharp")
  kernelDir
let StaticDir = Path.Combine(KernelDir, "static")
let TempDir = Path.Combine(StaticDir, "temp");