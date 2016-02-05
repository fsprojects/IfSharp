using IfSharp.Kernel;
using System.Globalization;

namespace IfSharpConsole
{
    class Program
    {
        static void Main(string[] args)
        {
            CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
            CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;
            App.Start(args);
        }
    }
}
