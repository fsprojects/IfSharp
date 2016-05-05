// include directory, this will be replaced by the kernel
#I "{0}"

// load base dlls
#r "IfSharp.Kernel.dll"
#r "System.Data.dll"
#r "System.Windows.Forms.DataVisualization.dll"
//#r "FSharp.Data.TypeProviders.dll" //Can be accessed by #N "FSharp.Data.TypeProviders" instead
#r "NetMQ.dll"

// open the global functions and methods
open IfSharp.Kernel
open IfSharp.Kernel.Globals
