﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
using AttackSurfaceAnalyzer.Objects;
using AttackSurfaceAnalyzer.Utils;
using Mono.Unix;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;

namespace AttackSurfaceAnalyzer.Collectors
{
    /// <summary>
    /// Collects Filesystem Data from the local file system.
    /// </summary>
    public class FileSystemCollector : BaseCollector
    {
        private readonly HashSet<string> roots;

        private bool INCLUDE_CONTENT_HASH = false;

        private bool downloadCloud;
        private bool examineCertificates;

        public FileSystemCollector(string runId, bool enableHashing = false, string directories = "", bool downloadCloud = false, bool examineCertificates = false)
        {
            this.runId = runId;
            this.downloadCloud = downloadCloud;
            this.examineCertificates = examineCertificates;

            roots = new HashSet<string>();
            INCLUDE_CONTENT_HASH = enableHashing;

            if (directories != null && !directories.Equals(""))
            {
                foreach (string path in directories.Split(','))
                {
                    AddRoot(path);
                }
            }

        }

        /// <summary>
        /// Add a root to be collected
        /// </summary>
        /// <param name="root">The path to scan</param>
        public void AddRoot(string root)
        {
            roots.Add(root);
        }

        public override bool CanRunOnPlatform()
        {
            return RuntimeInformation.IsOSPlatform(OSPlatform.Windows) || RuntimeInformation.IsOSPlatform(OSPlatform.Linux) || RuntimeInformation.IsOSPlatform(OSPlatform.OSX);
        }

        public override void Execute()
        {
            if (!CanRunOnPlatform())
            {
                return;
            }

            Start();

            if (roots == null || !roots.Any())
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    foreach (var driveInfo in DriveInfo.GetDrives())
                    {
                        if (driveInfo.IsReady && driveInfo.DriveType == DriveType.Fixed)
                        {
                            roots.Add(driveInfo.Name);
                        }
                    }
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    roots.Add("/");   // @TODO Improve this
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    roots.Add("/"); // @TODO Improve this
                }
            }

            foreach (var root in roots)
            {
                Log.Information("{0} root {1}", Strings.Get("Scanning"), root.ToString());
                //Ensure the transaction is started to prevent collisions on the multithreaded code ahead
                _ = DatabaseManager.Transaction;
                try
                {
                    var fileInfoEnumerable = DirectoryWalker.WalkDirectory(root);
                    Parallel.ForEach(fileInfoEnumerable,
                                    (fileInfo =>
                    {
                        try
                        {
                            FileSystemObject obj = FileSystemInfoToFileSystemObject(fileInfo, downloadCloud, INCLUDE_CONTENT_HASH);

                            if (obj != null)
                            {
                                DatabaseManager.Write(obj, runId);
                                if (examineCertificates &&
                                    fileInfo.FullName.EndsWith(".cer", StringComparison.CurrentCulture) ||
                                    fileInfo.FullName.EndsWith(".der", StringComparison.CurrentCulture) ||
                                    fileInfo.FullName.EndsWith(".p7b", StringComparison.CurrentCulture))
                                {
                                    var certificate = X509Certificate.CreateFromCertFile(fileInfo.FullName);
                                    var certObj = new CertificateObject()
                                    {
                                        StoreLocation = fileInfo.FullName,
                                        StoreName = "Disk",
                                        CertificateHashString = certificate.GetCertHashString(),
                                        Subject = certificate.Subject,
                                        Pkcs7 = certificate.Export(X509ContentType.Cert).ToString()
                                    };
                                    DatabaseManager.Write(certObj, runId);
                                }
                            }
                        }
                        catch (Exception e)
                        {
                            Logger.DebugException(e);
                        }
                    }));
                }
                catch (Exception e)
                {
                    Log.Warning(e, "Error collecting file system information: {0}", e.Message);
                }

                DatabaseManager.Commit();

            }

            Stop();

        }

        /// <summary>
        /// Converts a FileSystemInfo into a FileSystemObject by reading in data about the file
        /// </summary>
        /// <param name="fileInfo">A reference to a file on disk</param>
        /// <param name="downloadCloud">If the file is hosted in the cloud, the user has the option to include cloud files or not.</param>
        /// <param name="INCLUDE_CONTENT_HASH">If we should generate a hash of the file.</param>
        /// <returns></returns>
        public static FileSystemObject FileSystemInfoToFileSystemObject(FileSystemInfo fileInfo, bool downloadCloud = false, bool INCLUDE_CONTENT_HASH = false)
        {
            FileSystemObject obj = null;

            try
            {
                if (fileInfo is DirectoryInfo)
                {
                    obj = new FileSystemObject()
                    {
                        Path = fileInfo.FullName,
                        Permissions = FileSystemUtils.GetFilePermissions(fileInfo),
                        IsDirectory = true
                    };
                    if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) || RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                    {
                        var file = new UnixFileInfo(fileInfo.FullName);
                        obj.Owner = file.OwnerUser.UserName;
                        obj.Group = file.OwnerGroup.GroupName;
                        obj.SetGid = file.IsSetGroup;
                        obj.SetUid = file.IsSetUser;
                    }
                }
                else
                {
                    obj = new FileSystemObject()
                    {
                        Path = fileInfo.FullName,
                        Permissions = FileSystemUtils.GetFilePermissions(fileInfo),
                        Size = (ulong)(fileInfo as FileInfo).Length,
                        IsDirectory = false
                    };
                    if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) || RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                    {
                        var file = new UnixFileInfo(fileInfo.FullName);
                        obj.Owner = file.OwnerUser.UserName;
                        obj.Group = file.OwnerGroup.GroupName;
                        obj.SetGid = file.IsSetGroup;
                        obj.SetUid = file.IsSetUser;
                    }

                    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    {
                        if (WindowsFileSystemUtils.IsLocal(obj.Path) || downloadCloud)
                        {
                            if (WindowsFileSystemUtils.NeedsSignature(obj.Path))
                            {
                                obj.SignatureStatus = WindowsFileSystemUtils.GetSignatureStatus(fileInfo.FullName);
                                obj.Characteristics = WindowsFileSystemUtils.GetDllCharacteristics(fileInfo.FullName);
                            }
                            else
                            {
                                obj.SignatureStatus = "Cloud";
                            }
                        }
                    }

                    if (INCLUDE_CONTENT_HASH)
                    {
                        obj.ContentHash = FileSystemUtils.GetFileHash(fileInfo);
                    }

                    if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) || RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                    {
                        if (obj.Permissions.Contains("Execute"))
                        {
                            obj.IsExecutable = true;
                        }
                    }
                    else
                    {
                        try
                        {
                            if (WindowsFileSystemUtils.IsLocal(obj.Path) || downloadCloud)
                            {
                                if (WindowsFileSystemUtils.NeedsSignature(obj.Path))
                                {
                                    obj.IsExecutable = true;
                                }
                            }
                        }
                        catch (System.UnauthorizedAccessException ex)
                        {
                            Log.Verbose(ex, "Couldn't access {0} to check if signature is needed.", fileInfo.FullName);
                        }
                    }
                }
            }
            catch (System.UnauthorizedAccessException e)
            {
                Log.Verbose(e, "Access Denied {0}", fileInfo?.FullName);
            }
            catch (System.IO.IOException e)
            {
                Log.Verbose(e, "Couldn't parse {0}", fileInfo?.FullName);
            }
            catch (Exception e)
            {
                Log.Warning(e, "Error collecting file system information: {0}", e.Message);
            }

            return obj;
        }

    }
}