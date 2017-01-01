using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Livet;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.IO;
using Newtonsoft.Json;
using System.IO.Compression;
using System.Security.Cryptography;

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

        private MMDPluginData[] jsonData;

        public Model()
        {
        }

        public ObservableCollection<DownloadPluginData> DownloadPluginList { get; set; } = new ObservableCollection<DownloadPluginData>();

        #region ReadMePath変更通知プロパティ

        private string _ReadMePath;

        public string ReadMePath
        {
            get { return _ReadMePath; }

            set
            {
                if (_ReadMePath == value)
                    return;
                _ReadMePath = value;
                RaisePropertyChanged(nameof(ReadMePath));
            }
        }

        #endregion ReadMePath変更通知プロパティ


        public static string MakeRelative(string filePath, string referencePath)
        {
            var fileUri = new Uri(filePath);
            var referenceUri = new Uri(referencePath);
            return referenceUri.MakeRelativeUri(fileUri).ToString();
        }

        public void freeZipFile()
        {
            _ReadMePath = null;
        }

        public async Task<bool> InstallPlugin(string zipPath)
        {
            return await Task.Run(() =>
            {
                freeZipFile();
                var hash = CreateSHA1Hash(zipPath);
                MMDPluginData LoadItem = null;

                foreach (var item in jsonData)
                {
                    if (item.SHA1Hash == hash)
                    {
                        LoadItem = item;
                        break;
                    }
                }
                if (LoadItem == null) return false;

                using (ZipArchive zipArchive = ZipFile.OpenRead(zipPath))
                {
                    foreach (ZipArchiveEntry entry in zipArchive.Entries)
                    {
                        var filename = entry.FullName.Replace('/', '\\');
                        foreach (var item in LoadItem.InstallDir)
                        {
                            var item0 = item[0].Replace('/', '\\');
                            if (filename.StartsWith(item0, StringComparison.OrdinalIgnoreCase))
                            {
                                var rel = Directory.GetParent(filename).FullName;
                                var path = Path.Combine(rel, item[1], Path.GetFileName(filename));
                                Directory.CreateDirectory(Directory.GetParent(path).FullName);
                                entry.ExtractToFile(path, true);

                                if (String.Compare(filename, LoadItem.Readme, true) == 0)
                                {
                                    ReadMePath = path;
                                }
                                break;
                            }
                        }
                    }
                }
                return true;
            });
        }

        public void LoadPluginData()
        {
            var text = File.ReadAllText("package_list.json");

            jsonData = JsonConvert.DeserializeObject<MMDPluginData[]>(text);
            foreach (var item in jsonData)
            {
                DownloadPluginList.Add(new DownloadPluginData { url = item.URL, NewVersion = item.Version, NowVersion = -1, Title = item.Title });
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

        public struct DownloadPluginData
        {
            public float NewVersion { get; set; }

            public float NowVersion { get; set; }

            public string Title { get; set; }

            public string url { get; set; }
        }
    }

}
