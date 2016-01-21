# IfSharp
F# implementation for [iPython](http://ipython.org). View the [Feature Notebook](http://nbviewer.ipython.org/github/fsprojects/IfSharp/blob/master/Feature%20Notebook.ipynb) for some of the features that are included.
For more information view the [documentation](http://fsprojects.github.io/IfSharp/). IfSharp is 64-bit *ONLY*.

# Compatibility
IfSharp works with iPython Notebook 1.x and 2.x 

# Automatic Installation
See our [release repository](releases). Also, [installation documentation](http://fsprojects.github.io/IfSharp/installation.html).

# Manual Installation
1. Install [Anaconda](http://continuum.io/downloads)
2. Install [IPython](http://ipython.org/install.html)
3. Run: "ipython profile create ifsharp" in your user directory
4. Open the iF# solution file, restore nuget packages, and compile it
5. Copy the files from IfSharp\ipython-profile to the iFSharp profile directory
6. Open up the copied "ipython_config.py" file and replace "%s" with the path of your compiled ifsharp executable. E.g. "C:\\git\\ifsharp\\bin\\Release\\ifsharp.exe" 
7. Run: "ipython notebook --profile ifsharp" to launch the notebook process with the F# kernel.

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
