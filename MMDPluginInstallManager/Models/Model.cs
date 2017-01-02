using System;
using System.Collections.ObjectModel;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Livet;
using Newtonsoft.Json;

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

    #endregion JsonData

    public class Model : NotificationObject
    {
        /*
         * NotificationObjectはプロパティ変更通知の仕組みを実装したオブジェクトです。
         */

        private MMDPluginData[] _jsonData;

        public ObservableCollection<DownloadPluginData> DownloadPluginList { get; } =
            new ObservableCollection<DownloadPluginData>();

        public void FreeZipFile()
        {
            _ReadMePath = null;
        }

        public async Task InstallPlugin(string zipPath)
        {
            await Task.Run(() =>
            {
                FreeZipFile();
                var hash = CreateSHA1Hash(zipPath);
                var loadItem = _jsonData.FirstOrDefault(item => item.SHA1Hash == hash);
                if (loadItem == null)
                {
                    throw new ArgumentException("SHA1 of zip is not equal.");
                }

                using (var zipArchive = ZipFile.OpenRead(zipPath))
                {
                    foreach (var entry in zipArchive.Entries)
                    {
                        var filename = entry.FullName.Replace('/', '\\');
                        foreach (var item in loadItem.InstallDir)
                        {
                            var item0 = item[0].Replace('/', '\\');
                            if (filename.StartsWith(item0, StringComparison.OrdinalIgnoreCase))
                            {
                                var rel = Directory.GetParent(filename).FullName;
                                var path = Path.Combine(rel, item[1], Path.GetFileName(filename));
                                Directory.CreateDirectory(Directory.GetParent(path).FullName);
                                entry.ExtractToFile(path, true);

                                if (string.Compare(filename, loadItem.Readme, StringComparison.OrdinalIgnoreCase) == 0)
                                {
                                    ReadMePath = path;
                                }
                                break;
                            }
                        }
                    }
                }
            });
        }

        public void CheckExeDirectory()
        {
            var path = @"MikuMikuDance.exe";
            if (File.Exists(path) == false)
            {
                throw new FileNotFoundException("The MikuMikuDance.exe is not found.");
            }
            var hash = CreateSHA1Hash(path);
            if (hash != "7dbf4f27d6dd14ce77e2e659a69886e3b6739b56")
            {
                throw new InvalidOperationException("The MikuMikuDance.exe is wrong.");
            }
        }

        public void LoadPluginData()
        {
            var text = File.ReadAllText("package_list.json");

            _jsonData = JsonConvert.DeserializeObject<MMDPluginData[]>(text);
            foreach (var item in _jsonData)
            {
                DownloadPluginList.Add(new DownloadPluginData
                {
                    Url = item.URL,
                    NewVersion = item.Version,
                    NowVersion = -1,
                    Title = item.Title
                });
            }
            RaisePropertyChanged(nameof(DownloadPluginList));
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
            public float NewVersion { get; set; }

            public float NowVersion { get; set; }

            public string Title { get; set; }

            public string Url { get; set; }
        }

        #region ReadMePath変更通知プロパティ

        private string _ReadMePath;

        public string ReadMePath
        {
            get { return _ReadMePath; }

            set
            {
                if (_ReadMePath == value)
                {
                    return;
                }
                _ReadMePath = value;
                RaisePropertyChanged();
            }
        }

        #endregion ReadMePath変更通知プロパティ
    }
}
