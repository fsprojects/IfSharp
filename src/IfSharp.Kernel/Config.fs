module Config

open System
open System.Configuration

/// Convenience method for getting a setting with a default value
let defaultConfig (name : string, defaultValue) =
    let value = ConfigurationManager.AppSettings.[name]
    if value = null then defaultValue else value

// the configuration properties
let DefaultNuGetSource = defaultConfig("DefaultNuGetSource", "")