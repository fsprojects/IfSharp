# IfSharp, Jupyter and F# Azure Notebooks

This is the F# implementation for [Jupyter](http://jupyter.org/). View the [Feature Notebook](FSharp_Jupyter_Notebooks.ipynb) for some of the features that are included.

You can use Jupyter F# Notebooks for free (with free server-side execution) at [Azure Notebooks](https://notebooks.azure.com/). If you select "Show me some samples", then there is an "Introduction to F#" which guides you through the language and its use in Jupyter.

Build status: [![Build status](https://ci.appveyor.com/api/projects/status/7da6fkdqqm1g3cri/branch/master?svg=true)](https://ci.appveyor.com/project/cgravill/ifsharp) (master/Windows) [![Build Status](https://travis-ci.org/fsprojects/IfSharp.svg?branch=master)](https://travis-ci.org/fsprojects/IfSharp) (master/Travis)

# Compatibility
IfSharp supports Jupyter 4.0-5.2 and works with both Python 2.X and Python 3.X

If you need IPython 1.x or 2.x support please see the archived https://github.com/fsprojects/IfSharp/tree/ipython-archive

# Automatic Installation
Previous releases for the IPython notebook are here: [release repository](https://github.com/fsprojects/IfSharp/releases).
Automatic installs for Jupyter may be provided in the future. Contributions are welcome!

# Running inside a Docker container

## From DockerHub

IfSharp published all versions since v. 3.0.0 on DockerHub. Use it with

`docker run -d -v your_local_notebooks_dir:/notebooks -p your_port:8888 ifsharp/ifsharp:<tag>`

## Build image locally

There is a Docker file for running the F# kernel v. 3.0.0 in a container.
Build the container with:

`docker build -t ifsharp/ifsharp:local .`

Run it with:

`docker run -d -v your_local_notebooks_dir:/notebooks -p your_port:8888 ifsharp/ifsharp:local`

The container exposes a volume called `notebooks` where the files get saved. On Linux, connect to the notebook on `http://localhost:your_port` and, on Windows, use `http://your_docker_machine:your_port`.

# Manual Installation (Windows)
1. Download [Anaconda](https://www.anaconda.com/download/) for Python 3.6
2. Launch Anaconda3-4.4.0-Windows-x86_64.exe (or later exe should work, file an issue if you have issues)
   Click through the installation wizard, choosing the given install location. At the 'advanced installation options' screen shown below, select "Add Anaconda to my PATH environment variable". The installer warns against this step, as it can clash with previously installed software, however it's currently essential for running IfSharp. Now install.

This should also install Jupyter: you may check this by entering 'jupyter notebook' into the Anaconda console window. If Jupyter does not launch (it should launch in the browser), install using 'pip install jupyter', or by following [Jupyter](http://jupyter.readthedocs.io/en/latest/install.html) instructions.

![Installation screenshot](/docs/files/img/anaconda-installation.png)
***

3. Download current zip release of IfSharp [v3.0.1](https://github.com/fsprojects/IfSharp/releases/download/v3.0.1/IfSharp.v3.0.1.zip)
4. Run IfSharp.exe (IfSharp application icon).

Jupyter will start and a notebook with F# can be selected. This can be run via "jupyter notebook" in future

# Troubleshooting
If the launch fails in the console window, check that the Anaconda version used is currently added to the path. If not, uninstalling Anaconda and reinstalling using instructions 1-

# Manual Installation (Mac)
1. Install [Jupyter](http://jupyter.readthedocs.org/en/latest/install.html) via pip or Anaconda etc.
2. Install [Mono](http://www.mono-project.com/download/) (tested Mono 5.10.1.47)
3. Download current IfSharp zip release [v3.0.1](https://github.com/fsprojects/IfSharp/releases/download/v3.0.1/IfSharp.v3.0.1.zip)
4. Unzip the release then run `mono ifsharp.exe`

Jupyter will start and a notebook with F# can be selected. This can be run via "jupyter notebook" in future

# Manual Installation (Linux)
1. Install [Jupyter](http://jupyter.readthedocs.org/en/latest/install.html) via pip or Anaconda etc.
2. Install [Mono](http://www.mono-project.com/docs/getting-started/install/linux/) (Untested, suggest mono 5.10) and F# (tested 4.1).
3. Download the current IfSharp zip release [v3.0.1](https://github.com/fsprojects/IfSharp/releases/download/v3.0.1/IfSharp.v3.0.1.zip)
4. Unzip the release then run `mono ifsharp.exe` (this sets up the Jupyter kernel files in `~/.local/share/jupyter/kernels/ifsharp/`) 

Jupyter will start and a notebook with F# can be selected. This can be run via "jupyter notebook" in future

# Manual Installation (Linux - HDInsights)
1. Follow instructions to [install or update Mono](https://docs.microsoft.com/en-us/azure/hdinsight/hdinsight-hadoop-install-mono) on HDInsights.
2. [SSH into the HDInsights cluster](https://docs.microsoft.com/en-us/azure/hdinsight/hdinsight-hadoop-linux-use-ssh-unix).
3. Download the current Ifsharp zip release [v3.0.1](https://github.com/fsprojects/IfSharp/releases/download/v3.0.1/IfSharp.v3.0.1.zip) with the following commands: 

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


# Screens
## Intellisense
![Intellisense Example #1](/docs/files/img/intellisense-1.png?raw=true "Intellisense Example #1")
***

![Intellisense Example #2](docs/files/img/intellisense-2.png?raw=true "Intellisense Example #2")
***

## Integrated NuGet (via Paket)
![NuGet Example](docs/files/img/integratedNuget.png?raw=true "NuGet example")

## Inline Error Messages
![Inline Error Message](docs/files/img/errors-1.png?raw=true "Inline error message")
