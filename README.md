# N_m3u8DL-CLI-SimpleG-List (Windows Only)
根据m3u8下载工具 [N_m3u8DL-CLI](https://github.com/nilaoda/N_m3u8DL-CLI) 命令行工具的官方 Simple GUI 图形界面启动器改动。  
[https://github.com/nilaoda/N_m3u8DL-CLI-SimpleG](https://github.com/nilaoda/N_m3u8DL-CLI-SimpleG)  

# 新功能
* 新增任务列表功能
* 列表区域下方会显示下载进度的Log。
* 原版的"Go"按钮，变为"Add"，改为把任务添加到列表，而不是直接下载。
* GUI端支持 m3u8dl 协议。可以配合chrome浏览器扩展 [猫抓](https://chromewebstore.google.com/detail/%E7%8C%AB%E6%8A%93/jfedfbgedapdagkghmgibemcoggfppbb?hl=zh-CN) 使用。在[猫抓](https://chromewebstore.google.com/detail/%E7%8C%AB%E6%8A%93/jfedfbgedapdagkghmgibemcoggfppbb?hl=zh-CN)中，点击下载，会自动打开本GUI工具，把下载任务添加到列表中。
* 参数中，增加"--pageUrl"参数。这个参数是指下载的网页。实际下载时，不会用到，但是会保存在任务中。这样，选择一个任务，点击"打开网页"按钮，就能方便的打开该视频的网页。当下载失败时，便于重新获取m3u8地址。
* 点击任务列表中的一个任务，任务的内容，会填充到左边表单，可以修改之后，重新添加。
* 任务列表中的任务，右侧会显示简单的下载状态。   

![image](img/screenshot.jpg)  

# 使用方法
## 基本用法
* 下载 [N_m3u8DL-CLI](https://github.com/nilaoda/N_m3u8DL-CLI) 命令行工具，建议下载带ffmpeg的版本。
* 下载本工具，解压后，把N_m3u8DL-CLI-SimpleG_List.exe，放在N_m3u8DL-CLI同目录下。
* 打开本工具，选择你的N_m3u8DL-CLI下载工具，并选择下载目录。
* 以后要下载m3u8的时候，在左边表单界面上填写信息。参数请看[原版文档](https://nilaoda.github.io/N_m3u8DL-CLI/SimpleGUI.html)。
* 点击"Add"按钮，添加任务到队列。添加了足够的任务后，点击下载开始下载。  

下载的同时，可以继续添加任务。  

## 最佳用法
配合猫抓使用效果最好。方法：  

使用chrome类浏览器，安装扩展：[猫抓](https://chromewebstore.google.com/detail/%E7%8C%AB%E6%8A%93/jfedfbgedapdagkghmgibemcoggfppbb?hl=zh-CN)   

在猫抓的设置中，开启"**调用N_m3u8DL-CLI的m3u8dl://协议下载m3u8 和 mpd**"。填写好协议要用的下载参数。

下面的参数供参考：
```
"${url}" --saveName "${title}" --workDir "你的下载目录" --enableDelAfterDone --headers "Referer:${initiator}" --pageUrl "${webUrl}" --proxyAddress "socks5://127.0.0.1:你的代理端口"
```

(
如果用N_m3u8DL-CLI命令行工具注册过m3u8DL协议。请先用N_m3u8DL-CLI命令行工具注销协议。命令行命令：  
> 从命令行进入N_m3u8DL-CLI目录，运行：  
> N_m3u8DL-CLI可执行文件 --unregisterUrlProtocol  

)

用管理员方式，打开本GUI工具，点击左下角的"注册m3u8DL协议"。注册协议后，程序不要再移动位置。

要取消注册协议的话，再次用管理员方式运行本程序，点击左下角的"注销m3u8DL协议"即可。

关闭GUI工具，平时运行不需要管理员模式。
用普通模式重新打开本GUI工具，填入N_m3u8DL-CLI的可执行程序文件名。选择下载目录，就是工作目录。

好了，配置完毕。

以后要下载m3u8的时候，在页面上，点击浏览器扩展 猫抓 的图标。在m3u8地址旁边，会有下载箭头，点击，就会打开本GUI工具，自动把下载信息，添加到列表。  

添加够了之后，点击下载即可。  

# 已知问题
## 停止并不会立刻停止
下载是调用 N_m3u8DL-CLI命令行工具 在另一个进程异步进行的。中途停止比较麻烦，并没有开发。

所以，简化之后，停止按钮的作用变成，完全当前这一个任务后停止，而不是立刻停止。

## 特殊情况卡界面
下载时，程序会等待后台的N_m3u8DL-CLI命令行工具提供Log信息。如果 N_m3u8DL-CLI命令行工具 长时间不提供，本程序界面就会卡住。

N_m3u8DL-CLI命令行工具不提供Log信息只有2种情况：
* 网络太慢或不通畅，没有新的信息发出来
* 下载完毕，正在合并，期间没有Log信息

会卡住UI，是因为原版程序相当随意，使用的是古老的架构，并把代码全部塞在UI的代码页中。本程序只是改动，不打算颠覆，所以不动大手术的话，只能做到此地步，凑合吧。


# 原版文档

对应命令行工具：https://github.com/nilaoda/N_m3u8DL-CLI

相关说明：https://nilaoda.github.io/N_m3u8DL-CLI/SimpleGUI.html

![image](https://user-images.githubusercontent.com/20772925/153235235-712b338e-4e2a-4a77-8b3b-119bceb45f24.png)

# 开发人员信息
如果要自己继续开发，以下信息需要了解。

原版是C#的WPF项目。框架最高到.net framework 4.8，难以移植到更高。

虽然看起来只是添加了个任务列表，但背后的辛苦非常之多。

首先，windows的注册自定义协议，有很大的局限。当浏览器中，触发了协议时，windows只能打开指定程序，并传递参数。

但是，如果这个程序已经打开了，那么是收不到任何参数的。windows会再打开一个新实例。

所以，这里只能是在新实例里面，查找是否有同名的进程已经打开，然后，把参数，传递给已经打开的进程，并把这个进程，显示到前台。之后，这个新实例，再把自己关闭。

两个进程之间通讯，使用的是命名管道。程序一旦运行，就有个反复监听命名管道的服务。

监听命名管道是手写的异步管道，才能不卡界面。异步管道，以任务的方式，跑在不同线程中。这样，才能不卡住界面。

然而，在WPF中，不同的线程，不能修改UI。所以，获得了数据之后，不能简单添加到UI列表中。必须使用Invoke和Action的方式修改UI。

最后，下载的时候，是在后台运行N_m3u8DL-CLI进程进行下载。要不卡住界面，这个进程就要异步。异步等待进程结束的方法，是.net 6.0才有的。在.net framework 4.8上，还没有提供。所以，这里是手写了一个异步等待进程结束的方法。

同样的，N_m3u8DL-CLI进程的命令行输出的Log，要用异步的方式，重定向到本工具界面上。然而，即使异步，也要等待N_m3u8DL-CLI提供Log信息。如前所述，N_m3u8DL-CLI进程不提供的时候，就会卡住界面。

# 后续
没有。

本来以为几个小时就能搞定的简单功能，没想到一大堆雷区，折磨了好几天，已经严重超时。不打算更新，用的人将就。开发人员可以自行继续开发。





