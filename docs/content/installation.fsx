(*** hide ***)
// This block of code is omitted in the generated HTML documentation. Use 
// it to define helpers that you do not want to show in the documentation.
#I "../../bin"
#r "IFSharp.Kernel"
#r "System.Data.dll"
#r "System.Windows.Forms.DataVisualization.dll"
#r "FSharp.Data.TypeProviders.dll"
#r "FSharp.Charting.dll"
#r "fszmq.dll"

open FSharp.Charting
open IfSharp.Kernel
open IfSharp.Kernel.Globals

(**
# Automatic Installation
1. Install [Anaconda](http://continuum.io/downloads)
2. Install [IPython](http://ipython.org/install.html)

3. Download the latest version of IfSharp from the [release repository](https://github.com/BayardRock/IfSharp/releases).
Run the setup wizard and execute the icon that is placed on the desktop.

Running the executable after it is installed will automatically generate the files necessary
for starting up (if the file structure does not exist) and then execute the `ipython notebook --profile ifsharp` command.

If the file structure does exist, only the command `ipython notebook --profile ifsharp` is executed.

To overwrite the file structure again, invoke ifsharp.exe with the install parameter: `ifsharp.exe --install`.

# Manual Installation
1. Install [Anaconda](http://continuum.io/downloads)
2. Install [IPython](http://ipython.org/install.html)
3. Run: "ipython profile create ifsharp" in your user directory
4. Open the iF# solution file, restore nuget packages, and compile it
5. Copy the files from IfSharp\ipython-profile to the iFSharp profile directory
6. Open up the copied "ipython_config.py" file and replace "%s" with the path of your compiled ifsharp executable. E.g. "C:\\git\\ifsharp\\bin\\Release\\ifsharp.exe" 
7. Run: "ipython notebook --profile ifsharp" to launch the notebook process with the F# kernel.
*)