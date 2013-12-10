# IfSharp
F# implementation for [iPython](http://ipython.org). View the [Feature Notebook](http://nbviewer.ipython.org/github/BayardRock/IfSharp/blob/master/Feature%20Notebook.ipynb) for some of the features that are included.

# Getting Started
1) Install [Anaconda](http://continuum.io/downloads)
2) Install [IPython](http://ipython.org/install.html)
3) Run: "ipython profile create ifsharp" in the iPython directory
4) Open the iF# solution file, restore nuget packages, and compile it
5) Copy the files from IfSharp\ipython-profile to the iFSharp profile directory
6) Open up the copied "ipython_config.py" file and change "..\\bin\\ifsharp.exe" to the path of your compiled ifsharp executable.
7) Run: "ipython notebook --profile ifsharp" to launch the notebook process with the F# kernel.