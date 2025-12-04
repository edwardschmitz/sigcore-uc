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
using System.Windows.Shapes;

namespace SigCoreCleint {
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window {
        private SigCoreClient _client;
        public MainWindow() {
            InitializeComponent();

            Loaded += MainWindow_Loaded;
            Console.SetOut(new ConsoleWriter(AppendLog));
        }
        private async void MainWindow_Loaded(object sender, RoutedEventArgs e) {
            _client = new SigCoreClient();

        }

        private async void Open_Click(object sender, RoutedEventArgs e) {
            await _client.ConnectAsync("192.168.0.221");
            await _client.PingAsync();
        }

        private async void Send_Click(object sender, RoutedEventArgs e) {
            if (_client.IsCommander) {
                await _client.SetRelayAsync(0, true);
                await Task.Delay(1000);
                await _client.SetRelayAsync(0, false);
                await _client.SetRelayAsync(1, true);
                await Task.Delay(1000);
                await _client.SetRelayAsync(1, false);
                await _client.SetRelayAsync(2, true);
                await Task.Delay(1000);
                await _client.SetRelayAsync(2, false);
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e) {
            _client.Close();
        }

        private void AppendLog(string line) {
            Dispatcher.Invoke(() => {
                Log.AppendText(line + Environment.NewLine);
                Log.ScrollToEnd();
            });
        }

        private async void Ping_Click(object sender, RoutedEventArgs e) {
            await _client.PingAsync();
        }
    }

    public class ConsoleWriter : TextWriter {
        private readonly Action<string> _writeAction;

        public ConsoleWriter(Action<string> writeAction) {
            _writeAction = writeAction;
        }

        public override void WriteLine(string value) {
            _writeAction(value);
        }

        public override Encoding Encoding => Encoding.UTF8;
    }
}
