using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.IO;
using System.IO.Pipes;
using System.Security.Principal;
using System.Windows;
using System.Threading;


namespace N_m3u8DL_CLI_SimpleG_List
{
    public static class Helper
    {

        public static string pipeName = "N_m3u8DL_CLI_SimpleG_List_Pipe";
        public static string saveListFileName = "saved_list";
        public static NamedPipeServerStream serverStream;
        public static NamedPipeClientStream clientStream;
        public static CancellationTokenSource cts = new CancellationTokenSource();
        public static bool isAdmin = IsRunAsAdmin();

        //m3u8dl协议传入的参数。由app启动的时候，传到这里暂存
        //再在main window的处理方法中去处理。
        public static string[] Args;

        public static List<M3u8TaskItem> m3u8_tasks = new List<M3u8TaskItem>();
        //保存任务列表的IO对象
        public static IO listFileIO = new IO(saveListFileName);



        public static bool ValidateUrlWithRegex(string url)
        {
            string pattern = @"^(https?|ftp)://[^\s/$.?#].[^\s]*$";
            Regex regex = new Regex(pattern, RegexOptions.IgnoreCase);
            return regex.IsMatch(url);
        }

        public static string RegisterUriScheme(string scheme, string applicationPath)
        {
            string msg = "Registered m3u8DL Protocol";

            try
            {
                using (var schemeKey = Registry.ClassesRoot.CreateSubKey(scheme, writable: true))
                {
                    schemeKey.SetValue("", "URL:m3u8DL Protocol");
                    schemeKey.SetValue("URL Protocol", "");
                    using (var defaultIconKey = schemeKey.CreateSubKey("DefaultIcon"))
                    {
                        defaultIconKey.SetValue("", $"\"{applicationPath}\",1");
                    }
                    using (var shellKey = schemeKey.CreateSubKey("shell"))
                    using (var openKey = shellKey.CreateSubKey("open"))
                    using (var commandKey = openKey.CreateSubKey("command"))
                    {
                        commandKey.SetValue("", $"\"{applicationPath}\" \"%1\"");
                        return msg;
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                return e.Message;
            }

        }

        public static string UnregisterUriScheme(string scheme)
        {
            string msg = "Unregistered m3u8DL Protocol";

            try
            {
                Registry.ClassesRoot.DeleteSubKeyTree(scheme);
                return msg;
            }
            catch (Exception e)
            {
                return e.Message;
            }

            
        }


        [DllImport("shell32.dll", SetLastError = true)]
        static extern IntPtr CommandLineToArgvW([MarshalAs(UnmanagedType.LPWStr)] string lpCmdLine, out int pNumArgs);
        //使用Win32 API解析字符串为命令行参数
        public static IEnumerable<string> ParseArguments(string commandLine)
        {
            int argc;
            var argv = CommandLineToArgvW(commandLine, out argc);
            if (argv == IntPtr.Zero)
                throw new System.ComponentModel.Win32Exception();
            try
            {
                var args = new string[argc];
                for (var i = 0; i < args.Length; i++)
                {
                    var p = Marshal.ReadIntPtr(argv, i * IntPtr.Size);
                    args[i] = Marshal.PtrToStringUni(p);
                }

                return args;
            }
            finally
            {
                Marshal.FreeHGlobal(argv);
            }
        }

        /// <summary>
        /// 该函数设置由不同线程产生的窗口的显示状态
        /// </summary>
        /// <param name="hWnd">窗口句柄</param>
        /// <param name="cmdShow">指定窗口如何显示。查看允许值列表，请查阅ShowWlndow函数的说明部分</param>
        /// <returns>如果函数原来可见，返回值为非零；如果函数原来被隐藏，返回值为零</returns>
        [DllImport("User32.dll")]
        public static extern bool ShowWindowAsync(IntPtr hWnd, int cmdShow);
        /// <summary>
        ///  该函数将创建指定窗口的线程设置到前台，并且激活该窗口。键盘输入转向该窗口，并为用户改各种可视的记号。
        ///  系统给创建前台窗口的线程分配的权限稍高于其他线程。 
        /// </summary>
        /// <param name="hWnd">将被激活并被调入前台的窗口句柄</param>
        /// <returns>如果窗口设入了前台，返回值为非零；如果窗口未被设入前台，返回值为零</returns>
        [DllImport("User32.dll")]
        public static extern bool SetForegroundWindow(IntPtr hWnd);

        private const int SW_SHOWNOMAL = 1;
        public static void HandleRunningInstance(Process instance)
        {
            ShowWindowAsync(instance.MainWindowHandle, SW_SHOWNOMAL);//显示
            SetForegroundWindow(instance.MainWindowHandle);//唤醒到最前端
        }

        /// <summary>
        /// 单进程用
        /// </summary>
        /// <returns></returns>
        public static Process RuningInstance()
        {
            Process currentProcess = Process.GetCurrentProcess();
            Process[] Processes = Process.GetProcessesByName(currentProcess.ProcessName);

            foreach (Process process in Processes)
            {
                if (process.Id != currentProcess.Id)
                {
                    if (Assembly.GetExecutingAssembly().Location.Replace("/", "\\") == currentProcess.MainModule.FileName)
                    {
                        return process;
                    }
                }
            }
            return null;
        }

        /// <summary>
        /// 向命名管道发送数据。同步
        /// </summary>
        public static void SendData(string msg)
        {
            try
            {
                using (NamedPipeClientStream pipeClient =new NamedPipeClientStream(".", Helper.pipeName, PipeDirection.Out))
                {
                    pipeClient.Connect();
                    using (StreamWriter sw = new StreamWriter(pipeClient))
                    {
                        sw.WriteLine(msg);
                        sw.Flush();
                    }
                }
            }
            catch (Exception e)
            {
                MessageBox.Show($"发送管道消息出错：{e.Message}");
            }

        }


        /// <summary>
        /// 通过命名管道实现进程间通讯。异步
        /// windows的自定义协议，只能执行指定程序，并传递参数。
        /// 但是对于已经运行中的程序，无法得到自定义协议的参数。
        /// 唯一解决办法是，在自定义协议启动的新实例中，找到已经运行中的实例。并通过命名管道，把信息传递过来。
        /// 管道默认会卡UI，所以要用异步管道：
        /// https://developer.aliyun.com/article/1312874
        /// </summary>
        public static async Task SendMessage(string messageToSend)
        {
            //创建命名管道-client
            clientStream = new NamedPipeClientStream(".", pipeName, PipeDirection.Out, PipeOptions.Asynchronous);
            // 连接到命名管道服务器
            await clientStream.ConnectAsync();
            //转为byte[]
            byte[] data = Encoding.UTF8.GetBytes(messageToSend);
            //发送消息
            await clientStream.WriteAsync(data, 0, data.Length);
            //刷新
            await clientStream.FlushAsync();
        }

        /// <summary>
        /// 判断当前程序是否以管理员权限运行
        /// </summary>
        /// <returns>true = 管理员权限，false = 普通权限</returns>
        public static bool IsRunAsAdmin()
        {
            using (WindowsIdentity identity = WindowsIdentity.GetCurrent())
            {
                WindowsPrincipal principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }

        }

    }
}
