namespace IfSharp.Kernel.Globals

/// This module provides access to common types and functions so the user can get intellisense
[<AutoOpen>]
module Globals = 

    type Util = IfSharp.Kernel.Util

    let Display = IfSharp.Kernel.App.Display

    let Help = IfSharp.Kernel.App.Help

    let Clear = IfSharp.Kernel.App.Clear