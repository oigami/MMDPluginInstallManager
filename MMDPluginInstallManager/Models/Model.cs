﻿using System;
using System.Collections.ObjectModel;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Livet;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq.Expressions;
using System.Net;
using System.Runtime.CompilerServices;
using System.Windows.Markup;

namespace MMDPluginInstallManager.Models
{

    #region JsonData

    public class MMDPluginData
    {
        public string[][] InstallDir { get; set; }

        public string Readme { get; set; }

        public string SHA1Hash { get; set; }

        public string Title { get; set; }

        public string URL { get; set; }

        public float Version { get; set; }
    }

    public class MMDPluginPackage
    {
        public float Version { get; set; }

        public string ReadMeFilePath { get; set; }

        public List<string> InstalledDLL { get; set; } = new List<string>();
    }

    #endregion JsonData

    public class Model : NotificationObject
    {
        private const string MMDPluginPackageJsonFilename = "MMDPlugin.Package.json";
        /*
         * NotificationObjectはプロパティ変更通知の仕組みを実装したオブジェクトです。
         */

        private string _installPath;

        public Dictionary<string, MMDPluginPackage> MMDInstalledPluginPackage { get; private set; }

        #region IsInstalledMMDPlugin変更通知プロパティ

        private bool _IsInstalledMMDPlugin;

        public bool IsInstalledMMDPlugin
        {
            get { return _IsInstalledMMDPlugin; }
            set
            {
                if (_IsInstalledMMDPlugin == value)
                {
                    return;
                }
                _IsInstalledMMDPlugin = value;
                RaisePropertyChanged();
            }
        }

        #endregion

        public Dictionary<string, DownloadPluginData> DownloadPluginDic { get; } =
            new Dictionary<string, DownloadPluginData>();

        private string GetMMDPluginPackageJsonFilename()
        {
            var path = Path.Combine(_installPath, "plugin/");
            Directory.CreateDirectory(path);
            return Path.Combine(path, MMDPluginPackageJsonFilename);
        }

        public async Task UninstallPlugin(string mmdPluginName)
        {
            await Task.Run(() =>
            {
                var item = MMDInstalledPluginPackage[mmdPluginName];
                foreach (var i in item.InstalledDLL)
                {
                    File.Delete(i);
                }
                MMDInstalledPluginPackage.Remove(mmdPluginName);
                RaisePropertyChanged(nameof(DownloadPluginDic));
            });
        }

        public async Task<MMDPluginPackage> InstallPlugin(string zipPath)
        {
            return await Task.Run(() =>
            {
                var hash = CreateSHA1Hash(zipPath);
                DownloadPluginData loadItem;

                if (DownloadPluginDic.TryGetValue(hash, out loadItem) == false)
                {
                    throw new ArgumentException("A hash matching the SHA1 of the zip file was not found.\n");
                }

                var packageData = new MMDPluginPackage
                {
                    Version = loadItem.LatestVersion
                };
                using (var zipArchive = ZipFile.OpenRead(zipPath))
                {
                    foreach (var entry in zipArchive.Entries)
                    {
                        var filename = entry.FullName.Replace('/', '\\');
                        if (string.IsNullOrEmpty(filename) || filename[filename.Length - 1] == '\\')
                        {
                            continue;
                        }
                        string path;
                        if (!loadItem.TryGetInstallDir(filename, out path))
                        {
                            continue;
                        }

                        path = Path.Combine(_installPath, path, Path.GetFileName(filename));
                        Directory.CreateDirectory(Directory.GetParent(path).FullName);
                        entry.ExtractToFile(path, true);

                        if (Path.GetExtension(path).ToLower() == ".dll")
                        {
                            packageData.InstalledDLL.Add(path);
                        }

                        if (loadItem.IsReadMeFile(filename))
                        {
                            packageData.ReadMeFilePath = path;
                        }
                    }
                }
                MMDInstalledPluginPackage[loadItem.Title] = packageData;
                File.WriteAllText(GetMMDPluginPackageJsonFilename(),
                                  JsonConvert.SerializeObject(MMDInstalledPluginPackage));
                RaisePropertyChanged(nameof(DownloadPluginDic));
                return packageData;
            });
        }

        public void SetMMDDirectory(string installPath)
        {
            if (Path.GetFileName(installPath) == string.Empty)
            {
                installPath += @"MikuMikuDance.exe";
            }
            if (File.Exists(installPath) == false)
            {
                throw new FileNotFoundException("The MikuMikuDance.exe is not found.");
            }
            if (CreateSHA1Hash(installPath) != "7dbf4f27d6dd14ce77e2e659a69886e3b6739b56")
            {
                throw new InvalidOperationException(
                    "The MikuMikuDance.exe is wrong.\nThis program is only supported for 'MMD ver9.26 x64'.");
            }
            _installPath = Directory.GetParent(installPath).FullName;
        }

        private async Task<MMDPluginData[]> GetPackageList()
        {
            try
            {
                using (var wc = new WebClient())
                {
                    await wc.DownloadFileTaskAsync(
                                                   "https://raw.githubusercontent.com/oigami/MMDPluginInstallManager/master/MMDPluginInstallManager/package_list.json",
                                                   @"package_list.json");
                }
            }
            catch (Exception)
            {
                // ignored
            }
            var text = File.ReadAllText("package_list.json");
            return JsonConvert.DeserializeObject<MMDPluginData[]>(text);
        }

        public async Task LoadPluginData()
        {
            try
            {
                var mmdpluginPackageJsonText = File.ReadAllText(GetMMDPluginPackageJsonFilename());
                MMDInstalledPluginPackage =
                    JsonConvert.DeserializeObject<Dictionary<string, MMDPluginPackage>>(mmdpluginPackageJsonText);
            }
            catch (Exception)
            {
                MMDInstalledPluginPackage = new Dictionary<string, MMDPluginPackage>();
            }

            float mmdPluginVersion = -1;
            var jsonData = await GetPackageList();
            foreach (var item in jsonData)
            {
                MMDPluginPackage package = null;
                MMDInstalledPluginPackage.TryGetValue(item.Title, out package);
                if (string.IsNullOrEmpty(item.SHA1Hash))
                {
                    // TODO エラーログの追加
                    continue;
                }
                DownloadPluginDic.Add(item.SHA1Hash, new DownloadPluginData(item.InstallDir, item.Readme)
                {
                    Url = item.URL,
                    LatestVersion = item.Version,
                    Title = item.Title,
                });
                if (item.Title == "MMDPlugin")
                {
                    mmdPluginVersion = item.Version;
                }
            }
            RaisePropertyChanged(nameof(DownloadPluginDic));


            MMDPluginPackage mmdPluginPackage;
            if (MMDInstalledPluginPackage.TryGetValue("MMDPlugin", out mmdPluginPackage))
            {
                if (Math.Abs(mmdPluginPackage.Version - mmdPluginVersion) < 1e-5f)
                {
                    IsInstalledMMDPlugin = true;
                }
            }
        }

        private string CreateSHA1Hash(string zipPath)
        {
            using (var fs = new FileStream(zipPath, FileMode.Open))
            {
                var provider = new SHA1CryptoServiceProvider();
                var hash = provider.ComputeHash(fs);
                return BitConverter.ToString(hash).ToLower().Replace("-", "");
            }
        }

        public class DownloadPluginData
        {
            public float LatestVersion { get; set; }

            public string Title { get; set; }

            public string Url { get; set; }

            private readonly string[][] _installDir;

            private readonly string _readme;

            public DownloadPluginData(string[][] installDir, string readMe)
            {
                _installDir = installDir;
                foreach (var t in _installDir)
                {
                    t[0] = t[0].Replace('/', '\\');
                }
                _readme = readMe.Replace('/', '\\');
            }

            public bool TryGetInstallDir(string filename, out string path)
            {
                foreach (var item in _installDir)
                {
                    if (filename.StartsWith(item[0], StringComparison.OrdinalIgnoreCase))
                    {
                        path = Path.Combine(Path.GetDirectoryName(filename), item[1]);
                        return true;
                    }
                }
                path = null;
                return false;
            }

            public bool IsReadMeFile(string filename)
            {
                return string.Compare(filename, _readme, StringComparison.OrdinalIgnoreCase) == 0;
            }
        }
    }
}
