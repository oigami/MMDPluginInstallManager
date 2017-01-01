using MMDPluginInstallManager.Models;
using MMDPluginInstallManager.ViewModels;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;

namespace MMDPluginInstallManager.Views
{
    /*
	 * ViewModelからの変更通知などの各種イベントを受け取る場合は、PropertyChangedWeakEventListenerや
     * CollectionChangedWeakEventListenerを使うと便利です。独自イベントの場合はLivetWeakEventListenerが使用できます。
     * クローズ時などに、LivetCompositeDisposableに格納した各種イベントリスナをDisposeする事でイベントハンドラの開放が容易に行えます。
     *
     * WeakEventListenerなので明示的に開放せずともメモリリークは起こしませんが、できる限り明示的に開放するようにしましょう。
     */

    /// <summary>
    /// MainWindow.xaml の相互作用ロジック 
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private async void Window_Drop(object sender, DragEventArgs e)
        {
            var viewModel = this.DataContext as MainWindowViewModel;
            if (viewModel == null) return;


            string[] files = e.Data.GetData(DataFormats.FileDrop) as string[];
            if (files == null) return;

            foreach (var s in files)
            {
                if (string.Compare(Path.GetExtension(s), ".zip", true) == 0)
                {
                    await viewModel.Drop(s);
                }
            }
        }

        private void Window_PreviewDragOver(object sender, DragEventArgs e)
        {
            e.Effects = DragDropEffects.None;
            e.Handled = true;
            if (e.Data.GetDataPresent(DataFormats.FileDrop, true) == false) return;

            string[] files = e.Data.GetData(DataFormats.FileDrop) as string[];
            if (files == null) return;

            foreach (var s in files)
            {
                if (string.Compare(Path.GetExtension(s), ".zip", true) == 0)
                {
                    e.Effects = DragDropEffects.Copy;
                    break;
                }
            }
        }

    }
}
