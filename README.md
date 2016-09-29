# IfSharp

F# implementation for [Jupyter](http://jupyter.org/). View the [Feature Notebook](Feature%20Notebook%20format%204_0.ipynb) for some of the features that are included.

There is documentation for the existing release version: [documentation](http://fsprojects.github.io/IfSharp/) we're updating this for the Jupyter branch.

# Compatibility
IfSharp supports Jupyter 4.0 and 4.1 and works with both Python 2.X and Python 3.X

If you need IPython 1.x or 2.x support please see the archived https://github.com/fsprojects/IfSharp/tree/ipython-archive

# Automatic Installation
Previous releases for the IPython notebook are here: [release repository](https://github.com/fsprojects/IfSharp/releases).
Automatic installs for Jupyter will be provided in the future.

# Running inside a Docker container
The `jupyter` branch has a Docker file for running the F# kernel v. 3.0.0-alpha in a container.
Build the container with: 

`docker build -t ifsharp:3.0.0-alpha .`

Run it with:

`docker run -d -v your_local_notebooks_dir:/notebooks -p your_port:8888 ifsharp:3.0.0-alpha`

The container exposes a volume called `notebooks` where the files get saved. On Linux, connect to the notebook on `http://localhost:your_port` and, on Windows, use `http://your_docker_machine:your_port`.

# Manual Installation (Windows)
1. Install [Anaconda](http://continuum.io/downloads)
2. Install [Jupyter](http://jupyter.readthedocs.org/en/latest/install.html)
3. Download current zip release [v3.0.0-alpha2](https://github.com/fsprojects/IfSharp/releases/download/v3.0.0-alpha2/IfSharp.v3.0.0-alpha2.zip)
4. Run IfSharp.exe

Jupyter with IfSharp can be run via "jupyter notebook" in future

# Manual Installation (Mac)
1. Install [Anaconda](http://continuum.io/downloads)
2. Install [Jupyter](http://jupyter.readthedocs.org/en/latest/install.html)
3. Install [Mono](http://www.mono-project.com/download/) (tested 4.2.4)
3. Download current zip release [v3.0.0-alpha2](https://github.com/fsprojects/IfSharp/releases/download/v3.0.0-alpha2/IfSharp.v3.0.0-alpha2.zip)
4. Unzip the release then run `mono IfSharp.exe`
5. (workaround: Copy ~/.local/share/jupyter/kernels/ifsharp to /usr/local/share/jupyter/kernels/ifsharp)
6. Run `jupyter notebook`

The workaround is for IPython/Jupyter changes will be fixed in a future release.

# Manual Installation (Linux)
1. Install [Jupyter](http://jupyter.readthedocs.org/en/latest/install.html) via pip or Anaconda etc.
2. Install [Mono](http://www.mono-project.com/docs/getting-started/install/linux/) (tested 4.2.4) and F# (tested 4.0).
3. Download the current IfSharp zip release [v3.0.0-alpha2](https://github.com/fsprojects/IfSharp/releases/download/v3.0.0-alpha2/IfSharp.v3.0.0-alpha2.zip)
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
