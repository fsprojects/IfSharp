# IfSharp

F# implementation for [Jupyter](http://jupyter.org/). View the [Feature Notebook](http://nbviewer.jupyter.org/github/fsprojects/IfSharp/blob/master/Feature%20Notebook.ipynb) for some of the features that are included.

For more information view the [documentation](http://fsprojects.github.io/IfSharp/). IfSharp is 64-bit *ONLY*.

# Compatibility
IfSharp works with Jupyter 4.0 and 4.1 and works with both Python 2.X and Python 3.X

# Automatic Installation
Previous releases for the IPython notebook are here: [release repository](https://github.com/fsprojects/IfSharp/releases).
Automatic installs for Jupyter will be provided in the future.

# Manual Installation (Windows)
1. Install [Anaconda](http://continuum.io/downloads)
2. Install [Jupyter](http://jupyter.readthedocs.org/en/latest/install.html)
3. Download current zip release [v3.0.0-alpha1](https://github.com/fsprojects/IfSharp/releases/download/v3.0.0-alpha1/IfSharp.v3.0.0-alpha1.zip)
4. Run IfSharp.exe

Jupyter with IfSharp can be run via "jupyter notebook" in future

# Manual Installation (Mac)
1. Install [Anaconda](http://continuum.io/downloads)
2. Install [Jupyter](http://jupyter.readthedocs.org/en/latest/install.html)
3. Install [Mono](http://www.mono-project.com/download/) (tested 4.2.4)
3. Download current zip release [v3.0.0-alpha1](https://github.com/fsprojects/IfSharp/releases/download/v3.0.0-alpha1/IfSharp.v3.0.0-alpha1.zip)
4. Unzip the release then run `mono IfSharp.exe`
5. (workaround: Copy ~/.local/share/jupyter/kernels/ifsharp to /usr/local/share/jupyter/kernels/ifsharp)
6. Run `jupyter notebook`

The workaround is for IPython/Jupyter changes will be fixed in a future release.

# Manual Installation (Linux)
1. Install [Jupyter](http://jupyter.readthedocs.org/en/latest/install.html) via pip or Anaconda etc.
2. Install [Mono](http://www.mono-project.com/docs/getting-started/install/linux/) (tested 4.2.4) and F# (tested 4.0).
3. Download the current IfSharp zip release [v3.0.0-alpha1](https://github.com/fsprojects/IfSharp/releases/download/v3.0.0-alpha1/IfSharp.v3.0.0-alpha1.zip)
4. Unzip the release to a safe place such as `~/opt/ifsharp`.
5. Run `mono ~/opt/ifsharp/IfSharp.exe` to set up the jupyter config files in `~/.jupyter/` and `~/.local/share/jupyter/kernels/ifsharp/`.
  1. (For XPlot) From the install directory `~/opt/` run `mono paket.bootstrapper.exe` then `mono paket.exe install` 
6. Run `jupyter notebook`, the IfSharp kernel should now be one of the supported kernel types.


# Screens
## Intellisense
![Intellisense Example #1](/docs/files/img/intellisense-1.png?raw=true "Intellisense Example #1")
***

![Intellisense Example #2](docs/files/img/intellisense-2.png?raw=true "Intellisense Example #2")
***

![Intellisense Example #3 With Chart](docs/files/img/intellisense-3.png?raw=true "Intellisense Example #3 With Chart")
***

![Intellisense Example #4 #r Directive](docs/files/img/intellisense-reference.gif?raw=true "Intellisense Example #3 #r Directive")
***

![Intellisense Example #5 #load Directive](docs/files/img/intellisense-5.png?raw=true "Intellisense Example #load Directive")
***

## Integrated NuGet
![NuGet Example](docs/files/img/NuGet-1.png?raw=true "NuGet example")

## Inline Error Messages
![Inline Error Message](docs/files/img/errors-1.png?raw=true "Inline error message")
