using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Livet;
using Livet.Behaviors.ControlBinding.OneWay;
using Livet.Commands;
using Livet.EventListeners;
using Livet.Messaging;
using Livet.Messaging.Windows;
using Microsoft.Win32;
using MMDPluginInstallManager.Models;

namespace MMDPluginInstallManager.ViewModels
{
    public class MainWindowViewModel : ViewModel
    {
        private PropertyChangedEventListener _listener;
        private Model _model;
        /* コマンド、プロパティの定義にはそれぞれ
         *
         *  lvcom   : ViewModelCommand
         *  lvcomn  : ViewModelCommand(CanExecute無)
         *  llcom   : ListenerCommand(パラメータ有のコマンド)
         *  llcomn  : ListenerCommand(パラメータ有のコマンド・CanExecute無)
         *  lprop   : 変更通知プロパティ(.NET4.5ではlpropn)
         *
         * を使用してください。
         *
         * Modelが十分にリッチであるならコマンドにこだわる必要はありません。
         * View側のコードビハインドを使用しないMVVMパターンの実装を行う場合でも、ViewModelにメソッドを定義し、
         * LivetCallMethodActionなどから直接メソッドを呼び出してください。
         *
         * ViewModelのコマンドを呼び出せるLivetのすべてのビヘイビア・トリガー・アクションは
         * 同様に直接ViewModelのメソッドを呼び出し可能です。
         */

        /* ViewModelからViewを操作したい場合は、View側のコードビハインド無で処理を行いたい場合は
         * Messengerプロパティからメッセージ(各種InteractionMessage)を発信する事を検討してください。
         */

        /* Modelからの変更通知などの各種イベントを受け取る場合は、PropertyChangedEventListenerや
         * CollectionChangedEventListenerを使うと便利です。各種ListenerはViewModelに定義されている
         * CompositeDisposableプロパティ(LivetCompositeDisposable型)に格納しておく事でイベント解放を容易に行えます。
         *
         * ReactiveExtensionsなどを併用する場合は、ReactiveExtensionsのCompositeDisposableを
         * ViewModelのCompositeDisposableプロパティに格納しておくのを推奨します。
         *
         * LivetのWindowテンプレートではViewのウィンドウが閉じる際にDataContextDisposeActionが動作するようになっており、
         * ViewModelのDisposeが呼ばれCompositeDisposableプロパティに格納されたすべてのIDisposable型のインスタンスが解放されます。
         *
         * ViewModelを使いまわしたい時などは、ViewからDataContextDisposeActionを取り除くか、発動のタイミングをずらす事で対応可能です。
         */

        /* UIDispatcherを操作する場合は、DispatcherHelperのメソッドを操作してください。
         * UIDispatcher自体はApp.xaml.csでインスタンスを確保してあります。
         *
         * LivetのViewModelではプロパティ変更通知(RaisePropertyChanged)やDispatcherCollectionを使ったコレクション変更通知は
         * 自動的にUIDispatcher上での通知に変換されます。変更通知に際してUIDispatcherを操作する必要はありません。
         */

        #region DownLoadPluginList変更通知プロパティ

        private ObservableCollection<PluginData> _DownloadPluginList = new ObservableCollection<PluginData>();

        public ObservableCollection<PluginData> DownloadPluginList
        {
            get { return _DownloadPluginList; }
            set
            {
                _DownloadPluginList = value;
                RaisePropertyChanged();
            }
        }

        #endregion DownLoadPluginList変更通知プロパティ

        #region InstallCommand

        public async Task InstallCommand(string zipPath)
        {
            try
            {
                var installedItem = await _model.InstallPlugin(zipPath);
                MessageBox.Show("Install succeeded.\nfilename=" + zipPath
                                + "\n\nThe readme file is opened automatically.");
                try
                {
                    Process.Start(installedItem.ReadMeFilePath);
                }
                catch (Exception e)
                {
                    MessageBox.Show("Failed to open the readme file.\n" + e.Message + "\n"
                                    + installedItem.ReadMeFilePath + "\n\n"
                                    + e.StackTrace);
                }
            }
            catch (Exception e)
            {
                MessageBox.Show("Install failed.\nfilename=" + zipPath + "\n" + e.Message + "\n\n" + e.StackTrace);
            }
        }

        #endregion InstallCommand

        private void ExitWindow()
        {
            Messenger.Raise(new WindowActionMessage(WindowAction.Close, "Close"));
        }

        public async void Initialize()
        {
            _model = new Model();
            _listener = new PropertyChangedEventListener(_model)
            {
                nameof(_model.DownloadPluginDic),
                (_, __) =>
                    DispatcherHelper.UIDispatcher.Invoke(() =>
                    {
                        _DownloadPluginList.Clear();
                        foreach (var v in _model.DownloadPluginDic.Values)
                        {
                            MMDPluginPackage package;
                            _model.MMDInstalledPluginPackage.TryGetValue(v.Title, out package);
                            _DownloadPluginList.Add(new PluginData
                            {
                                LatestVersion = v.LatestVersion,
                                NowVersion = package?.Version ?? -1,
                                Title = v.Title,
                                ReadMeFilePath = package?.ReadMeFilePath,
                                Url = v.Url
                            });
                        }
                        RaisePropertyChanged(nameof(DownloadPluginList));
                    })
            };

            // mmdを選択してもらう
            MessageBox.Show("Choose MikuMikuDance.exe (ver9.26 x64).");
            var ofd = new OpenFileDialog
            {
                FileName = "MikuMikuDance.exe",
                Filter = "exe file(*.exe)|*.exe|all file(*.*)|*.*",
                FilterIndex = 1,
                Title = "Choose MikuMikuDance.exe (ver9.26 x64)"
            };


            if (ofd.ShowDialog() == false)
            {
                // キャンセルした場合は終了する
                MessageBox.Show("Canceled, so this program will end.");
                ExitWindow();
                return;
            }

            try
            {
                _model.SetMMDDirectory(ofd.FileName);
            }
            catch (Exception e)
            {
                // 選択したものが間違っていた場合は終了する
                MessageBox.Show(e.Message + "\nThis program will end.");
                ExitWindow();
                return;
            }
            await _model.LoadPluginData();
        }

        public class PluginData
        {
            public float NowVersion { get; set; }

            public float LatestVersion { get; set; }

            public string Title { get; set; }

            public string ReadMeFilePath { get; set; }

            public string Url { get; set; }
        }

        #region UninstallCommand

        private ViewModelCommand _UninstallCommand;

        public ViewModelCommand UninstallCommand
        {
            get
            {
                if (_UninstallCommand == null)
                {
                    _UninstallCommand = new ViewModelCommand(Uninstall, CanUninstall);
                }
                return _UninstallCommand;
            }
        }

        public bool CanUninstall() => SelectedPluginData != null && SelectedPluginData.NowVersion > 0.0;

        public async void Uninstall()
        {
            await _model.UninstallPlugin(_SelectedPluginData.Title);
            UninstallCommand.RaiseCanExecuteChanged();
        }

        #endregion

        #region SelectedPluginData変更通知プロパティ

        private PluginData _SelectedPluginData;

        public PluginData SelectedPluginData
        {
            get { return _SelectedPluginData; }

            set
            {
                if (_SelectedPluginData == value)
                {
                    return;
                }
                _SelectedPluginData = value;
                RaisePropertyChanged();
                UninstallCommand.RaiseCanExecuteChanged();
            }
        }

        #endregion SelectedPluginData変更通知プロパティ

        #region OpenLinkCommand

        private ListenerCommand<string> _OpenLinkCommand;

        public ListenerCommand<string> OpenLinkCommand
            => _OpenLinkCommand ?? (_OpenLinkCommand = new ListenerCommand<string>(OpenLink));

        public void OpenLink(string parameter)
        {
            Process.Start(parameter);
        }

        #endregion

        #region InstallZipCommand

        private ViewModelCommand _InstallZipCommand;

        public ViewModelCommand InstallZipCommand
        {
            get
            {
                if (_InstallZipCommand == null)
                {
                    _InstallZipCommand = new ViewModelCommand(InstallZip);
                }
                return _InstallZipCommand;
            }
        }

        public async void InstallZip()
        {
            var dlg = new OpenFileDialog
            {
                Filter = "zip file(*.zip)|*.zip|all file(*.*)|*.*",
                FilterIndex = 1,
                Title = "Select zip file."
            };
            if (dlg.ShowDialog() == true)
            {
                await InstallCommand(dlg.FileName);
            }
        }

        #endregion

        #region CopyLinkCommand

        private ListenerCommand<string> _CopyLinkCommand;

        public ListenerCommand<string> CopyLinkCommand
            => _CopyLinkCommand ?? (_CopyLinkCommand = new ListenerCommand<string>(CopyLink));

        private static void CopyLink(string parameter) => Clipboard.SetText(parameter);

        #endregion

        #region SetMMDPluginListViewCommand

        private ViewModelCommand _SetMMDPluginListViewCommand;

        public ViewModelCommand SetMMDPluginListViewCommand => _SetMMDPluginListViewCommand
                                                               ?? (_SetMMDPluginListViewCommand =
                                                                   new ViewModelCommand(SetMMDPluginListView));

        private void SetMMDPluginListView() =>
            SelectedPluginData = DownloadPluginList.First(s => s.Title == "MMDPlugin");

        #endregion

        #region OpenLicenseWindowCommand

        private ViewModelCommand _OpenLicenseWindowCommand;

        public ViewModelCommand OpenLicenseWindowCommand
        {
            get
            {
                if (_OpenLicenseWindowCommand == null)
                {
                    _OpenLicenseWindowCommand = new ViewModelCommand(OpenLicenseWindow);
                }
                return _OpenLicenseWindowCommand;
            }
        }

        public void OpenLicenseWindow()
        {
            var vm = new LicenseWindowViewModel();
            Messenger.Raise(new TransitionMessage(vm, "OpenLicenseCommand"));
        }

        #endregion
    }
}
