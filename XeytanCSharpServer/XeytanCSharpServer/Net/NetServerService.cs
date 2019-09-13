using NetLib.Packets;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using XeytanCSharpServer.Concurrent;
using XeytanCSharpServer.Models;
using Action = XeytanCSharpServer.Concurrent.Action;

namespace XeytanCSharpServer.Net
{
    class NetServerService
    {
        public bool Running { get; set; }
        public AppNetDoubleQueueThreadChannel AppNetChannel { get; set; }
        public Dictionary<int, NetClientService> Clients { get; set; } = new Dictionary<int, NetClientService>();

        private ReaderWriterLock ClientsLock { get; }

        public NetServerService(AppNetDoubleQueueThreadChannel channel)
        {
            ClientsLock = new ReaderWriterLock();
            AppNetChannel = channel;
        }

        public void StartAsync()
        {
            Socket serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            serverSocket.Bind(new IPEndPoint(IPAddress.Loopback, 3002));
            serverSocket.Listen(0);

            Running = true;

            AcceptOneClient(serverSocket);
            new Thread(ListenAppEvents).Start();
        }

        private void ListenAppEvents()
        {
            while (Running)
            {
                AppEvent appEvent = AppNetChannel.TakeFromApp();
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
                                    List<Client> clients = GetClients();
                                    appEvent.Data = clients;
                                    AppNetChannel.SubmitToApp(appEvent);
                                }

                                break;
                            }

                            case Subject.Interaction:
                            {
                                if (action == Action.Start)
                                {
                                    Client client = GetClientById((int) data);
                                    AppNetChannel.SubmitToApp(new ClientAppEvent(client)
                                    {
                                        Subject = Subject.Interaction, Action = Action.Start
                                    });
                                }

                                break;
                            }
                        }

                        break;
                    }

                    case Target.Client:
                    {
                        ClientAppEvent clientAppEvent = (ClientAppEvent) appEvent;
                        Client client = clientAppEvent.Client;
                        switch (subject)
                        {
                            case Subject.FileSystem:
                            {
                                if (action == Action.Start)
                                {
                                    if (data == null)
                                        FetchFileSystemDrives(client);
                                    else
                                        FetchDirEntries(client, (string) data);
                                }

                                break;
                            }

                            case Subject.Information:
                            {
                                if (action == Action.Start)
                                {
                                    FetchSystemInfo(client);
                                }

                                break;
                            }

                            case Subject.Process:
                            {
                                if (action == Action.Start)
                                {
                                    FetchProcessList(client);
                                }

                                break;
                            }

                            case Subject.Shell:
                            {
                                if (action == Action.Start)
                                {
                                    SendPacket(client, new PacketShell());
                                }
                                else if (action == Action.Push)
                                {
                                    SendPacket(client, new PacketShell()
                                    {
                                        Command = (string) data
                                    });
                                }

                                break;
                            }

                            case Subject.Desktop:
                            {
                                if (action == Action.Start)
                                {
                                    SendPacket(client, new PacketDesktop
                                    {
                                        DesktopAction = DesktopAction.Start
                                    });
                                }
                                else if (action == Action.Stop)
                                {
                                    SendPacket(client, new PacketDesktop
                                    {
                                        DesktopAction = DesktopAction.Stop
                                    });
                                }

                                break;
                            }
                        }

                        break;
                    }
                }
            }
        }

        private void AcceptOneClient(Socket serverSocket)
        {
            serverSocket.BeginAccept(OnConnectionPending, serverSocket);
        }

        private void OnConnectionPending(IAsyncResult result)
        {
            Socket serverSocket = ((Socket) result.AsyncState);
            Socket clientSocket = serverSocket.EndAccept(result);


            AcceptOneClient(serverSocket);

            NetClientService netClientService = new NetClientService(this, clientSocket);
            bool added = false;
            try
            {
                int clientId = (int) clientSocket.Handle;
                ClientsLock.AcquireWriterLock(15 * 1000); /* We could also use Timeout.Infinite */
                Clients.Add(clientId, netClientService);
                ClientsLock.ReleaseWriterLock();
                added = true;
            }
            catch (ApplicationException exception)
            {
                Console.WriteLine(
                    "======================================================================================");
                Console.WriteLine(
                    "Deadlock detected? 30 seconds waiting for ClientsClock in NetServerService::Start()");
                Console.WriteLine(
                    "======================================================================================");
            }

            if (added)
                netClientService.InteractAsync();
            else
                netClientService.ShutdownConnection();
        }

        public void OnPacketReceived(Client client, Packet packet)
        {
            Trace.WriteLine("Server::OnPacketReceived()");
            switch (packet.PacketType)
            {
                case PacketType.Presentation:

                    AppNetChannel.SubmitToApp(new ClientAppEvent(client)
                    {
                        Subject = Subject.Connection, Action = Action.Started
                    });
                    break;
                case PacketType.Information:
                {
                    PacketSystemInformation packetInfo = (PacketSystemInformation) packet;

                    AppNetChannel.SubmitToApp(new ClientAppEvent(client)
                    {
                        Subject = Subject.Information,
                        Action = Action.Fetched,
                        Data = packetInfo.SystemInfo
                    });
                    break;
                }

                case PacketType.FileSystem:
                {
                    PacketFileSystem packetFs = (PacketFileSystem) packet;
                    AppNetChannel.SubmitToApp(new ClientAppEvent(client)
                    {
                        Subject = Subject.FileSystem,
                        Action = Action.Fetched,
                        Data = packetFs
                    });

                    break;
                }

                case PacketType.Process:
                {
                    AppNetChannel.SubmitToApp(new ClientAppEvent(client)
                    {
                        Subject = Subject.Process,
                        Action = Action.Fetched,
                        Data = ((PacketProcess) packet).Processes
                    });
                    break;
                }

                case PacketType.Shell:
                {
                    Dictionary<string, string> data = new Dictionary<string, string>();
                    data["command"] = ((PacketShell) packet).Command;
                    data["output"] = ((PacketShell) packet).Output;

                    if (((PacketShell) packet).Action == ServiceAction.Stop)
                    {
                        AppNetChannel.SubmitToApp(new ClientAppEvent(client)
                        {
                            Subject = Subject.Shell,
                            Action = Action.Stop
                        });
                    }
                    else
                    {
                        AppNetChannel.SubmitToApp(new ClientAppEvent(client)
                        {
                            Subject = Subject.Shell,
                            Action = Action.Fetched,
                            Data = data
                        });
                    }

                    break;
                }
                case PacketType.Desktop:
                {
                    ClientAppEvent clientEvent =
                        new ClientAppEvent(client) {Subject = Subject.Desktop};
                    if (((PacketDesktop) packet).DesktopAction == DesktopAction.Push)
                    {
                        clientEvent.Action = Action.Fetched;
                        clientEvent.Data = ((PacketDesktop) packet).ImageData;
                    }
                    else if (clientEvent.Action == Action.Fetched)
                        clientEvent.Action = Action.Stop;


                    AppNetChannel.SubmitToApp(clientEvent);
                    break;
                }

                default:
                    Trace.WriteLine("Unhandled packet type");
                    break;
            }
        }

        public void OnClientDisconnected(Client client, Exception exception)
        {
            try
            {
                ClientsLock.AcquireWriterLock(15 * 1000);
                Clients.Remove(client.Id);
            }
            catch (ApplicationException applicationException)
            {
                Console.WriteLine("=============================================================================");
                Console.WriteLine("NetServerService::SendPacketsThreadFunc was not able to lock in 15 sec");
                Console.WriteLine("=============================================================================");
            }
            finally
            {
                ClientsLock.ReleaseWriterLock();
            }

            AppNetChannel.SubmitToApp(new ClientAppEvent(client)
            {
                Subject = Subject.Connection, Action = Action.Stop
            });
        }

        public void FetchSystemInfo(Client client)
        {
            SendPacket(client, new PacketSystemInformation());
        }

        public void FetchFileSystemDrives(Client client)
        {
            SendPacket(client, new PacketFileSystem()
            {
                FsFocus = PacketFileSystem.FileSystemFocus.Roots
            });
        }

        public void FetchDirEntries(Client client, string basePath)
        {
            SendPacket(client, new PacketFileSystem()
            {
                FsFocus = PacketFileSystem.FileSystemFocus.DirectoryEntries,
                BasePath = basePath
            });
        }

        public void FetchProcessList(Client client)
        {
            SendPacket(client, new PacketProcess());
        }

        private void SendPacket(Client client, Packet packet)
        {
            try
            {
                ClientsLock.AcquireReaderLock(15 * 1000); /* We could also use Timeout.Infinite */
                NetClientService netClient = Clients[client.Id];
                netClient?.SendPacket(packet);
            }
            catch (ApplicationException ex)
            {
                Console.WriteLine("=============================================================================");
                Console.WriteLine("NetServerService::SendPacket was not able to lock in 15 sec");
                Console.WriteLine("=============================================================================");
            }
            finally
            {
                ClientsLock.ReleaseReaderLock();
            }
        }


        public Client GetClientById(int clientId)
        {
            try
            {
                ClientsLock.AcquireReaderLock(15 * 1000);
                if (Clients.TryGetValue(clientId, out var client))
                    return client.Client;
            }
            catch (ApplicationException exception)
            {
                Console.WriteLine("=============================================================================");
                Console.WriteLine("NetServerService::GetClientById was not able to lock in 15 sec");
                Console.WriteLine("=============================================================================");
            }
            finally
            {
                ClientsLock.ReleaseReaderLock();
            }

            return null;
        }

        public List<Client> GetClients()
        {
            try
            {
                ClientsLock.AcquireReaderLock(15 * 1000);
                return new List<Client>(Clients.Values.Select(netClient => netClient.Client));
            }
            catch (ApplicationException exception)
            {
            }
            finally
            {
                ClientsLock.ReleaseReaderLock();
            }

            return new List<Client>();
        }
    }
}