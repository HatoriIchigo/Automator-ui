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
using static System.Runtime.CompilerServices.RuntimeHelpers;
using System.Text.RegularExpressions;

namespace Automator
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private bool debug = false;
        string[] testCaseNames = null;
        private Dictionary<string, string> properties { set; get; } = new Dictionary<string, string>();
        private ObservableCollection<Log> logs = new ObservableCollection<Log>();

        // actionerの定義
        private Actioner actioner;
        private string ACTIONER_PATH = System.IO.Path.Combine(Directory.GetCurrentDirectory(), "libs", "automator-actioner.dll");

        public MainWindow()
        {
            // UI周りの初期化
            InitializeComponent();
            this.Height = 460;
            Button_Debug.Content = "▶ デバッグ";

            // ログ関連の初期化
            List_Log.ItemsSource = logs;
            Combo_TestCase.Items.Clear();
            Combo_TestCase.Items.Add("DEBUG");
            Combo_TestCase.Text = "DEBUG";

            // 共通設定ファイル読み込み
            switch (ReadConfigFile(System.IO.Path.Combine(Directory.GetCurrentDirectory(), "config", "common.ini"))) {
                case 0:
                    break;
                case 1:
                    Application.Current.Shutdown(1);
                    break;
            }

            // Actionerロード
            Assembly assembly = Assembly.LoadFrom(ACTIONER_PATH);
            foreach (Type type in assembly.GetTypes())
            {
                Debug.WriteLine(type.FullName);
            }
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
                        LogMessage("I-cmn-0020", "ファイル読み込みに成功しました。");
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
            var folderDialog = new OpenFolderDialog();
            if (folderDialog.ShowDialog() == true)
            {
                Text_DestDirPath.Text = folderDialog.FolderName;
                this.actioner.SetDestDirectory(folderDialog.FolderName);
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
                            // 異常時
                            if (LogFlush(log) > 0)
                            {
                                return;
                            }
                        }
                        this.actioner.NextPtr();
                        Thread.Sleep(Int32.Parse(this.properties["SLEEP_TIME"]));
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
                this.Height = 460;
                Button_Debug.Content = "▶ デバッグ";
            }
            else {
                this.Height = 595;
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

            if (Combo_TestCase.Text != "DEBUG" && Combo_TestCase.Text != "")
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
            Debug.WriteLine("LOG: " + log);
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
                case 'D':
                    if (this.properties["LOGLEVEL"] == "DEBUG")
                    {
                        Dispatcher.Invoke(new Action(() => { LogMessage(errCode, errMsg); }));
                    }
                    break;
                case 'I':
                    if (this.properties["LOGLEVEL"] == "INFO" || this.properties["LOGLEVEL"] == "DEBUG")
                    {
                        Dispatcher.Invoke(new Action(() => { LogMessage(errCode, errMsg); }));
                    }
                    break;
                case 'W':
                    MessageBox.Show(errMsg, errCode, MessageBoxButton.OK, MessageBoxImage.Warning);
                    if (this.properties["LOGLEVEL"] == "WARN" || this.properties["LOGLEVEL"] == "INFO" || this.properties["LOGLEVEL"] == "DEBUG")
                    {
                        Dispatcher.Invoke(new Action(() => { LogMessage(errCode, errMsg); }));
                    }
                    break;
                case 'E':
                    MessageBox.Show(errMsg, errCode, MessageBoxButton.OK, MessageBoxImage.Error);
                    if (this.properties["LOGLEVEL"] == "ERROR" || this.properties["LOGLEVEL"] == "WARN" || this.properties["LOGLEVEL"] == "INFO" || this.properties["LOGLEVEL"] == "DEBUG")
                    {
                        Dispatcher.Invoke(new Action(() => { LogMessage(errCode, errMsg); }));
                    }
                    ret = 1;
                    break;
                default:
                    MessageBox.Show("存在しないログレベルが検出されました。[errCode: " + errCode + "]", "E-cmn-0027", MessageBoxButton.OK, MessageBoxImage.Error);
                    break;
            }
            return ret;
        }

        private int ReadConfigFile(string configPath)
        {
            // 設定ファイル無し
            if (!File.Exists(configPath))
            {
                MessageBox.Show("設定ファイルが見つかりません。[configPath:  " + configPath + "]\nアプリケーションを終了します。", "E-cmn-0021", MessageBoxButton.OK, MessageBoxImage.Error);
                return 1;
            }

            // 設定ファイル読み込み
            foreach (string line in File.ReadAllLines(configPath))
            {
                if (string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith("#")) continue;
                string[] d = line.Split(new char[] { '=' });
                if (d.Length == 2)
                {
                    this.properties[d[0].Trim()] = d[1].Trim();
                }
                else
                {
                    MessageBox.Show("設定ファイルに異常な値が見つかりました。[configFilePath: " + configPath + " line:  " + line + "]\nアプリケーションを終了します。", "E-cmn-0022", MessageBoxButton.OK, MessageBoxImage.Error);
                    return 1;
                }
            }

            // 設定値確認
            // SLEEP_TIME
            if (!this.properties.ContainsKey("SLEEP_TIME"))
            {
                MessageBox.Show("設定ファイルに[SLEEP_TIME]が見つかりません。\nアプリケーションを終了します。", "E-cmn-0023", MessageBoxButton.OK, MessageBoxImage.Error);
                return 1;
            }
            if (!Regex.IsMatch(this.properties["SLEEP_TIME"], @"^\d+$"))
            {
                MessageBox.Show("設定値[SLEEP_TIME]は数値に変換できません。\nアプリケーションを終了します。", "E-cmn-0024", MessageBoxButton.OK, MessageBoxImage.Error);
                return 1;
            }

            // ログレベル
            string[] logLevels = { "DEBUG", "INFO", "WARN", "ERROR" };
            if (!this.properties.ContainsKey("LOGLEVEL"))
            {
                this.properties["LOGLEVEL"] = "INFO";
            }
            if (!logLevels.Contains(this.properties["LOGLEVEL"]))
            {
                MessageBox.Show("設定値[LOGLEVEL]の値が異常でした。\nログレベルをIFNOに設定します。", "W-cmn-0026", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            if (this.properties["LOGLEVEL"] == "DEBUG")
            {
                LogMessage("D-cmn-0025", "設定値SLEEP_TIME[" + this.properties["SLEEP_TIME"] + "] 確認OK");
                LogMessage("D-cmn-0025", "設定値LOGLEVEL[" + this.properties["LOGLEVEL"] + "] 確認OK");
            }
            return 0;
        }
    }
}
