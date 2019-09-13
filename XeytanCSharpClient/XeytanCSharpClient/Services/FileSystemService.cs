using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using NetLib.Models;

namespace XeytanCSharpClient.Services
{
    class FileSystemService
    {
        public static List<NetLib.Models.FileInfo> GetDirectoryPaths(string path, out string error)
        {
            try
            {
                FileAttributes attributes = File.GetAttributes(path);
                List<NetLib.Models.FileInfo> files = new List<NetLib.Models.FileInfo>();
                if ((attributes & FileAttributes.Directory) == FileAttributes.Directory)
                {
                    string[] dirEntries = Directory.GetFileSystemEntries(path);

                    foreach (string dirEntry in dirEntries)
                    {
                        try
                        {
                            FileAttributes entryAttributes = File.GetAttributes(dirEntry);
                            long fileSize;
                            if (!entryAttributes.HasFlag(FileAttributes.Directory))
                            {
                                fileSize = new System.IO.FileInfo(dirEntry).Length;
                            }
                            else
                            {
                                fileSize = 0;
                            }

                            files.Add(new NetLib.Models.FileInfo
                            {
                                FilePath = dirEntry,
                                FileSize = fileSize,
                                FileAttributes = entryAttributes,
                                CreationTime = File.GetCreationTimeUtc(dirEntry),
                                LastAccessTime = File.GetLastAccessTimeUtc(dirEntry),
                                LastWriteTime = File.GetLastWriteTimeUtc(dirEntry),
                            });
                        }
                        catch (FileNotFoundException exception)
                        {
                            Debug.WriteLine("Error trying to retrieve info on {0}\n{1}",
                                path, exception.ToString());
                        }
                    }

                    error = null;
                    return files;
                }
                else
                {
                    error = string.Format("{0} Is not a directory", path);
                    Debug.WriteLine(error);
                }
            }
            catch (Exception exception)
            {
                error = string.Format("Error trying to retrieve attributes on {0}\n{1}",
                    path, exception.ToString());
                Debug.WriteLine(error);
            }

            return null;
        }

        public static List<DiskDriveInfo> GetRoots()
        {
            DriveInfo[] drives = DriveInfo.GetDrives();
            List<DiskDriveInfo> diskDrives = new List<DiskDriveInfo>();
            foreach (DriveInfo driveInfo in drives)
            {
                diskDrives.Add(new DiskDriveInfo
                {
                    Name = driveInfo.Name,
                    DriveFormat = driveInfo.DriveFormat,
                    Label = driveInfo.VolumeLabel
                });
            }

            return diskDrives;
        }
    }
}