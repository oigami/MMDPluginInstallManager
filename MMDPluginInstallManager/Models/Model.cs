using System;
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

        public Dictionary<string, MMDPluginPackage> MMDInstalledPluginPackage { get; set; }

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

        public Dictionary<string, DownloadPluginData>.ValueCollection DownloadPluginList
        {
            get { return DownloadPluginDic.Values; }
        }

        public async Task<DownloadPluginData> InstallPlugin(string zipPath)
        {
            return await Task.Run(() =>
            {
                var hash = CreateSHA1Hash(zipPath);
                DownloadPluginData loadItem;

                if (DownloadPluginDic.TryGetValue(hash, out loadItem) == false)
                {
                    throw new ArgumentException("A hash matching the SHA1 of the zip file was not found.\n");
                }
                RaisePropertyChanged();
                var packageData = new MMDPluginPackage
                {
                    Version = loadItem.NewVersion
                };
                using (var zipArchive = ZipFile.OpenRead(zipPath))
                {
                    foreach (var entry in zipArchive.Entries)
                    {
                        var filename = entry.FullName.Replace('/', '\\');
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
                            loadItem.ReadMeFilePath = path;
                        }
                    }
                }
                loadItem.NowVersion = loadItem.NewVersion;
                packageData.ReadMeFilePath = loadItem.ReadMeFilePath;
                MMDInstalledPluginPackage[loadItem.Title] = packageData;
                File.WriteAllText(MMDPluginPackageJsonFilename, JsonConvert.SerializeObject(MMDInstalledPluginPackage));
                return loadItem;
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
            var hash = CreateSHA1Hash(installPath);
            if (hash != "7dbf4f27d6dd14ce77e2e659a69886e3b6739b56")
            {
                throw new InvalidOperationException(
                    "The MikuMikuDance.exe is wrong.\nThis program is only supported for 'MMD ver9.26 x64'.");
            }
            _installPath = Directory.GetParent(installPath).FullName;
        }

        public void LoadPluginData()
        {
            var text = File.ReadAllText("package_list.json");
            try
            {
                var mmdpluginPackageJsonText = File.ReadAllText(MMDPluginPackageJsonFilename);
                MMDInstalledPluginPackage =
                    JsonConvert.DeserializeObject<Dictionary<string, MMDPluginPackage>>(mmdpluginPackageJsonText);
            }
            catch (Exception)
            {
                MMDInstalledPluginPackage = new Dictionary<string, MMDPluginPackage>();
            }

            double mmdPluginVersion = -1;
            var jsonData = JsonConvert.DeserializeObject<MMDPluginData[]>(text);
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
                    NewVersion = item.Version,
                    NowVersion = package?.Version ?? -1,
                    Title = item.Title,
                    ReadMeFilePath = package?.ReadMeFilePath
                });
                if (item.Title == "MMDPlugin")
                {
                    mmdPluginVersion = item.Version;
                }
            }
            RaisePropertyChanged(nameof(DownloadPluginList));


            MMDPluginPackage mmdPluginPackage;
            if (MMDInstalledPluginPackage.TryGetValue("MMDPlugin", out mmdPluginPackage))
            {
                if (Math.Abs(mmdPluginPackage.Version - mmdPluginVersion) < 1e-5)
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
            public DownloadPluginData(string[][] installDir, string readMe)
            {
                _installDir = installDir;
                foreach (var t in _installDir)
                {
                    t[0] = t[0].Replace('/', '\\');
                }
                _readme = readMe;
            }

            public float NewVersion { get; set; }

            public float NowVersion { get; set; }

            public string Title { get; set; }

            public string Url { get; set; }

            public string ReadMeFilePath { get; set; }

            private readonly string[][] _installDir;

            private readonly string _readme;

            public bool TryGetInstallDir(string filename, out string path)
            {
                foreach (var item in _installDir)
                {
                    if (filename.StartsWith(item[0], StringComparison.OrdinalIgnoreCase))
                    {
                        path = item[1];
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
