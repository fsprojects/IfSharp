# IfSharp
F# implementation for [iPython](http://ipython.org). View the [Feature Notebook](http://nbviewer.ipython.org/github/BayardRock/IfSharp/blob/master/Feature%20Notebook.ipynb) for some of the features that are included.
For more information view the [documenation](http://bayardrock.github.io/IfSharp/).

# Compatibility
IfSharp works with iPython Notebook 1.x and 2.x 

# Automatic Installation
See our [release repository](https://github.com/BayardRock/IfSharp/releases). 

# Manual Installation
1. Install [Anaconda](http://continuum.io/downloads)
2. Install [IPython](http://ipython.org/install.html)
3. Run: "ipython profile create ifsharp" in your user directory
4. Open the iF# solution file, restore nuget packages, and compile it
5. Copy the files from IfSharp\ipython-profile to the iFSharp profile directory
6. Open up the copied "ipython_config.py" file and replace "%s" with the path of your compiled ifsharp executable. E.g. "C:\\git\\ifsharp\\bin\\Release\\ifsharp.exe" 
7. Run: "ipython notebook --profile ifsharp" to launch the notebook process with the F# kernel.

# Screens
![Intellisense Example #1](https://raw.github.com/BayardRock/IfSharp/master/docs/files/img/intellisense-1.png "Intellisense Example #1")

![Intellisense Example #2](https://raw.github.com/BayardRock/IfSharp/master/docs/files/img/intellisense-2.png "Intellisense Example #2")

![Intellisense Example #3 With Chart](https://raw.github.com/BayardRock/IfSharp/master/docs/files/img/intellisense-3.png "Intellisense Example #3 With Chart")
