(*** hide ***)
// This block of code is omitted in the generated HTML documentation. Use 
// it to define helpers that you do not want to show in the documentation.
#I "../../bin"
#r "IFSharp.Kernel"
#r "NetMQ.dll"

open IfSharp.Kernel
open IfSharp.Kernel.Globals

(**
# Windows Installation

## Automatic Installation

1. Install [Anaconda](http://continuum.io/downloads)
2. Install [IPython](http://ipython.org/install.html)
3. Download the latest version of IfSharp from the [release repository](https://github.com/BayardRock/IfSharp/releases)
4. Run the setup wizard and execute the icon that is placed on the desktop

Running the executable after it is installed will automatically generate the files necessary for starting up (if the file structure does not exist) and then execute the `ipython notebook --profile ifsharp` command.

To overwrite the file structure again, invoke ifsharp.exe with the install parameter: `ifsharp.exe --install`.

## Manual Installation

1. Install [Anaconda](http://continuum.io/downloads)
2. Install [IPython](http://ipython.org/install.html)
3. Either open the iF# solution file, restore nuget packages, and compile it
4. Or run `./build.cmd` from the command line
5. Run `isharp.exe --install` to set up the F# kernel
6. `jupyter notebook`

# Linux Installation

1. Use your package manager to install `mono`, `fsharp`, and `python3`
2. `pip3 install jupyter`
3. Download IfSharp and build it with `./build.sh`
4. Install IfSharp with `mono ./bin/ifsharp.exe --install`
5. `jupyter notebook` 

# OSX Installation

1. `brew install mono fsharp python3`
2. `pip3 install jupyter`
3. Download IfSharp and build it with `./build.sh`
4. Install IfSharp with `mono ./bin/ifsharp.exe --install`
5. `jupyter notebook` 
*)
