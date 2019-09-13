using System;
using System.Collections;
using NetLib.Models;
using XeytanCSharpServer.Models;
using C = System.Console;

namespace XeytanCSharpServer.Ui.Console.Views
{
    class SystemInfoView : ClientView
    {
        public void PrintBanner()
        {
            throw new NotImplementedException();
        }

        protected override string GetBannerLabel()
        {
            return "sysinfo";
        }

        public override string Loop()
        {
            throw new NotImplementedException();
        }

        public void SetActiveClient(Client client)
        {
            throw new NotImplementedException();
        }

        public static void PrintSystemInfo(Client client, SystemInfo systemInformation)
        {
            C.WriteLine("System Information for {0}", client.PcName);
            C.WriteLine("\tOperating System: {0}", client.OperatingSystem);
            C.WriteLine("\tUserName: {0}", client.UserName);
            C.WriteLine("\tRemote Address: {0}:{1}", client.RemoteIpAddress, client.RemotePort);
            C.WriteLine("\t.Net Version: {0}", client.DotNetVersion);

            C.WriteLine("\tEnvironment variables");
            foreach (DictionaryEntry environmentVariable in systemInformation.EnvironmentVariables)
            {
                C.WriteLine("\t\t{0}: {1}", environmentVariable.Key, environmentVariable.Value);
            }
        }
    }
}