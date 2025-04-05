using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading;
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
using static Automator.MainWindow;
using automator_actioner;

namespace Automator
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private bool debug = false;
        string[] testCaseNames = null;
        private ObservableCollection<Log> logs = new ObservableCollection<Log>();

        // actionerの定義
        private Actioner actioner;
        private string ACTIONER_PATH = System.IO.Path.Combine(Directory.GetCurrentDirectory(), "automator-actioner.dll");

        public MainWindow()
        {
            // UI周りの初期化
            InitializeComponent();
            this.Height = 420;
            Button_Debug.Content = "▶ デバッグ";

            // ログ関連の初期化
            List_Log.ItemsSource = logs;
            Combo_TestCase.Items.Clear();
            Combo_TestCase.Items.Add("DEBUG");
            Combo_TestCase.Text = "DEBUG";

            // Actionerロード
            Assembly assembly = Assembly.LoadFrom(ACTIONER_PATH);
            Type actionerType = assembly.GetType("automator_actioner.Actioner");
            this.actioner = (Actioner)Activator.CreateInstance(actionerType);

            // Actioner初期化
            foreach (string log in this.actioner.Init())
            {
                LogFlush(log);
            }
        }

        /*
         * バージョン情報の表示
         */
        private void MenuItem_Version_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Version 1.0.0 Created by Hatori Ichigo", "バージョン情報");

        }

        /*
         * csvファイル読み込み先ファイル
         */
        private void OpenSourceFile_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog();
            if (openFileDialog.ShowDialog() == true)
            {
                try
                {
                    Text_SourceFilePath.Text = openFileDialog.FileName;
                    this.actioner.SetSourceFile(openFileDialog.FileName);
                    string ret = this.actioner.ParseProgram();
                    if (ret == "")
                    {
                        testCaseNames = actioner.GetAllTestCaseNames();

                        Combo_TestCase.Items.Clear();
                        Combo_TestCase.Items.Add("DEBUG");
                        foreach (string testCaseName in testCaseNames)
                        {
                            Combo_TestCase.Items.Add(testCaseName);
                        }
                    }
                    else
                    {
                        LogFlush(ret);
                    }
                }
                catch {
                    LogMessage("E-cmn-0013", "入力ファイル読み込み中にエラーが発生しました。");
                    MessageBox.Show("入力ファイル読み込み中にエラーが発生しました。", "E-cmn-0013");
                    return;
                }

            }
        }

        /*
         * 書き込み先ディレクトリ指定
         */
        private void OpenDestDirectory_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog();
            if (openFileDialog.ShowDialog() == true)
            {
                Text_DestDirPath.Text = openFileDialog.FileName;
                this.actioner.SetDestDirectory(openFileDialog.FileName);
            }
        }

        /*
         * 実行ボタンをクリック
         */
        private async void Action_Click(object sender, RoutedEventArgs e)
        {
            await Task.Run(() =>
            {
                foreach (string caseName in actioner.GetAllTestCaseNames())
                {
                    actioner.setCasePtr(caseName);
                    actioner.setPtr(0);

                    // 開始ログ
                    Dispatcher.Invoke(new Action(() => { LogMessage("I-cmn-0001", "ケース[" + caseName + "]の処理を開始します。"); }));

                    string cmd = "__EOA__";
                    while ((cmd = actioner.getActionCmd()) != "__EOA__")
                    {
                        Dispatcher.Invoke(new Action(() => { LogMessage("D-cmn-0003", "実行コマンド：" + cmd); }));

                        foreach (string log in this.actioner.Action(cmd))
                        {
                            if (LogFlush(log) > 0)
                            {
                                return;
                            }
                        }
                        this.actioner.NextPtr();
                        Thread.Sleep(50);
                    }

                    // 終了ログ
                    Dispatcher.Invoke(new Action(() => { LogMessage("I-cmn-0002", "ケース[" + caseName + "]の処理を終了します。"); }));
                }
            });
        }


        /*
         * デバッグボタンをクリック
         */
        private void Debug_Click(object sender, RoutedEventArgs e)
        {
            if (debug) {
                this.Height = 420;
                Button_Debug.Content = "▶ デバッグ";
            }
            else {
                this.Height = 570;
                Button_Debug.Content = "▼ デバッグ";
            }
            debug = !debug;
        }

        private void Combo_TestCase_DropDownClosed(object sender, EventArgs e)
        {
            if (Combo_TestCase.SelectedItem.ToString() != "DEBUG")
            {
                string caseName = Combo_TestCase.SelectedItem.ToString();
                actioner.setCasePtr(caseName);
                actioner.setPtr(0);
                Text_ActionCmd.Text = actioner.getActionCmd();

            }

        }

        private void Button_Step_Click(object sender, RoutedEventArgs e)
        {
            if (Text_ActionCmd.Text == "") { return; }

            foreach (string log in this.actioner.Action(Text_ActionCmd.Text))
            {
                JsonNode jsonNode = JsonNode.Parse(log);

                Log _log = new Log();
                _log.date = DateTime.Now.ToString();
                _log.errCode = jsonNode["errCode"]?.ToString();
                _log.errMsg = jsonNode["errMsg"]?.ToString();

                switch (_log.errCode[0])
                {
                    case 'E':
                        MessageBox.Show(_log.errMsg, _log.errCode);
                        break;
                    case 'C':
                        MessageBox.Show(_log.errMsg, _log.errCode);
                        break;
                    case 'F':
                        MessageBox.Show(_log.errMsg, _log.errCode);
                        break;
                }

                logs.Add(_log);
            }

            if (Combo_TestCase.Text != "DEBUG")
            {
                this.actioner.NextPtr();
                Text_ActionCmd.Text = actioner.getActionCmd();
            }
            
        }

        /*
         * 
         * ログ関連
         * 
         */

        public class Log
        {
            public string errCode { get; set; } = "";
            public string errMsg { get; set; } = "";
            public string date { get; set; } = DateTime.Now.ToString();
        }

        private void LogMessage(string errCode, string errMsg)
        {
            Log log = new Log();
            log.errCode = errCode;
            log.errMsg = errMsg;
            logs.Add(log);
        }
        private int LogFlush(string log)
        {
            // 結果のjsonパース
            JsonNode? jsonNode;
            try
            {
                jsonNode = JsonNode.Parse(log);
                if (jsonNode == null)
                {
                    Dispatcher.Invoke(new Action(() => { LogMessage("E-cmn-0004", "結果の取得に失敗しました。 {log: null}"); }));
                    MessageBox.Show("結果の取得に失敗しました。", "E-cmn-0004");
                    return 1;
                }
            }
            catch
            {
                Dispatcher.Invoke(new Action(() => { LogMessage("E-cmn-0005", "結果の取得に失敗しました。 {log: " + log + "}"); }));
                MessageBox.Show("結果の取得に失敗しました。", "E-cmn-0005");
                return 1;
            }

            string errCode = "";
            string errMsg = "";
            try
            {
                errCode = jsonNode["errCode"]?.ToString();
                if (errCode == "")
                {
                    Dispatcher.Invoke(new Action(() => { LogMessage("E-cmn-0008", "errCodeの取得に失敗しました。"); }));
                    MessageBox.Show("errCodeの取得に失敗しました。", "E-cmn-0008");
                    return 1;
                }
            }
            catch
            {
                Dispatcher.Invoke(new Action(() => { LogMessage("E-cmn-0006", "errCodeの取得に失敗しました。"); }));
                MessageBox.Show("errCodeの取得に失敗しました。", "E-cmn-0006");
                return 1;
            }
            try
            {
                errMsg = jsonNode["errMsg"]?.ToString();
            }
            catch
            {
                Dispatcher.Invoke(new Action(() => { LogMessage("E-cmn-0007", "errMsgの取得に失敗しました。"); }));
                MessageBox.Show("errMsgの取得に失敗しました。", "E-cmn-0007");
                return 1;
            }

            // メッセージボックス表示
            int ret = 0;
            switch (errCode[0])
            {
                case 'E':
                    MessageBox.Show(errMsg, errCode);
                    ret = 1;
                    break;
                case 'C':
                    MessageBox.Show(errMsg, errCode);
                    ret = 1;
                    break;
                case 'F':
                    MessageBox.Show(errMsg, errCode);
                    ret = 1;
                    break;
            }
            Dispatcher.Invoke(new Action(() => { LogMessage(errCode, errMsg); }));
            return ret;
        }
    }
}
