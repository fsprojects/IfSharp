| Build status  | Linux | macOS | Windows |
| --- | --- | ----------- | --- |
| .NET Framework / Mono | [![Build Status](https://travis-ci.org/fsprojects/IfSharp.svg?branch=master)](https://travis-ci.org/fsprojects/IfSharp) |     | [![Build Status](https://dev.azure.com/IFSharp/IFSharp/_apis/build/status/fsprojects.IfSharp?branchName=master&jobName=Windows)](https://dev.azure.com/IFSharp/IFSharp/_build/latest?definitionId=1&branchName=master) |
| .NET Core (experimental) | [![Build Status](https://dev.azure.com/IFSharp/IFSharp/_apis/build/status/fsprojects.IfSharp?branchName=master&jobName=Linux)](https://dev.azure.com/IFSharp/IFSharp/_build/latest?definitionId=1&branchName=master) | [![Build Status](https://dev.azure.com/IFSharp/IFSharp/_apis/build/status/fsprojects.IfSharp?branchName=master&jobName=macOS)](https://dev.azure.com/IFSharp/IFSharp/_build/latest?definitionId=1&branchName=master) | [![Build Status](https://dev.azure.com/IFSharp/IFSharp/_apis/build/status/fsprojects.IfSharp?branchName=master&jobName=Windows)](https://dev.azure.com/IFSharp/IFSharp/_build/latest?definitionId=1&branchName=master) |

# F# and Jupyter

This implements F# for [Jupyter](http://jupyter.org/) notebooks. View the [Feature Notebook](FSharp_Jupyter_Notebooks.ipynb) for some of the features that are included.

# Getting Started

## Docker

To run using a Docker container on Linux/macOS:

    docker run -v $PWD:/notebooks -p 8888:8888 fsprojects/ifsharp
    
or with PowerShell on Windows:

    docker run -v ${PWD}:/notebooks -p 8888:8888 fsprojects/ifsharp

The container exposes your current directory as a volume called `notebooks` where the files get saved.
Open with 

    http://localhost:8888

and enter the token printed by the docker container startup, or set up a password.

Notes:

* Add `-p <your_port>:8888` if a different port mapping is required.

* If using Windows you must enable file sharing for docker on that drive.

## Azure Notebooks

You can use Jupyter F# Notebooks with free server-side execution at [Azure Notebooks](https://notebooks.azure.com/).
If you select "Show me some samples", then there is an "Introduction to F#" which guides you through the language
and its use in Jupyter.

## Windows Local Installation and Use

1. Download [Anaconda](https://www.anaconda.com/download/) for Python 3.6

2. Launch Anaconda3-4.4.0-Windows-x86_64.exe (or later exe should work, file an issue if you have issues)
   Click through the installation wizard, choosing the given install location. At the 'advanced installation options' screen shown below, select "Add Anaconda to my PATH environment variable". The installer warns against this step, as it can clash with previously installed software, however it's currently essential for running IfSharp. Now install.

   This should also install Jupyter: you may check this by entering 'jupyter notebook' into the Anaconda console window. If Jupyter does not launch (it should launch in the browser), install using 'pip install jupyter', or by following [Jupyter](http://jupyter.readthedocs.io/en/latest/install.html) instructions.

   ![Installation screenshot](/docs/files/img/anaconda-installation.png)

3. Download [the latest IfSharp zip release](https://github.com/fsprojects/IfSharp/releases/)

4. Run IfSharp.exe (IfSharp application icon).

Jupyter will start and a notebook with F# can be selected. This can be run via "jupyter notebook" in future

#### Troubleshooting

If the launch fails in the console window, check that the Anaconda version used is currently added to the path. If not, uninstalling Anaconda and reinstalling using instructions 1-

## macOS Local Installation and Use

1. Install [Jupyter](http://jupyter.readthedocs.org/en/latest/install.html) via pip or Anaconda etc.

2. Install [Mono](http://www.mono-project.com/download/) (tested Mono 5.10.1.47)

3. Download [the latest IfSharp zip release](https://github.com/fsprojects/IfSharp/releases/)

4. Unzip the release then run `mono ifsharp.exe`

Jupyter will start and a notebook with F# can be selected. This can be run via "jupyter notebook" in future

## Linux Local Installation and Use

1. Install [Jupyter](http://jupyter.readthedocs.org/en/latest/install.html) via pip or Anaconda etc.

2. Install [Mono](http://www.mono-project.com/docs/getting-started/install/linux/) (Untested, suggest mono 5.10) and F# (tested 4.1).

3. Download [the latest IfSharp zip release](https://github.com/fsprojects/IfSharp/releases/)

4. Unzip the release then run `mono ifsharp.exe` (this sets up the Jupyter kernel files in `~/.local/share/jupyter/kernels/ifsharp/`) 

Jupyter will start and a notebook with F# can be selected. This can be run via "jupyter notebook" in future

## Linux Local Installation (HDInsights)

1. Follow instructions to [install or update Mono](https://docs.microsoft.com/en-us/azure/hdinsight/hdinsight-hadoop-install-mono) on HDInsights.

2. [SSH into the HDInsights cluster](https://docs.microsoft.com/en-us/azure/hdinsight/hdinsight-hadoop-linux-use-ssh-unix).

3. Download [the latest IfSharp zip release](https://github.com/fsprojects/IfSharp/releases/)

```
# create ifsharp folder under /tmp
mkdir ifsharp
cd ifsharp
wget https://github.com/fsprojects/IfSharp/releases/download/v3.0.1/IfSharp.v3.0.1.zip
unzip IfSharp.v3.0.1.zip
chmod +x ifsharp.exe
```
4. From the [Azure portal](https://portal.azure.com/), open your cluster.  See [List and show clusters](../hdinsight-administer-use-portal-linux.md#list-and-show-clusters) for the instructions. The cluster is opened in a new portal blade.
5. From the **Quick links** section, click **Cluster dashboards** to open the **Cluster dashboards** blade.  If you don't see **Quick Links**, click **Overview** from the left menu on the blade.
6. Click **Jupyter Notebook**. If prompted, enter the admin credentials for the cluster.

   > [!NOTE]
   > You may also reach the Jupyter notebook on Spark cluster by opening the following URL in your browser. Replace **CLUSTERNAME** with the name of your cluster:
   >
   > `https://CLUSTERNAME.azurehdinsight.net/jupyter`
   >
7. Click **New**, and then click **Terminal**.
8. In the terminal window `cd` into the `/tmp/ifsharp/` folder and using mono, run the installer:

```
cd /tmp/ifsharp
mono ifsharp.exe
```
9. Back on the Jupyter homepage, click **New** and you will now see the F# kernel installed.

# Screenshots

## Intellisense
![Intellisense Example #1](/docs/files/img/intellisense-1.png?raw=true "Intellisense Example #1")
***

![Intellisense Example #2](docs/files/img/intellisense-2.png?raw=true "Intellisense Example #2")

## Integrated NuGet (via Paket)
![NuGet Example](docs/files/img/integratedNuget.png?raw=true "NuGet example")

## Inline Error Messages
![Inline Error Message](docs/files/img/errors-1.png?raw=true "Inline error message")


# Development Guide

## Building Docker image locally

Build the container with: 

    docker build -t fsprojects/ifsharp:local .

# Compatibility

IfSharp supports Jupyter 5.7.7 and works with both Python 2.X and Python 3.X

If you need IPython 1.x or 2.x support please see the archived https://github.com/fsprojects/IfSharp/tree/ipython-archive

# Automatic Installation

Previous releases for the IPython notebook are here: [release repository](https://github.com/fsprojects/IfSharp/releases).
Automatic installs for Jupyter may be provided in the future. Contributions are welcome!
