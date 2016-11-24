using System;
using IfSharp.Kernel;
using System.Globalization;
using System.IO;
using Trinet.Core.IO.Ntfs;

namespace IfSharpConsole
{
    class Program
    {
        private static void ClearAlternativeStreamsWindows()
        {
            var path = System.Reflection.Assembly.GetEntryAssembly().Location;
            if (path != null)
            {
                foreach (var filePath in new FileInfo(path).Directory.GetFileSystemInfos())
                {
                    filePath.DeleteAlternateDataStream("Zone.Identifier");
                }
            }
        }

        static void Main(string[] args)
        {
            //On Windows if you download our zip releases you files will be marked as from the Internet
            //Depending on how you extract the files this marker may be left, this will break Paket, so clear it.
            if (Environment.OSVersion.Platform != PlatformID.Unix && Environment.OSVersion.Platform != PlatformID.MacOSX)
            {
                ClearAlternativeStreamsWindows();
            }
            CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
            CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;
            App.Start(args);
        }
    }
}
