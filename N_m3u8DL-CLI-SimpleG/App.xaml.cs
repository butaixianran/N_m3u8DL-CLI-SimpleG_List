using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace N_m3u8DL_CLI_SimpleG_List
{
    /// <summary>
    /// App.xaml 的交互逻辑
    /// </summary>
    public partial class App : Application
    {

        protected override void OnStartup(StartupEventArgs e)
        {

            
            //单进程：https://blog.csdn.net/smartmz/article/details/7264467
            Process process = Helper.RuningInstance();
            if (process != null)
            {
                //唤醒已经运行的实例到前台
                //MessageBox.Show("应用程序已经在运行中。。。");
                Helper.HandleRunningInstance(process);

                //process.Kill();

                //把获得的启动参数，通过命名管道传递过去
                if (!(e.Args is null) && e.Args.Length > 0) 
                {
                    Helper.SendData(e.Args[0]);
                    //await Helper.SendMessage(Helper.Args[0]);
                }


                //退出
                Application.Current.Shutdown();
                Environment.Exit(1);
            }

            string loc = "en-US";
            string currLoc = Thread.CurrentThread.CurrentUICulture.Name;
            if (currLoc == "zh-TW" || currLoc == "zh-HK" || currLoc == "zh-MO") loc = "zh-TW";
            else if (currLoc == "zh-CN" || currLoc == "zh-SG") loc = "zh-CN";
            //设置语言
            Thread.CurrentThread.CurrentUICulture = CultureInfo.GetCultureInfo(loc);

            //保存参数
            Helper.Args = e.Args;


        }


    }
}
