using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Reflection;
using System.Windows.Forms;

namespace Deasterisk
{
    public partial class Form1 : Form
    {
        MemoryService m;
        IntPtr ptr;
        string exec;
        const int VersionNum = 1;
        Assembly assembly;
        Type type;
        MethodInfo method;

        public Form1()
        {
            InitializeComponent();

            var p = GameService.GetProcess();
            if(p == null)
            {
                label2.Text = "游戏未启动";
                button1.Enabled = false;
                return;
            }
            m = new MemoryService(p);
            GameScanner g = new GameScanner();
            g.Initialize(m);
            if (!g.ValidatePointers())
            {
                MessageBox.Show("初始化失败，如发生在游戏更新后请向作者催更。");
                Environment.Exit(-1);
            }
            ptr = g.GetPointer(ConstSignature.PointerType.LogFilter);
            var dat = m.Read(ptr, 16);

            if (BitConverter.ToInt64(dat, 0) == 0 && BitConverter.ToInt64(dat, 8) == 0)
            {
                label2.Text = "屏蔽已解除";
                button1.Enabled = false;
                m.Dispose();
            }

            g.Dispose();
        }

        private void button2_Click(object sender, EventArgs e)
        {
            string content =
                "Deasterisk 4.5+" + "\n" +
                "作者：安娜贝拉尔" + "\n" +
                "邮箱：i@kage.moe" + "\n" +
                "" + "\n" +
                "适用版本：FF14(DX11) 4.5及以上" + "\n" +
                "如遇游戏崩溃/无响应/报错/行为异常等" + "\n" +
                "请立即停止使用，并联系作者进行更新" + "\n" +
                "5.0更新后请检查更新（发布时删除此行）" + "\n" +
                "" + "\n" +
                "说明：" + "\n" +
                "本工具用于解除游戏自带的敏感词屏蔽功能" + "\n" +
                "" + "\n" +
                "表情包Time：" + "\n" +
                "写作练刁读作练习，写作练习读作练*.jpg" + "\n" +
                "那一天人们又回想起被星星盾·灵光支配的恐怖.png" + "\n" +
                "你可知道NPC是全国人大的英文缩写？.bmp" + "\n" +
                "就算是库啵任务喊话，我也屏蔽给你看！.tiff" + "\n" +
                "我在**米格的神拳痕等你，你怎么还不来QAQ.gif" + "\n" +
                "" + "\n" +
                "使用方法：" + "\n" +
                "戳一下帮助上面那个大方块，然后关掉程序= =" + "\n" +
                "" + "\n" +
                "免责声明：" + "\n" +
                "上封神榜了别找我，找卖你脚本/给你代练的人去" + "\n";
            MessageBox.Show(content, "Deasterisk帮助和关于");
        }

        private void button1_Click(object sender, EventArgs e)
        {
            m.Write(ptr, new byte[16]);
            label2.Text = "屏蔽已解除";
            button1.Enabled = false;

            m.Dispose();
        }

        private void button3_Click(object sender, EventArgs e)
        {
            label3.Text = "正在检查更新";
            WebClient webClient = new WebClient();
            //webClient.DownloadStringCompleted += new DownloadStringCompletedEventHandler(DownloadHandler);
            //webClient.DownloadStringAsync(new Uri(@""));
        }

        private void DownloadHandler(object sender, DownloadStringCompletedEventArgs e)
        {
            if (!e.Cancelled && e.Error == null)
            {
                var textString = e.Result.Split('\n');
                if (Convert.ToInt32(textString[0]) <= VersionNum)
                {
                    label3.Invoke((MethodInvoker)delegate {
                        label3.Text = "已是最新版本";
                    });
                    return;
                }

                label3.Invoke((MethodInvoker)delegate {
                    label3.Text = "正在下载更新";
                });

                WebClient webClient = new WebClient();
                webClient.DownloadFileCompleted += new AsyncCompletedEventHandler(UpdateHandler);
                exec = Path.Combine(Path.GetTempPath() + Path.GetRandomFileName());
                webClient.DownloadFileAsync(new Uri(@"" + textString[1]), exec);

                // TODO: Remaining codes
            }
            else
            {
                label3.Invoke((MethodInvoker)delegate {
                    label3.Text = "无法检查更新";
                });
            }
        }

        [Obsolete("Insecure approch. Abandoned.")]
        private void UpdateHandler(object sender, AsyncCompletedEventArgs e)
        {
            if (!e.Cancelled && e.Error == null)
            {
                label3.Invoke((MethodInvoker)delegate {
                    label3.Text = "正在应用更新";
                });

                string strCmdText = $"/c (echo. > \"{exec}\":Zone.Identifier) 2>NUL";
                Process.Start("CMD.exe", strCmdText);

                string path = Assembly.GetExecutingAssembly().CodeBase;
                var directory = Path.GetDirectoryName(path);
                assembly = Assembly.LoadFile(exec);
                type = assembly.GetType("DeasteriskUpd.Updater");
                method = type.GetMethod("DoUpdate", BindingFlags.Static | BindingFlags.Public);
                var result = (bool)method.Invoke(null, new object[] { directory });

                if (result)
                {
                    label3.Invoke((MethodInvoker)delegate
                    {
                        label3.Text = "重启程序以完成更新";
                    });
                }
                else
                {
                    label3.Invoke((MethodInvoker)delegate
                    {
                        label3.Text = "无法应用更新";
                    });
                }

            }
            else
            {
                label3.Invoke((MethodInvoker)delegate {
                    label3.Text = "无法下载更新";
                });
            }
        }
    }
}
