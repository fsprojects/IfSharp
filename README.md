# IfSharp, Jupyter and F# Azure Notebooks

This is the F# implementation for [Jupyter](http://jupyter.org/). View the [Feature Notebook](FSharp_Jupyter_Notebooks.ipynb) for some of the features that are included.

You can use Jupyter F# Notebooks for free (with free server-side execution) at [Azure Notebooks](https://notebooks.azure.com/). If you select "Show me some samples", then there is an "Introduction to F#" which guides you through the language and its use in Jupyter.

Build status: [![Build status](https://ci.appveyor.com/api/projects/status/7da6fkdqqm1g3cri?svg=true)](https://ci.appveyor.com/project/cgravill/ifsharp) (master/Windows) [![Build Status](https://travis-ci.org/fsprojects/IfSharp.svg?branch=master)](https://travis-ci.org/fsprojects/IfSharp) (master/Travis)

# Compatibility
IfSharp supports Jupyter 4.0, 4.1, 4.2 and works with both Python 2.X and Python 3.X

If you need IPython 1.x or 2.x support please see the archived https://github.com/fsprojects/IfSharp/tree/ipython-archive

# Automatic Installation
Previous releases for the IPython notebook are here: [release repository](https://github.com/fsprojects/IfSharp/releases).
Automatic installs for Jupyter will be provided in the future.

# Running inside a Docker container
There is a Docker file for running the F# kernel v. 3.0.0-alpha in a container.
Build the container with: 

`docker build -t ifsharp:3.0.0-alpha .`

Run it with:

`docker run -d -v your_local_notebooks_dir:/notebooks -p your_port:8888 ifsharp:3.0.0-alpha`

The container exposes a volume called `notebooks` where the files get saved. On Linux, connect to the notebook on `http://localhost:your_port` and, on Windows, use `http://your_docker_machine:your_port`.

# Manual Installation (Windows)
1. Download [Anaconda](http://continuum.io/downloads) for Python 3.6
2. Launch Anaconda3-4.4.0-Windows-x86_64.exe (or x-86.exe for 32-bit)
   Click through the installation wizard, choosing the given install location. At the 'advanced installation options' screen shown below, select "Add Anaconda to my PATH environment variable". The installer warns against this step, as it can clash with previously installed software, however it's essential in running IfSharp. Now install. 

This should also install Jupyter: you may check this by entering 'jupyter notebook' into the Anaconda console window. If Jupyter does not launch (it should launch in the browser), install using 'pip install jupyter', or by following [Jupyter](http://jupyter.readthedocs.io/en/latest/install.html) instructions.

![Installation screenshot](/docs/files/img/anaconda-installation.png)
***

3. Download current zip release of IfSharp [v3.0.0-beta2](https://github.com/fsprojects/IfSharp/releases/download/v3.0.0-beta2/IfSharp.v3.0.0-beta2.zip)
4. Run IfSharp.exe (IfSharp application icon). 

Jupyter with IfSharp can be run via "jupyter notebook" in future

# Troubleshooting
If the launch fails in the console window, check that the Anaconda version used is currently added to the path. If not, uninstalling Anaconda and reinstalling using instructions 1-

# Manual Installation (Mac)
1. Install [Jupyter](http://jupyter.readthedocs.org/en/latest/install.html) via pip or Anaconda etc.
2. Install [Mono](http://www.mono-project.com/download/) (tested Mono 4.2.4 & Mono 5.0.1)
3. Download current IfSharp zip release [v3.0.0-beta2](https://github.com/fsprojects/IfSharp/releases/download/v3.0.0-beta2/IfSharp.v3.0.0-beta2.zip)
4. Unzip the release then run `mono IfSharp.exe`
5. Run `jupyter notebook`, the IfSharp kernel should now be one of the supported kernel types.

# Manual Installation (Linux)
1. Install [Jupyter](http://jupyter.readthedocs.org/en/latest/install.html) via pip or Anaconda etc.
2. Install [Mono](http://www.mono-project.com/docs/getting-started/install/linux/) (Tested mono 5.2) and F# (tested 4.1).
3. Download the current IfSharp zip release [v3.0.0-beta2](https://github.com/fsprojects/IfSharp/releases/download/v3.0.0-beta2/IfSharp.v3.0.0-beta2.zip)
4. Unzip the release then run `mono IfSharp.exe` (this sets up the Jupyter kernel files in `~/.local/share/jupyter/kernels/ifsharp/`) 
6. Run `jupyter notebook`, the IfSharp kernel should now be one of the supported kernel types.


# Screens
## Intellisense
![Intellisense Example #1](/docs/files/img/intellisense-1.png?raw=true "Intellisense Example #1")
***

![Intellisense Example #2](docs/files/img/intellisense-2.png?raw=true "Intellisense Example #2")
***

![Intellisense Example #4 #r Directive](docs/files/img/intellisense-reference.gif?raw=true "Intellisense Example #3 #r Directive")
***

![Intellisense Example #5 #load Directive](docs/files/img/intellisense-5.png?raw=true "Intellisense Example #load Directive")
***

## Integrated NuGet
![NuGet Example](docs/files/img/integratedNuget.png?raw=true "NuGet example")

## Inline Error Messages
![Inline Error Message](docs/files/img/errors-1.png?raw=true "Inline error message")
