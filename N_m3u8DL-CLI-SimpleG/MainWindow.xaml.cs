using Microsoft.Win32.SafeHandles;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Reflection;
using System.Runtime.Remoting.Messaging;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web.UI.WebControls;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Forms.VisualStyles;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;
using System.Xml.Linq;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;
using Application = System.Windows.Application;
using Button = System.Windows.Controls.Button;
using Clipboard = System.Windows.Clipboard;
using DataFormats = System.Windows.DataFormats;
using DragDropEffects = System.Windows.DragDropEffects;
using MessageBox = System.Windows.MessageBox;
using Path = System.IO.Path;
using TextBox = System.Windows.Controls.TextBox;

namespace N_m3u8DL_CLI_SimpleG
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// 
    /// 2019年6月17日
    /// - 重构界面并修复爱奇艺标题获取BUG
    /// 2019年6月18日
    /// - 添加图标
    /// 2019年6月23日
    /// - 调整寻找主程序的逻辑
    /// - 修改匹配URL的正则表达式
    /// - 启动时自动匹配URL并识别标题
    /// - 启动后M3U8地址文本框会自动获得焦点
    /// - M3U8地址和标题两个文本框能够响应回车事件
    /// - GO按钮点击可以使用ALT+S快捷键来触发
    /// 2019年7月24日
    /// - 优化获取视频标题的逻辑
    /// - 增加生成--downloadRange参数
    /// 2019年8月11日
    /// - 批量txt支持自定义文件名
    /// 2019年8月17日
    /// - 支持爱奇艺dash链接直接下载
    /// - 修复腾讯视频标题获取bug
    /// 2019年9月18日
    /// - 支持限速
    /// - 全新界面
    /// - 增加控件悬浮提示
    /// 2019年9月28日
    /// - 双击时判断URL是否一致再赋值
    /// - 细节优化
    /// 2019年10月9日
    /// - 自动获取文件编码
    /// 2019年10月24日
    /// - 请求dash链接时尝试读取iqiyicookie.txt
    /// 2019年12月16日
    /// - 批量读取txt跳过空白行
    /// - 腾讯Unicode转换
    /// 2020年2月1日
    /// - 修复部分wetv无法识别标题的问题 
    /// 2020年2月17日
    /// - 拖入meta.json自动命名
    /// - 拖入KEY文件校验是否正确
    /// - 可调节大小
    /// 2020年4月17日
    /// - 修改BAT为UTF-8编码
    /// - 细微优化
    /// 2020年11月21日
    /// - 修正UI
    /// 2021年1月24日
    /// - 支持简繁英多语言
    /// 2021年3月4日
    /// - 支持设置代理
    /// - 支持存储代理、请求头
    /// 2021年3月21日
    /// - 支持MPD批量
    /// 2023年12月
    /// - 添加任务列表支持
    /// - Go按钮变成Add按钮，不是直接执行，而是添加到列表
    /// </summary>
    public partial class MainWindow : System.Windows.Window
    {
        public List<M3u8TaskItem> m3u8_tasks;
        public M3u8TaskItem selected_task;
        public string tasks_status;

        //下载进程
        private Process m3u8dlProcess;
        private TaskCompletionSource<bool> m3u8dlEventHandled;
        //下载时，读取后台下载进程的log到UI用
        StreamReader logBlockReader;

        public MainWindow()
        {
            InitializeComponent();
            TextBox_URL.Focus();

            //当通过协议启动时，目录会指向 windows/system32
            //设置当前目录为exe所在目录，防止因快捷方式或通过协议启动时，当前目录不是exe目录，导致找不到目录下的其他文件
            Environment.CurrentDirectory = Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName);

            //this.m3u8_tasks = new List<M3u8TaskItem>();
            //从文件读取m3u8_tasks
            Helper.m3u8_tasks = Helper.listFileIO.Load();

            this.tasks_status = "";
            lbTaskList.ItemsSource = Helper.m3u8_tasks;
            //lbTaskList.Items.Refresh();

            //通过协议启动时，协议的内容，会作为启动参数传递进来
            //但是，如果程序已经运行，则不会传递任何信息，而是后面注册命名管道之后，通过命名管道传递进来。
            HandleArgs();

            //启动命名管道，接收数据
            //这是异步管道，不要等待结果，等待会卡UI。
            ReceiveDataAsync();

        }


        //用于证书验证  
        public static bool CertificateValidationCallback(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            X509Chain verify = new X509Chain();
            verify.ChainPolicy.VerificationFlags = X509VerificationFlags.AllowUnknownCertificateAuthority;
            verify.ChainPolicy.RevocationMode = X509RevocationMode.Online; //revocation checking  
            verify.ChainPolicy.RevocationFlag = X509RevocationFlag.ExcludeRoot;
            if (verify.Build(new X509Certificate2(certificate)))
            {
                return true;
            }
            return false;
        }


        private void Button_SelectDir_Click(object sender, RoutedEventArgs e)
        {
            FolderBrowserDialog openFileDialog = new FolderBrowserDialog();  //选择文件夹
            openFileDialog.Description = Properties.Resources.String1;
            if (openFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                TextBox_WorkDir.Text = openFileDialog.SelectedPath;
            }
        }

        private void GetParameter()
        {
            if (TextBox_Parameter == null)
                return;

            StringBuilder sb = new StringBuilder();
            sb.Append("\"" + TextBox_URL.Text + "\" ");
            if (!string.IsNullOrEmpty(TextBox_WorkDir.Text))
            {
                if (TextBox_WorkDir.Text.Trim('\\').EndsWith(":")) //根目录
                {
                    sb.Append("--workDir \"" + TextBox_WorkDir.Text.Trim('\\') + "\\\\" + "\" ");
                }
                else
                {
                    sb.Append("--workDir \"" + TextBox_WorkDir.Text.Trim('\\') + "\" ");
                }
            }
            if (!string.IsNullOrEmpty(TextBox_Title.Text))
                sb.Append("--saveName \"" + TextBox_Title.Text + "\" ");
            if (!string.IsNullOrEmpty(TextBox_Headers.Text))
                sb.Append("--headers \"" + TextBox_Headers.Text + "\" ");
            if (!string.IsNullOrEmpty(TextBox_Baseurl.Text))
                sb.Append("--baseUrl \"" + TextBox_Baseurl.Text + "\" ");
            if (!string.IsNullOrEmpty(TextBox_MuxJson.Text))
                sb.Append("--muxSetJson \"" + TextBox_MuxJson.Text + "\" ");
            if (TextBox_Max.Text != "32")
                sb.Append("--maxThreads \"" + TextBox_Max.Text + "\" ");
            if (TextBox_Min.Text != "16")
                sb.Append("--minThreads \"" + TextBox_Min.Text + "\" ");
            if (TextBox_Retry.Text != "15")
                sb.Append("--retryCount \"" + TextBox_Retry.Text + "\" ");
            if (TextBox_Timeout.Text != "10")
                sb.Append("--timeOut \"" + TextBox_Timeout.Text + "\" ");
            if (TextBox_StopSpeed.Text != "0") 
                sb.Append("--stopSpeed \"" + TextBox_StopSpeed.Text + "\" ");
            if (TextBox_MaxSpeed.Text != "0")
                sb.Append("--maxSpeed \"" + TextBox_MaxSpeed.Text + "\" ");
            if (TextBox_Key.Text != "")
            {
                if (File.Exists(TextBox_Key.Text))
                    sb.Append("--useKeyFile \"" + TextBox_Key.Text + "\" ");
                else
                    sb.Append("--useKeyBase64 \"" + TextBox_Key.Text + "\" ");
            }
            if (TextBox_IV.Text != "")
            {
                sb.Append("--useKeyIV \"" + TextBox_IV.Text + "\" ");
            }
            if (TextBox_Proxy.Text != "")
            {
                sb.Append("--proxyAddress \"" + TextBox_Proxy.Text.Trim() + "\" ");
            }
            if (CheckBox_Del.IsChecked == true) 
                sb.Append("--enableDelAfterDone ");
            if (CheckBox_FastStart.IsChecked == true)
                sb.Append("--enableMuxFastStart ");
            if (CheckBox_BinaryMerge.IsChecked == true)
                sb.Append("--enableBinaryMerge ");
            if (CheckBox_ParserOnly.IsChecked == true)
                sb.Append("--enableParseOnly ");
            if (CheckBox_DisableDate.IsChecked == true)
                sb.Append("--disableDateInfo ");
            if (CheckBox_DisableMerge.IsChecked == true)
                sb.Append("--noMerge ");
            if (CheckBox_DisableProxy.IsChecked == true)
                sb.Append("--noProxy ");
            if (CheckBox_DisableCheck.IsChecked == true)
                sb.Append("--disableIntegrityCheck ");
            if (CheckBox_AudioOnly.IsChecked == true)
                sb.Append("--enableAudioOnly ");
            if (TextBox_RangeStart.Text!="00:00:00"|| TextBox_RangeEnd.Text != "00:00:00")
            {
                sb.Append($"--downloadRange \"{TextBox_RangeStart.Text}-{TextBox_RangeEnd.Text}\"");
            }

            TextBox_Parameter.Text = sb.ToString();
        }

        private void TextChanged(object sender, TextChangedEventArgs e)
        {
            GetParameter();
        }

        private void CheckBoxChanged(object sender, RoutedEventArgs e)
        {
            if (((System.Windows.Controls.CheckBox)sender).IsChecked == true) 
            {
                ((System.Windows.Controls.CheckBox)sender).Foreground = new SolidColorBrush(Color.FromRgb(46, 204, 113));
            }
            else
            {
                ((System.Windows.Controls.CheckBox)sender).Foreground = new SolidColorBrush(Color.FromRgb(241, 241, 241));
            }
            GetParameter();
        }

        private void FlashTextBox(TextBox textBox)
        {
            var orgColor = textBox.Background;
            SolidColorBrush myBrush = new SolidColorBrush();
            ColorAnimation myColorAnimation = new ColorAnimation();
            myColorAnimation.To = (Color)ColorConverter.ConvertFromString("#2ecc71");
            myColorAnimation.Duration = TimeSpan.FromMilliseconds(300);
            myBrush.BeginAnimation(SolidColorBrush.ColorProperty, myColorAnimation, HandoffBehavior.Compose);
            textBox.Background = myBrush;

            myColorAnimation.To = (Color)ColorConverter.ConvertFromString(orgColor.ToString());
            myColorAnimation.Duration = TimeSpan.FromMilliseconds(1000);
            myBrush.BeginAnimation(SolidColorBrush.ColorProperty, myColorAnimation, HandoffBehavior.Compose);
            textBox.Background = myBrush;
        }

        private void TextBox_URL_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            //从剪切板读取url
            Regex url = new Regex(@"(https?)://[-A-Za-z0-9+&@#/%?=~_|!:,.;]+[-A-Za-z0-9+&@#/%=~_|]", RegexOptions.Compiled | RegexOptions.Singleline);//取已下载大小
            string str = url.Match(Clipboard.GetText()).Value;
            if (str != "" && str != TextBox_URL.Text)
            {
                TextBox_URL.Text = str;
                FlashTextBox(TextBox_URL);
            }
        }

        public static byte[] HexStringToBytes(string hexStr)
        {
            if (string.IsNullOrEmpty(hexStr))
            {
                return new byte[0];
            }

            if (hexStr.StartsWith("0x") || hexStr.StartsWith("0X"))
            {
                hexStr = hexStr.Remove(0, 2);
            }

            int count = hexStr.Length;

            if (count % 2 == 1)
            {
                throw new ArgumentException("Invalid length of bytes:" + count);
            }

            int byteCount = count / 2;
            byte[] result = new byte[byteCount];
            for (int ii = 0; ii < byteCount; ++ii)
            {
                var tempBytes = Byte.Parse(hexStr.Substring(2 * ii, 2), System.Globalization.NumberStyles.HexNumber);
                result[ii] = tempBytes;
            }

            return result;
        }

        private string GetTitleFromURL(string url)
        {
            try
            {
                if (File.Exists(url))
                    return Path.GetFileNameWithoutExtension(url);
                if (url.StartsWith("http"))
                    url = url.Replace("http://", "").Replace("https://", "");
                //从爱奇艺dash接口获取内容
                if (url.Contains("dash") && (url.StartsWith("cache.video.iqiyi.com") || url.StartsWith("intel-cache.video.iqiyi.com"))) 
                {
                    string tvid = GetQueryString("tvid", url);
                    string webSource = GetWebSource($"https://pcw-api.iqiyi.com/video/video/baseinfo/{tvid}");
                    Regex rexTitle = new Regex("name\":\"(.*?)\"");
                    string title = GetValidFileName(rexTitle.Match(webSource).Groups[1].Value);

                    webSource = GetWebSource("https://" + url, "Cookie:" + (File.Exists("iqiyicookie.txt") ? File.ReadAllText("iqiyicookie.txt").Trim() : ""));
                    string[] videoes = new Regex("\"video\"[\\s\\S]*").Match(webSource).Value.Replace("},{", "|").Split('|');
                    string size = "";
                    string m3u8Content = "";
                    string code = "";
                    string duration = "";
                    string scrsz = "";
                    string fileName = "";
                    string filePath = "";
                    foreach (var video in videoes)
                    {
                        if (video.Contains("\"_selected\":true"))
                        {
                            size = FormatFileSize(Convert.ToDouble(new Regex("\"vsize\":(\\d+)").Match(video).Groups[1].Value));
                            m3u8Content = new Regex("\"m3u8\":\"(.*?)\"").Match(video).Groups[1].Value.Replace("\\n", "\n").Replace("\\/", "/");
                            code = new Regex("\"code\":(\\d+)").Match(video).Groups[1].Value;
                            duration = FormatTime(Convert.ToInt32(new Regex("\"duration\":(\\d+)").Match(video).Groups[1].Value));
                            scrsz = new Regex("\"scrsz\":\"(.*?)\"").Match(video).Groups[1].Value;
                            fileName = title + "_" + scrsz + "_" + (code == "2" ? "H264" : "H265") + "_" + duration + "_" + size;
                            filePath = Path.Combine(Path.GetTempPath(), fileName + ".m3u8");
                            break;
                        }
                    }
                    File.WriteAllText(filePath, m3u8Content);
                    TextBox_URL.Text = filePath;
                    return GetValidFileName(fileName);
                }
                else if (url.StartsWith("cache.m.iqiyi.com"))
                {
                    string tvid = GetQueryString("tvid", url);
                    string webSource = GetWebSource($"https://pcw-api.iqiyi.com/video/video/baseinfo/{tvid}");
                    Regex rexTitle = new Regex("name\":\"(.*?)\"");
                    Regex rexDur = new Regex("duration\":\"(.*?)\"");
                    string title = rexTitle.Match(webSource).Groups[1].Value
                        + "_"
                        + rexDur.Match(webSource).Groups[1].Value;
                    //获得有效文件名
                    return GetValidFileName(title);
                }
                else if (url.Contains("ccode=") && url.Contains("vid="))
                {
                    string vid = GetQueryString("vid", url);
                    string webSource = GetWebSource($"https://openapi.youku.com/v2/videos/show.json?video_id={vid}&client_id=3d01f04416cbe807");
                    Regex rexTitle = new Regex("title\":\"(.*?)\"");
                    Regex rexDur = new Regex("duration\":\"(.*?)\"");
                    string type = GetQueryString("type", url);
                    string title = Unicode2String(rexTitle.Match(webSource).Groups[1].Value)
                        + "_"
                        + FormatTime((int)Convert.ToDouble(rexDur.Match(webSource).Groups[1].Value));
                    if (type != "")
                        title += "_" + type;
                    return GetValidFileName(title);
                }
                else if ((url.Contains(".ts.m3u8") || url.Contains(".mp4.m3u8")) && url.Contains("qq.com"))
                {
                    Regex rexVid = new Regex("\\/(\\w+).(\\d){6,}.*m3u8");
                    string match = rexVid.Match(url).Groups[1].Value;
                    string vid = "";
                    if (match.Contains("_"))
                        vid = match.Split('_')[1];
                    else
                        vid = match;

                    return GetValidFileName(GetQQTitle(vid));
                }
                else
                {
                    return GetUrlFileName(url);
                }
            }
            catch (Exception)
            {
                return DateTime.Now.ToString("yyyy.MM.dd-HH.mm.ss");
            }
        }

        public static string GetQQTitle(string vid)
        {
            Regex rexTitle1 = new Regex("\"title\":(.*?),");
            Regex rexDur = new Regex("duration\":\"(.*?)\"");
            string webSource = GetWebSource($"https://union.video.qq.com/fcgi-bin/data?tid=682&otype=json&appid=20001373&appkey=f6301da6035cd6cc&client=tim&idlist={vid}");
            string title = "";

            if (rexTitle1.Match(webSource).Groups[1].Value.Trim('\"') != "null")
            {
                string t = rexTitle1.Match(webSource).Groups[1].Value.Trim('\"');
                if (t.Contains("\\u"))
                    t = Unicode2String(t);
                title = t
                + "_"
                + FormatTime(Convert.ToInt32(rexDur.Match(webSource).Groups[1].Value));
                return title;
            }
            else
            {
                Regex rexTitle = new Regex("\"ti\":\"(.*?)\"");
                webSource = GetWebSource($"https://vv.video.qq.com/getinfo?otype=json&appver=3.4.40&platform=4830701&vid={vid}");
                title = rexTitle.Match(webSource).Groups[1].Value;
                if (title.Contains("\\u"))
                    title = Unicode2String(title);
                if (string.IsNullOrEmpty(title))
                {
                    rexTitle = new Regex("VIDEO_INFO.*\"title\":\"(.*?)\"");
                    webSource = new WebClient() { Encoding = Encoding.UTF8 }.DownloadString($"https://v.qq.com/x/page/{vid}.html");
                    title = rexTitle.Match(webSource).Groups[1].Value;
                    return title;
                }
                else
                    return title;
            }
        }

        //获取网页源码  
        private static string GetWebSource(String url, string headers = "", int TimeOut = 60000)
        {
            ServicePointManager.ServerCertificateValidationCallback = CertificateValidationCallback;
            //Init时执行，用于注册方法。
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Ssl3
                                       | SecurityProtocolType.Tls
                                       | (SecurityProtocolType)0x300 //Tls11  
                                       | (SecurityProtocolType)0xC00; //Tls12  
            string htmlCode = string.Empty;
            try
            {
                HttpWebRequest webRequest = (System.Net.HttpWebRequest)System.Net.WebRequest.Create(url);
                webRequest.Method = "GET";
                webRequest.UserAgent = "Mozilla/4.0";
                webRequest.Headers.Add("Accept-Encoding", "gzip, deflate");
                webRequest.Timeout = TimeOut;  //设置超时  
                webRequest.KeepAlive = false;
                //添加headers  
                if (headers != "")
                {
                    foreach (string att in headers.Split('|'))
                    {
                        try
                        {
                            if (att.Split(':')[0].ToLower() == "referer")
                                webRequest.Referer = att.Substring(att.IndexOf(":") + 1);
                            else if (att.Split(':')[0].ToLower() == "user-agent")
                                webRequest.UserAgent = att.Substring(att.IndexOf(":") + 1);
                            else if (att.Split(':')[0].ToLower() == "range")
                                webRequest.AddRange(Convert.ToInt32(att.Substring(att.IndexOf(":") + 1).Split('-')[0], Convert.ToInt32(att.Substring(att.IndexOf(":") + 1).Split('-')[1])));
                            else if (att.Split(':')[0].ToLower() == "accept")
                                webRequest.Accept = att.Substring(att.IndexOf(":") + 1);
                            else
                                webRequest.Headers.Add(att);
                        }
                        catch (Exception e)
                        {

                        }
                    }
                }
                HttpWebResponse webResponse = (HttpWebResponse)webRequest.GetResponse();
                if (webResponse.ContentEncoding != null
                    && webResponse.ContentEncoding.ToLower() == "gzip") //如果使用了GZip则先解压  
                {
                    using (Stream streamReceive = webResponse.GetResponseStream())
                    {
                        using (var zipStream =
                            new System.IO.Compression.GZipStream(streamReceive, System.IO.Compression.CompressionMode.Decompress))
                        {
                            using (StreamReader sr = new StreamReader(zipStream, Encoding.UTF8))
                            {
                                htmlCode = sr.ReadToEnd();
                            }
                        }
                    }
                }
                else
                {
                    using (Stream streamReceive = webResponse.GetResponseStream())
                    {
                        using (StreamReader sr = new StreamReader(streamReceive, Encoding.UTF8))
                        {
                            htmlCode = sr.ReadToEnd();
                        }
                    }
                }

                if (webResponse != null)
                {
                    webResponse.Close();
                }
                if (webRequest != null)
                {
                    webRequest.Abort();
                }
            }
            catch (Exception e)  //捕获所有异常  
            {

            }

            return htmlCode;
        }

        /// <summary>
        /// Unicode转字符串
        /// </summary>
        /// <param name="source">经过Unicode编码的字符串</param>
        /// <returns>正常字符串</returns>
        public static string Unicode2String(string source)
        {
            return new Regex(@"\\u([0-9A-F]{4})", RegexOptions.IgnoreCase | RegexOptions.Compiled).Replace(
                         source, x => string.Empty + Convert.ToChar(Convert.ToUInt16(x.Result("$1"), 16)));
        }

        /// <summary>    
        /// 获取url字符串参数，返回参数值字符串    
        /// </summary>    
        /// <param name="name">参数名称</param>    
        /// <param name="url">url字符串</param>    
        /// <returns></returns>    
        public string GetQueryString(string name, string url)
        {
            Regex re = new Regex(@"(^|&)?(\w+)=([^&]+)(&|$)?", System.Text.RegularExpressions.RegexOptions.Compiled);
            MatchCollection mc = re.Matches(url);
            foreach (Match m in mc)
            {
                if (m.Result("$2").Equals(name))
                {
                    return m.Result("$3");
                }
            }
            return "";
        }

        private void TextBox_Title_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (TextBox_URL.Text != "")
                TextBox_Title.Text = GetTitleFromURL(TextBox_URL.Text);
        }


        //寻找cookie字符串中的value
        public static string FindCookie(string key, string cookie)
        {
            string[] values = cookie.Split(';');
            string value = "";
            foreach (var v in values)
            {
                if (v.Trim().StartsWith(key + "="))
                    value = v.Remove(0, v.IndexOf('=') + 1).Trim();
            }
            return value;
        }

        //此函数用于格式化输出时长  
        public static String FormatTime(Int32 time)
        {
            TimeSpan ts = new TimeSpan(0, 0, time);
            string str = "";
            str = (ts.Hours.ToString("00") == "00" ? "" : ts.Hours.ToString("00") + ".") + ts.Minutes.ToString("00") + "." + ts.Seconds.ToString("00");
            return str;
        }

        //此函数用于格式化输出文件大小  
        public static String FormatFileSize(Double fileSize)
        {
            if (fileSize < 0)
            {
                throw new ArgumentOutOfRangeException("fileSize");
            }
            else if (fileSize >= 1024 * 1024 * 1024)
            {
                return string.Format("{0:########0.00}GB", ((Double)fileSize) / (1024 * 1024 * 1024));
            }
            else if (fileSize >= 1024 * 1024)
            {
                return string.Format("{0:####0.00}MB", ((Double)fileSize) / (1024 * 1024));
            }
            else if (fileSize >= 1024)
            {
                return string.Format("{0:####0.00}KB", ((Double)fileSize) / 1024);
            }
            else
            {
                return string.Format("{0}bytes", fileSize);
            }
        }

        public static string GetUrlFileName(string url)
        {
            if (string.IsNullOrEmpty(url))
            {
                return "None";
            }
            try
            {
                string[] strs1 = url.Split(new char[] { '/' });
                return GetValidFileName(System.Web.HttpUtility.UrlDecode(strs1[strs1.Length - 1].Split(new char[] { '?' })[0].Replace(".m3u8", "")));
            }
            catch (Exception)
            {
                return DateTime.Now.ToString("yyyy.MM.dd-HH.mm.ss");
            }
        }

        public static string GetValidFileName(string input, string re = ".")
        {
            string title = input;
            foreach (char invalidChar in Path.GetInvalidFileNameChars())
            {
                title = title.Replace(invalidChar.ToString(), re);
            }
            return title;
        }

        private void TextBox_URL_PreviewDragOver(object sender, System.Windows.DragEventArgs e)
        {
            e.Effects = DragDropEffects.Copy;
            e.Handled = true;
        }

        private void TextBox_URL_PreviewDragEnter(object sender, System.Windows.DragEventArgs e)
        {
            e.Effects = DragDropEffects.Copy;
            e.Handled = true;
        }

        private void TextBox_URL_PreviewDrop(object sender, System.Windows.DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop, false) == true)
            {
                //获取拖拽的文件地址
                var filenames = (string[])e.Data.GetData(DataFormats.FileDrop);
                var hz = filenames[0].LastIndexOf('.') + 1;
                var houzhui = filenames[0].Substring(hz).ToLower();//文件后缀名
                string path = ((System.Array)e.Data.GetData(DataFormats.FileDrop)).GetValue(0).ToString();
                if (houzhui == "m3u8" || houzhui == "txt" || houzhui == "json" || houzhui == "mpd") //只允许拖入部分文件
                {
                    e.Effects = DragDropEffects.Copy;
                    e.Handled = true;
                    if (TextBox_URL.Text != path) FlashTextBox(TextBox_URL);
                    TextBox_URL.Text = path; //将获取到的完整路径赋值到textBox1
                    if (houzhui == "m3u8" || houzhui == "json" || houzhui == "mpd")
                        TextBox_Title.Text = Path.GetFileNameWithoutExtension(path);  //自动获取文件名
                }
                if (Directory.Exists(path))
                {
                    if (TextBox_URL.Text != path) FlashTextBox(TextBox_URL);
                    TextBox_URL.Text = path;
                }
            }
        }

        private void TextBox_MuxJson_PreviewDragEnter(object sender, System.Windows.DragEventArgs e)
        {
            e.Effects = DragDropEffects.Copy;
            e.Handled = true;
        }

        private void TextBox_MuxJson_PreviewDragOver(object sender, System.Windows.DragEventArgs e)
        {
            e.Effects = DragDropEffects.Copy;
            e.Handled = true;
        }

        private void TextBox_MuxJson_PreviewDrop(object sender, System.Windows.DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop, false) == true)
            {
                //获取拖拽的文件地址
                var filenames = (string[])e.Data.GetData(DataFormats.FileDrop);
                var hz = filenames[0].LastIndexOf('.') + 1;
                var houzhui = filenames[0].Substring(hz).ToLower();//文件后缀名
                string path = ((System.Array)e.Data.GetData(DataFormats.FileDrop)).GetValue(0).ToString();
                if (houzhui == "json") //只允许拖入部分文件
                {
                    e.Effects = DragDropEffects.Copy;
                    e.Handled = true;
                    TextBox_MuxJson.Text = path; //将获取到的完整路径赋值到textBox1
                }
            }
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            //string  to  base64
            Encoding encode = Encoding.UTF8;
            byte[] bytedata = encode.GetBytes(TextBox_EXE.Text);
            string exePath = Convert.ToBase64String(bytedata, 0, bytedata.Length);
            bytedata = encode.GetBytes(TextBox_WorkDir.Text);
            string saveDir = Convert.ToBase64String(bytedata, 0, bytedata.Length);
            bytedata = encode.GetBytes(TextBox_Proxy.Text);
            string proxy = Convert.ToBase64String(bytedata, 0, bytedata.Length);
            bytedata = encode.GetBytes(TextBox_Headers.Text);
            string headers = Convert.ToBase64String(bytedata, 0, bytedata.Length);

            string config = "程序路径=" + exePath
                + ";保存路径=" + saveDir
                + ";代理=" + proxy
                + ";请求头=" + headers
                + ";删除临时文件=" + (CheckBox_Del.IsChecked == true ? "1" : "0")
                + ";MP4混流边下边看=" + (CheckBox_FastStart.IsChecked == true ? "1" : "0")
                + ";二进制合并=" + (CheckBox_BinaryMerge.IsChecked == true ? "1" : "0")
                + ";仅解析模式=" + (CheckBox_ParserOnly.IsChecked == true ? "1" : "0")
                + ";不写入日期=" + (CheckBox_DisableDate.IsChecked == true ? "1" : "0")
                + ";最大线程=" + TextBox_Max.Text
                + ";最小线程=" + TextBox_Min.Text
                + ";重试次数=" + TextBox_Retry.Text
                + ";超时秒数=" + TextBox_Timeout.Text
                + ";停止速度=" + TextBox_StopSpeed.Text
                + ";最大速度=" + TextBox_MaxSpeed.Text
                + ";不合并=" + (CheckBox_DisableMerge.IsChecked == true ? "1" : "0")
                + ";不使用系统代理=" + (CheckBox_DisableProxy.IsChecked == true ? "1" : "0")
                + ";仅合并音频=" + (CheckBox_AudioOnly.IsChecked == true ? "1" : "0");
            File.WriteAllText("config.txt", config);

            //关闭任务
            Helper.cts.Cancel();
            //Process.GetCurrentProcess().Kill();

        }

        //private void Window_Closed(object sender, System.ComponentModel.CancelEventArgs e)
        //{
        //    //退出环境。
        //    Application.Current.Shutdown();

        //}


        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Environment.CurrentDirectory = Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName);
            //读取配置
            if (File.Exists("config.txt"))
            {
                string config = File.ReadAllText("config.txt");

                TextBox_EXE.Text = Encoding.UTF8.GetString(Convert.FromBase64String(FindCookie("程序路径", config)));
                TextBox_WorkDir.Text = Encoding.UTF8.GetString(Convert.FromBase64String(FindCookie("保存路径", config)));
                try
                {
                    TextBox_Proxy.Text = Encoding.UTF8.GetString(Convert.FromBase64String(FindCookie("代理", config)));
                }
                catch (Exception) {; }
                try
                {
                    TextBox_Headers.Text = Encoding.UTF8.GetString(Convert.FromBase64String(FindCookie("请求头", config)));
                }
                catch (Exception) {; }
                if (FindCookie("删除临时文件", config) == "1")
                    CheckBox_Del.IsChecked = true;
                if (FindCookie("MP4混流边下边看", config) == "1")
                    CheckBox_FastStart.IsChecked = true;
                if (FindCookie("二进制合并", config) == "1")
                    CheckBox_BinaryMerge.IsChecked = true;
                if (FindCookie("仅解析模式", config) == "1")
                    CheckBox_ParserOnly.IsChecked = true;
                if (FindCookie("不写入日期", config) == "1")
                    CheckBox_DisableDate.IsChecked = true;
                TextBox_Max.Text = FindCookie("最大线程", config);
                TextBox_Min.Text = FindCookie("最小线程", config);
                TextBox_Retry.Text = FindCookie("重试次数", config);
                try
                {
                    if (!string.IsNullOrEmpty(FindCookie("超时秒数", config)))
                        TextBox_Timeout.Text = FindCookie("超时秒数", config);
                }
                catch (Exception) {; }
                try
                {
                    if (!string.IsNullOrEmpty(FindCookie("停止速度", config)))
                        TextBox_StopSpeed.Text = FindCookie("停止速度", config);
                }
                catch (Exception) {; }
                try
                {
                    if (!string.IsNullOrEmpty(FindCookie("最大速度", config)))
                        TextBox_MaxSpeed.Text = FindCookie("最大速度", config);
                }
                catch (Exception) {; }
                try
                {
                    if (FindCookie("不合并", config) == "1")
                        CheckBox_DisableMerge.IsChecked = true;
                    if (FindCookie("不使用系统代理", config) == "1")
                        CheckBox_DisableProxy.IsChecked = true;
                }
                catch (Exception) {; }
                try
                {
                    if (FindCookie("仅合并音频", config) == "1")
                        CheckBox_AudioOnly.IsChecked = true;
                }
                catch (Exception) {; }
            }

            if (!File.Exists(TextBox_EXE.Text))//尝试寻找主程序
            {
                DirectoryInfo d = new DirectoryInfo(Environment.CurrentDirectory);
                foreach (FileInfo fi in d.GetFiles().Reverse()) 
                {
                    if (fi.Extension.ToUpper() == ".exe".ToUpper() && fi.Name.StartsWith("N_m3u8DL-CLI_"))
                    {
                        TextBox_EXE.Text = fi.Name;
                    }
                }
            }

            if (Environment.GetCommandLineArgs().Length > 1)
            {
                var ext = Path.GetExtension(Environment.GetCommandLineArgs()[1]);
                if (ext == ".m3u8" || ext == ".json" || ext == ".txt" || Directory.Exists(Environment.GetCommandLineArgs()[1]))
                    TextBox_URL.Text = Environment.GetCommandLineArgs()[1];
                if (TextBox_URL.Text != "")
                {
                    FlashTextBox(TextBox_URL);
                    if (!Directory.Exists(TextBox_URL.Text))
                        TextBox_Title.Text = GetTitleFromURL(TextBox_URL.Text);
                }
            }
            else
            {
                //从剪切板读取url
                Regex url = new Regex(@"(https?)://[-A-Za-z0-9+&@#/%?=~_|!:,.;]+[-A-Za-z0-9+&@#/%=~_|]", RegexOptions.Compiled | RegexOptions.Singleline);
                string str = url.Match(Clipboard.GetText()).Value;
                TextBox_URL.Text = str;
                if (TextBox_URL.Text != "")
                {
                    FlashTextBox(TextBox_URL);
                    TextBox_Title.Text = GetTitleFromURL(TextBox_URL.Text);
                }
            }
        }

        private void Button_GO_Click(object sender, RoutedEventArgs e)
        {
            //hex to base64
            try
            {
                TextBox_Key.Text = Convert.ToBase64String(HexStringToBytes(TextBox_Key.Text));
            }
            catch(Exception) {

            }
            if (!File.Exists(TextBox_EXE.Text))
            {
                MessageBox.Show(Properties.Resources.String2);
                return;
            }
            if (TextBox_URL.Text == "")
            {
                MessageBox.Show(Properties.Resources.String3);
                return;
            }
            if (TextBox_Proxy.Text != "" && (!TextBox_Proxy.Text.StartsWith("http://") && !TextBox_Proxy.Text.StartsWith("socks5://")))
            {
                MessageBox.Show(Properties.Resources.String7);
                return;
            }

            //批量
            if ((!TextBox_URL.Text.StartsWith("http") && TextBox_URL.Text.EndsWith(".txt") && File.Exists(TextBox_URL.Text))
                || Directory.Exists(TextBox_URL.Text))
            {
                this.IsEnabled = false;
                Button_GO.Content = Properties.Resources.String4;
                string inputUrl = TextBox_URL.Text;
                string exePath = TextBox_EXE.Text;
                List<string> m3u8list = new List<string>();
                if (Directory.Exists(inputUrl))
                {
                    foreach (var file in Directory.GetFiles(inputUrl))
                    {
                        if (new FileInfo(file).Name.ToLower().EndsWith(".m3u8") || new FileInfo(file).Name.ToLower().EndsWith(".mpd"))
                        {
                            m3u8list.Add(new FileInfo(file).FullName);
                        }
                    }
                    StringBuilder sb = new StringBuilder();
                    sb.AppendLine("@echo off");
                    sb.AppendLine("::Created by N_m3u8DL-CLI-SimpleG\r\n");
                    //sb.AppendLine("chcp 65001 >nul");
                    int i = 0;
                    foreach (var item in m3u8list)
                    {
                        TextBox_Title.Text = GetTitleFromURL(item);
                        sb.AppendLine($"TITLE \"[{++i}/{m3u8list.Count}] - {TextBox_Title.Text}\"");
                        sb.AppendLine("\"" + exePath + "\" \"" + item.Replace("%", "%%") + "\" " + TextBox_Parameter.Text.Remove(0, TextBox_Parameter.Text.IndexOf("\" ") + 2));
                    }
                    //sb.AppendLine("del %0");
                    string bat = "Batch-" + DateTime.Now.ToString("yyyy.MM.dd-HH.mm.ss") + ".bat";
                    File.WriteAllText(bat,
                        sb.ToString(),
                        Encoding.Default);
                    Process.Start(bat);
                }
                else
                {
                    m3u8list = File.ReadAllLines(inputUrl, GetType(inputUrl)).ToList();
                    StringBuilder sb = new StringBuilder();
                    sb.AppendLine("@echo off");
                    sb.AppendLine("::Created by N_m3u8DL-CLI-SimpleG");
                    //sb.AppendLine("chcp 65001 >nul");
                    int i = 0;
                    foreach (var item in m3u8list)
                    {
                        if (item.Trim() != "")
                        {
                            if (item.StartsWith("http"))
                            {
                                TextBox_Title.Text = GetTitleFromURL(item);
                                sb.AppendLine($"TITLE \"[{++i}/{m3u8list.Count}] - {TextBox_Title.Text}\"");
                                sb.AppendLine("\"" + exePath + "\" \"" + item.Replace("%", "%%") + "\" " + TextBox_Parameter.Text.Remove(0, TextBox_Parameter.Text.IndexOf("\" ") + 2));
                            }
                            //自定义文件名
                            else
                            {
                                TextBox_Title.Text = item.Substring(0, item.IndexOf(",http"));
                                sb.AppendLine($"TITLE \"[{++i}/{m3u8list.Count}] - {TextBox_Title.Text}\"");
                                sb.AppendLine("\"" + exePath + "\" \"" + item.Replace(TextBox_Title.Text + ",", "").Replace("%", "%%") + "\" " + TextBox_Parameter.Text.Remove(0, TextBox_Parameter.Text.IndexOf("\" ") + 2));
                            }
                        }
                    }
                    //sb.AppendLine("del %0");
                    string bat = "Batch-" + DateTime.Now.ToString("yyyy.MM.dd-HH.mm.ss") + ".bat";
                    File.WriteAllText(bat,
                        sb.ToString(),
                        Encoding.Default);
                    Process.Start(bat);
                }

                Button_GO.Content = "GO";
                this.IsEnabled = true;
            }
            else
            {
                Button_GO.IsEnabled = false;
                Process.Start(TextBox_EXE.Text, TextBox_Parameter.Text);
                Button_GO.IsEnabled = true;
            }
        }


        private void TextBox_URL_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
                Button_GO.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
        }

        private void TextBox_Title_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
                Button_GO.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
        }

        private void SetTopMost(object sender, RoutedEventArgs e)
        {
            if (CheckBox_TopMost.IsChecked == true) 
            {
                Topmost = true;
            }
            else
            {
                Topmost = false;
            }
        }

        private void Menu_GetDownloader(object sender, RoutedEventArgs e)
        {
            Process.Start("https://github.com/nilaoda/N_m3u8DL-CLI/releases");
        }

        /// <summary> 
        /// 给定文件的路径，读取文件的二进制数据，判断文件的编码类型 
        /// </summary> 
        /// <param name=“FILE_NAME“>文件路径</param> 
        /// <returns>文件的编码类型</returns> 
        public static Encoding GetType(string FILE_NAME)
        {
            FileStream fs = new FileStream(FILE_NAME, FileMode.Open, FileAccess.Read);
            Encoding r = GetType(fs);
            fs.Close();
            return r;
        }

        /// <summary> 
        /// 通过给定的文件流，判断文件的编码类型 
        /// </summary> 
        /// <param name=“fs“>文件流</param> 
        /// <returns>文件的编码类型</returns> 
        public static Encoding GetType(FileStream fs)
        {
            byte[] Unicode = new byte[] { 0xFF, 0xFE, 0x41 };
            byte[] UnicodeBIG = new byte[] { 0xFE, 0xFF, 0x00 };
            byte[] UTF8 = new byte[] { 0xEF, 0xBB, 0xBF }; //带BOM 
            Encoding reVal = Encoding.Default;

            BinaryReader r = new BinaryReader(fs, System.Text.Encoding.Default);
            int i;
            int.TryParse(fs.Length.ToString(), out i);
            byte[] ss = r.ReadBytes(i);
            if (IsUTF8Bytes(ss) || (ss[0] == 0xEF && ss[1] == 0xBB && ss[2] == 0xBF))
            {
                reVal = Encoding.UTF8;
            }
            else if (ss[0] == 0xFE && ss[1] == 0xFF && ss[2] == 0x00)
            {
                reVal = Encoding.BigEndianUnicode;
            }
            else if (ss[0] == 0xFF && ss[1] == 0xFE && ss[2] == 0x41)
            {
                reVal = Encoding.Unicode;
            }
            r.Close();
            return reVal;
        }

        /// <summary> 
        /// 判断是否是不带 BOM 的 UTF8 格式 
        /// </summary> 
        /// <param name=“data“></param> 
        /// <returns></returns> 
        private static bool IsUTF8Bytes(byte[] data)
        {
            int charByteCounter = 1; //计算当前正分析的字符应还有的字节数 
            byte curByte; //当前分析的字节. 
            for (int i = 0; i < data.Length; i++)
            {
                curByte = data[i];
                if (charByteCounter == 1)
                {
                    if (curByte >= 0x80)
                    {
                        //判断当前 
                        while (((curByte <<= 1) & 0x80) != 0)
                        {
                            charByteCounter++;
                        }
                        //标记位首位若为非0 则至少以2个1开始 如:110XXXXX...........1111110X 
                        if (charByteCounter == 1 || charByteCounter > 6)
                        {
                            return false;
                        }
                    }
                }
                else
                {
                    //若是UTF-8 此时第一位必须为1 
                    if ((curByte & 0xC0) != 0x80)
                    {
                        return false;
                    }
                    charByteCounter--;
                }
            }
            if (charByteCounter > 1)
            {
                throw new Exception(Properties.Resources.String5);
            }
            return true;
        }

        private void TextBox_Key_PreviewDragEnter(object sender, System.Windows.DragEventArgs e)
        {
            e.Effects = DragDropEffects.Copy;
            e.Handled = true;
        }

        private void TextBox_Key_PreviewDragOver(object sender, System.Windows.DragEventArgs e)
        {
            e.Effects = DragDropEffects.Copy;
            e.Handled = true;
        }

        private void TextBox_Key_PreviewDrop(object sender, System.Windows.DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop, false) == true)
            {
                //获取拖拽的文件地址
                string path = ((System.Array)e.Data.GetData(DataFormats.FileDrop)).GetValue(0).ToString();
                e.Effects = DragDropEffects.Copy;
                e.Handled = true;
                if (new FileInfo(path).Length == 16)
                    TextBox_Key.Text = path; //将获取到的完整路径赋值到textBox1
                else
                    MessageBox.Show(Properties.Resources.String6);
            }
        }

        private void AddTaskToList(M3u8TaskItem a_task)
        {
            bool find_exist = false;

            //查找列表中是否已经存在同名的任务
            foreach (M3u8TaskItem item in Helper.m3u8_tasks)
            {
                //查找同名任务
                if (item.Name == a_task.Name)
                {
                    //检查任务状态
                    if (string.IsNullOrEmpty(item.Status))
                    {
                        //可以重设任务，而不需要添加新任务
                        item.M3u8Url = a_task.M3u8Url;
                        item.PageUrl = a_task.PageUrl;
                        item.Parameter = a_task.Parameter;
                        find_exist = true;
                        MessageBox.Show("找到了同名任务，已更新其参数");
                        break;
                    }
                }
            }



            if (!find_exist)
            {
                //没有找到存在的任务，才添加新任务
                Helper.m3u8_tasks.Add(a_task);
            }

            //把列表内容，保存到文件
            try
            {
                Helper.listFileIO.Save(Helper.m3u8_tasks);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"保存任务列表到文件时出错：{ex.Message}");
            }

            try
            {
                lbTaskList.Items.Refresh();
            }
            catch (Exception e)
            {
                Debug.WriteLine($"遇到错误：{e.Message}");
                Debug.WriteLine("改为用this.Dispatcher.BeginInvoke调用控件");
                //线程中，不能调用UI控件，只能这样间接调用
                //不然会报错：调用线程无法访问此对象 因为另一个线程拥有该对象
                //参考：https://www.yuantk.com/weblog/dee39674-c663-4e31-bf81-523518165a78.html
                //在创建控件的基础句柄所在线程上异步执行指定委托。
                this.Dispatcher.BeginInvoke(new Action(() =>
                {
                    lbTaskList.Items.Refresh();
                }));
            }


        }

        /// <summary>
        /// 添加任务到列表
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void AddToList_Click(object sender, RoutedEventArgs e)
        {
            M3u8TaskItem a_task = new M3u8TaskItem(TextBox_Title.Text, TextBox_URL.Text, TextBox_PageUrl.Text, TextBox_Parameter.Text);

            AddTaskToList(a_task);

        }

        /// <summary>
        /// 向LogBlock控件写入一行下载日志
        /// </summary>
        /// <param name="line"></param>
        private void WriteLogBlockLine(string line)
        {
            line = line.Trim() + Environment.NewLine;

            //TextBlock是性能杀手，每次都要重新绘制整个输入框，内容多了性能下降难以相像。
            //限制行数，避免无限增长导致卡顿
            int lineCount = LogBlock.Text.Count(c => c == '\n');
            if (lineCount > 100)
            {
                ClearLogBlock();
            }

            try
            {
                
                LogBlock.Text = line + LogBlock.Text;
            }
            catch (Exception e)
            {
                //Debug.WriteLine($"更新LogBlock控件遇到错误：{e.Message}");
                //Debug.WriteLine("改为用this.Dispatcher.BeginInvoke调用控件");
                //线程中，不能调用UI控件，只能这样间接调用
                //不然会报错：调用线程无法访问此对象 因为另一个线程拥有该对象
                //参考：https://www.yuantk.com/weblog/dee39674-c663-4e31-bf81-523518165a78.html
                //在创建控件的基础句柄所在线程上异步执行指定委托。
                this.Dispatcher.BeginInvoke(new Action(() =>
                {
                    LogBlock.Text = line + LogBlock.Text;
                }));
            }

        }


        /// <summary>
        /// 清理LogBlock控件内容
        /// </summary>
        /// <param name="line"></param>
        private void ClearLogBlock()
        {
            try
            {
                LogBlock.Text = "";
            }
            catch (Exception e)
            {
                //Debug.WriteLine($"更新LogBlock控件遇到错误：{e.Message}");
                //Debug.WriteLine("改为用this.Dispatcher.BeginInvoke调用控件");
                //线程中，不能调用UI控件，只能这样间接调用
                //不然会报错：调用线程无法访问此对象 因为另一个线程拥有该对象
                //参考：https://www.yuantk.com/weblog/dee39674-c663-4e31-bf81-523518165a78.html
                //在创建控件的基础句柄所在线程上异步执行指定委托。
                this.Dispatcher.BeginInvoke(new Action(() =>
                {
                    LogBlock.Text = "";
                }));
            }

        }


        /// <summary>
        /// 异步启动下载进程，并触发进程完成的事件。这样可以在进程完成的事件中，做后续的事情
        /// </summary>
        /// <param name="fileName"></param>
        /// <param name="arguments"></param>
        /// <returns></returns>
        private async Task StartDownloadProcessAsync(string fileName, string arguments)
        {
            m3u8dlEventHandled = new TaskCompletionSource<bool>();

            using (m3u8dlProcess = new Process())
            {
                try
                {
                    // Start a process to print a file and raise an event when done.
                    m3u8dlProcess.StartInfo.FileName = fileName;
                    m3u8dlProcess.StartInfo.Arguments = arguments;
                    m3u8dlProcess.StartInfo.UseShellExecute = false;        //是否使用操作系统shell启动
                    m3u8dlProcess.StartInfo.CreateNoWindow = true;          //不显示程序窗口
                    m3u8dlProcess.StartInfo.RedirectStandardOutput = true;  //由调用程序获取输出信息
                    //m3u8dlProcess.StartInfo.StandardOutputEncoding = Encoding.UTF8;
                    //m3u8dlProcess.StartInfo.RedirectStandardError = true;   //重定向标准错误输出
                    //m3u8dlProcess.StartInfo.StandardErrorEncoding = Encoding.UTF8;
                    //m3u8dlProcess.StartInfo.RedirectStandardInput = true;   //接受来自调用程序的输入信息
                    //m3u8dlProcess.StandardInput.AutoFlush = true;
                    m3u8dlProcess.EnableRaisingEvents = true; //要求引发Exited事件
                    m3u8dlProcess.Exited += new EventHandler(DownloadProcess_Exited);
                    m3u8dlProcess.Start(); //启动

                }
                catch (Exception e)
                {
                    MessageBox.Show($"调用下载进程出错:{e.Message}");
                    return;
                }

                // 等待后台进程Log
                try
                {
                    //获取输出
                    logBlockReader = m3u8dlProcess.StandardOutput;
                    // 每次读取一行
                    string line = "";
                    while (logBlockReader != null && !logBlockReader.EndOfStream)
                    {
                        line = await logBlockReader.ReadLineAsync();
                        WriteLogBlockLine(line);

                        //检查内容是不是地址无效
                        if (line.Contains("地址无效")) 
                        {
                            //要终止写入，并停止这个任务的下载
                            StopTask("Failed");
                            break;
                        }
                    }
                }
                catch (Exception e)
                {
                    MessageBox.Show($"读取后台下载进程Log出错:{e.Message}");
                    return;
                }

                // 等待完成事件
                try 
                {
                    await Task.WhenAny(m3u8dlEventHandled.Task);
                }
                catch (Exception e)
                {
                    MessageBox.Show($"等待进程完成出错:{e.Message}");
                    return;
                }



            }
        }

        /// <summary>
        /// 处理进程完成的事件。这里就是打开下一个下载任务
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void DownloadProcess_Exited(object sender, System.EventArgs e)
        {
            try
            {
                m3u8dlEventHandled.TrySetResult(true);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"设置下载进程为完成时出错:{ex.Message}");
                return;
            }

        }

        /// <summary>
        /// 获取下一个需要下载的任务，不存在，就返回null
        /// </summary>
        /// <returns></returns>
        private M3u8TaskItem GetNextTaskToDownload()
        {
            //遍历任务列表，查找下一个需要下载的任务
            foreach (M3u8TaskItem item in Helper.m3u8_tasks)
            {
                //检查当前遍历到的任务状态
                //状态是空，说明这个任务还没有下载过
                if (string.IsNullOrEmpty(item.Status) || item.Status == "Stopped")
                {
                    //检查参数
                    if (string.IsNullOrEmpty(item.Parameter))
                    {
                        item.Status = "No Parameter";
                        continue;
                    }
                    else
                    {
                        //找到需要下载的任务，可以返回
                        return item;
                    }
                }
            }

            return null;
        }


        /// <summary>
        /// 启动任务列表，异步
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void TaskStartAsync_Click(object sender, RoutedEventArgs e)
        {
            //如果已经在下载，就什么也不做
            if (this.tasks_status == "Downloading")
            {
                return;
            }
            else if (this.tasks_status == "Stopped")
            {
                this.tasks_status = "";
            }

            //定义要下载的任务
            M3u8TaskItem item = GetNextTaskToDownload();

            //在下载期间，用户可能继续添加任务到列表，所以列表长度是动态变化的
            //因此，不能简单用foreach遍历列表，会报错：集合已修改 可能无法执行枚举操作
            //解决办法是，单独遍历任务列表，获取要下载的任务。
            //再把下载放在while循环中，然后每完成一个任务，都重新检查列表，判断是否需要继续
            while (item != null)
            {
                //检查任务列表状态
                if (this.tasks_status == "Stopped")
                {
                    this.tasks_status = "";
                    break;
                }
                else if (item.Status == "Downloading")
                {
                    //退出循环什么也不做
                    break;
                }

                //清理LogBlock
                ClearLogBlock();

                //修改任务状态
                this.tasks_status = "Downloading";
                item.Status = "Downloading";
                lbTaskList.Items.Refresh();

                //启动异步下载
                try
                {
                    await StartDownloadProcessAsync(TextBox_EXE.Text, item.Parameter);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"运行异步下载出错:{ex.Message}");
                    return;
                }


                //再次修改任务状态
                if (item.Status == "Downloading")
                {
                    item.Status = "Done";
                    //把列表内容，保存到文件
                    try
                    {
                        Helper.listFileIO.Save(Helper.m3u8_tasks);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"保存任务列表到文件时出错：{ex.Message}");
                    }
                    lbTaskList.Items.Refresh();
                }

                //获取下一个任务
                item = GetNextTaskToDownload();
            }

            //设置任务状态
            this.tasks_status = "";


        }


        private void StopTask(string new_status)
        {
            //改变任务列表状态
            this.tasks_status = "Stopped";

            //遍历任务列表，查找当前任务
            foreach (M3u8TaskItem item in Helper.m3u8_tasks)
            {
                //检查任务状态
                if (item.Status == "Downloading")
                {
                    //修改任务状态
                    item.Status = new_status;
                }
            }

            //终止logBlockReader写入
            if (logBlockReader != null)
            {
                try
                {
                    logBlockReader.Close();
                    logBlockReader = null;
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"中止下载Log读取器时出错：{ex.Message}");
                }
            }

            //终止正在进行的下载进程
            if (m3u8dlProcess != null)
            {
                try
                {
                    m3u8dlProcess.Kill();
                    m3u8dlProcess.WaitForExit();
                    m3u8dlProcess.Close();
                    m3u8dlProcess = null;
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"中止下载进程出错：{ex.Message}");
                }
            }


            //把列表内容，保存到文件
            try 
            {
                Helper.listFileIO.Save(Helper.m3u8_tasks);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"保存任务列表到文件时出错：{ex.Message}");
            }

            try
            {
                //刷新任务列表
                lbTaskList.Items.Refresh();
            }
            catch (Exception e)
            {
                Debug.WriteLine($"遇到错误：{e.Message}");
                Debug.WriteLine("改为用this.Dispatcher.BeginInvoke调用控件");
                //线程中，不能调用UI控件，只能这样间接调用
                //不然会报错：调用线程无法访问此对象 因为另一个线程拥有该对象
                //参考：https://www.yuantk.com/weblog/dee39674-c663-4e31-bf81-523518165a78.html
                //在创建控件的基础句柄所在线程上异步执行指定委托。
                this.Dispatcher.BeginInvoke(new Action(() =>
                {
                    lbTaskList.Items.Refresh();
                }));
            }

        }


        /// <summary>
        /// 停止任务列表。因为命令是发送给cmd调用其他下载工具的，所以并不能真的停止。这里只是简单的修改任务状态。
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void TaskStop_Click(object sender, RoutedEventArgs e)
        {
            StopTask("Stopped");
        }


        /// <summary>
        /// 重置当前选择的任务的状态。
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void TaskReset_Click(object sender, RoutedEventArgs e)
        {

            if(this.selected_task is null) { return; }

            this.selected_task.Status = "";

            lbTaskList.Items.Refresh();
        }


        /// <summary>
        /// 移除当前选择的任务。
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void TaskRemove_Click(object sender, RoutedEventArgs e)
        {

            if (this.selected_task is null) { return; }

            Helper.m3u8_tasks.Remove(this.selected_task);
            //把列表内容，保存到文件
            try
            {
                Helper.listFileIO.Save(Helper.m3u8_tasks);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"保存任务列表到文件时出错：{ex.Message}");
            }

            lbTaskList.Items.Refresh();
        }

        /// <summary>
        /// 移除已经完成的任务，不会移除下载的文件。
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void TaskRemoveDownloaded_Click(object sender, RoutedEventArgs e)
        {
            foreach (M3u8TaskItem item in Helper.m3u8_tasks.ToList())
            {
                //检查任务状态
                if (item.Status == "Done" || item.Status == "No Parameter")
                {
                    Helper.m3u8_tasks.Remove(item);
                }
            }

            //把列表内容，保存到文件
            try
            {
                Helper.listFileIO.Save(Helper.m3u8_tasks);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"保存任务列表到文件时出错：{ex.Message}");
            }

            lbTaskList.Items.Refresh();

        }

        /// <summary>
        /// 移除全部任务。
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void TaskClear_Click(object sender, RoutedEventArgs e)
        {

            Helper.m3u8_tasks.Clear();
            //把列表内容，保存到文件
            try
            {
                Helper.listFileIO.Save(Helper.m3u8_tasks);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"保存任务列表到文件时出错：{ex.Message}");
            }

            lbTaskList.Items.Refresh();
        }

        /// <summary>
        /// 打开当前选择的任务对应的网页，便于重新获取新的m3u8
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void TaskOpenUrl_Click(object sender, RoutedEventArgs e)
        {

            if (this.selected_task is null) { return; }

            if (string.IsNullOrEmpty(this.selected_task.PageUrl))
            {
                MessageBox.Show("这个任务没有设置页面地址");
                return;
            }

            if (!Helper.ValidateUrlWithRegex(this.selected_task.PageUrl))
            {
                MessageBox.Show($"这个任务的页面地址不是网页: {this.selected_task.PageUrl}");
                return;
            }

            Process.Start(this.selected_task.PageUrl);
        }



        /// <summary>
        /// 在任务列表上点击了一个任务时触发。
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SelectionChanged_Click(object sender, RoutedEventArgs e)
        {
            //设置当前选择的任务为当前任务
            this.selected_task = ((sender as System.Windows.Controls.ListBox).SelectedItem as M3u8TaskItem);

            if (!(this.selected_task is null)) 
            {
                //把当前任务的内容，填充到左边的表格界面中
                TextBox_Title.Text = this.selected_task.Name;
                TextBox_URL.Text = this.selected_task.M3u8Url;
                TextBox_PageUrl.Text = this.selected_task.PageUrl;
                TextBox_Parameter.Text = this.selected_task.Parameter;
            }

        }

        /// <summary>
        /// 注册m3u8dl协议。
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void RegisterUrlProtocol(object sender, RoutedEventArgs e)
        {
            string[] args = new string[1];
            args[0] = "--registerUrlProtocol";

            string result = Helper.RegisterUriScheme("m3u8dl", Assembly.GetExecutingAssembly().Location);

            MessageBox.Show("注册协议要管理员权限。操作结果："+ result);
        }

        /// <summary>
        /// 取消注册m3u8dl协议。
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void UnregisterUrlProtocol(object sender, RoutedEventArgs e)
        {
            string[] args = new string[1];
            args[0] = "--unregisterUrlProtocol";

            string result = Helper.UnregisterUriScheme("m3u8dl");

            MessageBox.Show("注销协议要管理员权限。操作结果："+result);
        }

        /// <summary>
        /// 处理参数
        /// </summary>
        private void HandleArgs()
        {
            string[] args = Helper.Args;

            if (args is null)
            {
                return;
            }

            if (args.Length < 1) 
            {
                return;
            }

            if (args.Length > 1)
            {
                MessageBox.Show("运行参数长度大于1，无法识别");
                return;
            }

            if (!args[0].ToLower().StartsWith("m3u8dl:"))
            {
                MessageBox.Show("运行参数开头不是m3u8dl，无法识别");
                return;
            }

            //MessageBox.Show($"运行参数为：{args[0]}");

            string base64 = args[0].Replace("m3u8dl://", "").Replace("m3u8dl:", "").Trim();
            base64 = base64.TrimEnd('/');
            string cmd = "";

            //MessageBox.Show($"base64格式运行参数为：{base64}");

            try 
            {
                cmd = Encoding.UTF8.GetString(Convert.FromBase64String(base64)); 
            }
            catch (Exception e) 
            {
                MessageBox.Show($"格式异常：{e.Message}");
                try
                {
                    cmd = Encoding.UTF8.GetString(Convert.FromBase64String(base64.TrimEnd('/')));
                }
                catch (Exception e2)
                {
                    MessageBox.Show($"格式异常2：{e2.Message}");
                    return;
                }
            }

            //MessageBox.Show($"cmd为：{cmd}");

            //修正参数转义符
            cmd = cmd.Replace("\\\"", "\"");

            args = Helper.ParseArguments(cmd).ToArray();  //解析命令行

            //MessageBox.Show($"解析得到的参数为：{string.Join(", ", args)}");
            //MessageBox.Show($"参数列表长度为：{args.Length}");


            //根据参数，创建对应的M3u8TaskItem
            string m3u8Url = args[0];
            string name = "";
            string pageUrl = "";
            string workDir = "";
            string headers = "";
            string proxyAddress = "";

            int name_index = Array.IndexOf(args, "--saveName") + 1;
            int pageUrl_index = Array.IndexOf(args, "--pageUrl") + 1;
            int workDir_index = Array.IndexOf(args, "--workDir") + 1;
            int headers_index = Array.IndexOf(args, "--headers") + 1;
            int proxyAddress_index = Array.IndexOf(args, "--proxyAddress") + 1;



            //参数内容同时更新到界面
            if (name_index > 0)
            {
                name = args[name_index];
            }
            else 
            {
                MessageBox.Show($"无法从参数列表获取文件名：{cmd}");
                return;
            }

            if (pageUrl_index > 0)
            {
                //MessageBox.Show($"更新前cmd: {cmd}");
                pageUrl = args[pageUrl_index];
                //N_m3u8DL-CLI无法识别pageUrl所以要重新构建参数
                cmd = "";
                for (int i = 0; i < args.Length; i++) 
                {
                    if (i == pageUrl_index || i == pageUrl_index - 1) { continue; }

                    if (args[i].StartsWith("--"))
                    {
                        cmd += " " + args[i];
                    }
                    else 
                    {
                        cmd += $" \"{args[i]}\"";
                    }

                }
                //MessageBox.Show($"更新后cmd: {cmd}");
            }

            if (workDir_index > 0)
            {
                workDir = args[workDir_index];
            }

            if (headers_index > 0)
            {
                headers = args[headers_index];
            }

            if (proxyAddress_index > 0)
            {
                proxyAddress = args[proxyAddress_index];
            }

            //准备添加到列表
            M3u8TaskItem a_task = new M3u8TaskItem(name, m3u8Url, pageUrl, cmd);

            try 
            {
                TextBox_Title.Text = name;
                TextBox_WorkDir.Text = workDir;
                TextBox_URL.Text = m3u8Url;
                TextBox_PageUrl.Text = pageUrl;
                TextBox_Headers.Text = headers;
                TextBox_Proxy.Text = proxyAddress;
                TextBox_Parameter.Text = cmd;

                //添加到列表
                AddTaskToList(a_task);
            }
            catch(Exception e) 
            {
                Debug.WriteLine($"遇到错误：{e.Message}");
                Debug.WriteLine("改为用this.Dispatcher.BeginInvoke调用控件");
                //线程中，不能调用UI控件，只能这样间接调用
                //不然会报错：调用线程无法访问此对象 因为另一个线程拥有该对象
                //参考：https://www.yuantk.com/weblog/dee39674-c663-4e31-bf81-523518165a78.html
                //在创建控件的基础句柄所在线程上异步执行指定委托。
                this.Dispatcher.BeginInvoke(new Action(() =>
                {
                    TextBox_Title.Text = name;
                    TextBox_WorkDir.Text = workDir;
                    TextBox_URL.Text = m3u8Url;
                    TextBox_PageUrl.Text = pageUrl;
                    TextBox_Headers.Text = headers;
                    TextBox_Proxy.Text = proxyAddress;
                    TextBox_Parameter.Text = cmd;

                    //添加到列表
                    AddTaskToList(a_task);
                }));
            }




        }




        /// <summary>
        /// 通过命名管道实现进程间通讯。
        /// windows的自定义协议，只能执行指定程序，并传递参数。
        /// 但是对于已经运行中的程序，无法得到自定义协议的参数。
        /// 唯一解决办法是，在自定义协议启动的新实例中，找到已经运行中的实例。并通过命名管道，把信息传递过来。
        /// 管道默认会卡UI，所以要用异步管道：
        /// https://developer.aliyun.com/article/1312874
        /// </summary>
        private async Task ReceiveDataAsync()
        {
            
            {
                await Task.Run(async () =>
                {
                    string data = "";
                    while (!Helper.cts.IsCancellationRequested) 
                    {
                        using (var server = new NamedPipeServerStream(Helper.pipeName, PipeDirection.In, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous))
                        {

                            try
                            {
                                //等待连接
                                await server.WaitForConnectionAsync(Helper.cts.Token);
                            }
                            catch (Exception e)
                            {
                                //MessageBox.Show($"等待管道连接出错：{e.Message}");
                                Debug.WriteLine($"等待管道连接出错：{e.Message}");
                                return;
                            }

                            try
                            {
                                //申请内存
                                byte[] buffer = new byte[2048];
                                //等待1秒
                                await Task.Delay(500, Helper.cts.Token);
                                //将管道消息给buffer
                                int bytesRead = await server.ReadAsync(buffer, 0, buffer.Length);

                                //将byte[]转换至string
                                data = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                            }
                            catch (Exception e)
                            {
                                MessageBox.Show($"读取管道出错：{e.Message}");
                                return;
                            }
                        }


                        //显示接收的消息
                        //MessageBox.Show($"收到管道传递的信息：{data}");

                        if (string.IsNullOrEmpty(data)) { return; }

                        //把消息拼成参数列表
                        string[] args = new string[1];
                        args[0] = data;
                        //塞给全局变量
                        try
                        {
                            Helper.Args = args;

                        }
                        catch (Exception e)
                        {
                            MessageBox.Show($"全局赋值出错：{e.Message}");
                            return;
                        }

                        HandleArgs();

                    } 


                });
            }


        }


    }



}
