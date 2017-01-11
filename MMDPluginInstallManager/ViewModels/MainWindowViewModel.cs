using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Livet;
using Livet.Commands;
using Livet.EventListeners;
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

        public ObservableCollection<Model.DownloadPluginData> DownloadPluginList
        {
            get { return _model?.DownloadPluginList; }
        }

        #endregion DownLoadPluginList変更通知プロパティ

        #region InstallCommand

        public async Task InstallCommand(string zipPath)
        {
            try
            {
                await _model.InstallPlugin(zipPath);
                MessageBox.Show("Install succeeded.");
            }
            catch (Exception e)
            {
                MessageBox.Show("Install failed.\n" + e.Message + "\n\n" + e.StackTrace);
                return;
            }

            try
            {
                Process.Start(_model.ReadMePath);
            }
            catch (Exception e)
            {
                MessageBox.Show("readme file was not found.\n" + _model.ReadMePath + "\n\n" + e.StackTrace);
            }
        }

        #endregion InstallCommand

        private static void ExitWindow()
        {
            var window = Application.Current.Windows.OfType<Window>().SingleOrDefault(w => w.IsActive);
            window?.Close();
        }

        public void Initialize()
        {
            _model = new Model();
            _listener = new PropertyChangedEventListener(_model)
            {
                nameof(_model.DownloadPluginList),
                (_, __) => RaisePropertyChanged(nameof(DownloadPluginList))
            };

            try
            {
                // 今いるディレクトリにmmdがあるかチェック
                _model.SetMMDDirectory("");
            }
            catch (Exception)
            {
                // mmdがなかった場合は選択してもらう
                MessageBox.Show("Choose MikuMikuDance.exe.");
                var ofd = new OpenFileDialog
                {
                    FileName = "MikuMikuDance.exe",
                    Filter = "exe file(*.exe)|*.exe|all file(*.*)|*.*",
                    FilterIndex = 1,
                    Title = "Choose MikuMikuDance.exe"
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
                    MessageBox.Show(e.Message + "\nThis Program will end.");
                    ExitWindow();
                }
            }
            _model.LoadPluginData();
        }

        #region SelectedPluginData変更通知プロパティ

        private Model.DownloadPluginData _SelectedPluginData;

        public Model.DownloadPluginData SelectedPluginData
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
            }
        }

        #endregion SelectedPluginData変更通知プロパティ

        #region OpenDownloadLinkCommand

        private ViewModelCommand _OpenDownloadLinkCommand;

        public ViewModelCommand OpenDownloadLinkCommand
        {
            get
            {
                return _OpenDownloadLinkCommand ?? (_OpenDownloadLinkCommand = new ViewModelCommand(OpenDownloadLink));
            }
        }

        private void OpenDownloadLink()
        {
            Process.Start(SelectedPluginData.Url);
        }

        #endregion OpenDownloadLinkCommand
    }
}
