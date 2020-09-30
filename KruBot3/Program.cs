using System;
using System.Diagnostics;
using Eto.Forms;
using nucs.JsonSettings;
using System.IO;
using System.Net;
using System.Threading;
using Eto.Drawing;
using TwitchLib.Client;
using TwitchLib.Client.Events;
using TwitchLib.Client.Models;
using System.Timers;
using Gtk;
using Newtonsoft.Json;
using TwitchLib.PubSub.Models.Responses;
using Button = Eto.Forms.Button;
using Label = Eto.Forms.Label;
using Orientation = Eto.Forms.Orientation;
using Timer = System.Timers.Timer;

namespace KruBot3
{
    class Program
    {

        [STAThread]
        static void Main(string[] args)
        {

            new Eto.Forms.Application().Run(new MyForm());
        }
    }

    class MyForm : Eto.Forms.Form
    {
        public static MySettings Settings;
        public static TwitchClient twitchClient;

        public MyForm()
        {
            if (File.Exists("Config.json"))
            {
                Settings = JsonSettings.Load<MySettings>("Config.json");
            }
            else
            {
                Settings = JsonSettings.Construct<MySettings>("Config.json");
            }

            // sets the client (inner) size of the window for your content
            //this.ClientSize = new Eto.Drawing.Size(600, 400);
            this.ClientSize = Settings.windowSize;
            this.SizeChanged += (sender, args) =>
            {
                Settings.windowSize = this.ClientSize;
                Settings.Save();
            };
            this.Title = "KruBot3";
            var Twitch = new WebView();
            var Pretzel = new WebView();
            Twitch.OpenNewWindow += TwitchOnOpenNewWindow;
            Pretzel.OpenNewWindow += TwitchOnOpenNewWindow; //Nothing special is needed.    
            Twitch.Url = new Uri("https://www.twitch.tv/popout/" + Settings.Username + "/chat?popout=");
            WebClient wb = new WebClient();
            string Script = wb.DownloadString("https://cdn.frankerfacez.com/static/ffz_injector.user.js");
            Twitch.DocumentLoaded += (sender, args) =>
                // Twitch.ExecuteScript(""););
                Pretzel.Url = new Uri("https://app.pretzel.rocks/player");
            var browserLayout = new Splitter();
            browserLayout.Panel1 = Twitch;
            browserLayout.Panel2 = Pretzel;
            browserLayout.Orientation = Orientation.Horizontal;
            browserLayout.RelativePosition = 0.75;
            browserLayout.FixedPanel = SplitterFixedPanel.None;
            browserLayout.PositionChanged += (sender, args) =>
            {
                Console.WriteLine(browserLayout.RelativePosition);
                Settings.splitPosition = browserLayout.RelativePosition;
                Settings.Save();
            };
            browserLayout.RelativePosition = Settings.splitPosition;

            var Tabs = new TabControl();
            var TabPage1 = new TabPage();
            TabPage1.Text = "Main Chat";
            TabPage1.Content = browserLayout;
            Tabs.Pages.Add(TabPage1);

            var TabPage2 = new TabPage();
            TabPage2.Text = "Settings";

            var t2Contents = new StackLayout();
            t2Contents.Padding = 50;
            Label Info = new Label();
            Info.Text = "This page lets you configure the bot to be able to listen to your chat.";
            t2Contents.Items.Add(Info);
            Label Info2 = new Label();
            Info2.Text = "Please enter your Username Below:";
            t2Contents.Items.Add(Info2);
            TextBox userName = new TextBox();
            userName.Size = new Size(400, userName.Height);
            if (Settings.Username == "Twitch")
            {
                userName.PlaceholderText = "Username";
            }
            else
            {
                userName.Text = Settings.Username;
            }

            t2Contents.Items.Add(userName);

            Label Info3 = new Label();
            Info3.Text = "And now we need an OAuth Code from that account:";
            t2Contents.Items.Add(Info3);

            Button openOAuth = new Button();
            openOAuth.Click += OpenOAuthOnClick;
            openOAuth.Text = "Open Website";
            t2Contents.Items.Add(openOAuth);

            PasswordBox oAuth = new PasswordBox();
            if (string.IsNullOrEmpty(Settings.oAuthToken))
            {

            }
            else
            {
                oAuth.Text = Settings.oAuthToken;
            }

            oAuth.Size = new Size(400, oAuth.Height);
            t2Contents.Items.Add(oAuth);

            Label SaveNow = new Label();
            SaveNow.Text = "Once you're done, click this button to save your settings!";
            t2Contents.Items.Add(SaveNow);

            Button Save = new Button();
            Save.Text = "Save!";
            Save.Click += (sender, args) =>
            {
                Settings.Username = userName.Text.ToLower();
                Settings.oAuthToken = oAuth.Text;
                Settings.Save();
                MessageBox.Show("Saved!");
                Twitch.Url = new Uri("https://www.twitch.tv/popout/" + Settings.Username + "/chat?popout=");
                initBot();
            };
            t2Contents.Items.Add(Save);
            TabPage2.Content = t2Contents;
            Tabs.Pages.Add(TabPage2);
            this.Content = Tabs;


            #region Bringing up the bot

            if (string.IsNullOrEmpty(Settings.oAuthToken))
            {
                Tabs.SelectedIndex = 1;
            }
            else
            {
                initBot();
            }

            #endregion
        }

        static Timer autoPost = new Timer(); 
        static Timer ClearXKCD = new Timer();
        private void initBot()
        {
            try
            {
                if (twitchClient != null)
                {
                    if (twitchClient.IsConnected)
                    {
                        twitchClient.Disconnect();
                    }
                }

                ConnectionCredentials creds = new ConnectionCredentials(Settings.Username, Settings.oAuthToken);
                twitchClient = new TwitchClient();
                twitchClient.Initialize(creds, Settings.Username);
                twitchClient.Connect();
                twitchClient.OnConnected += (sender, args) =>
                    twitchClient.SendMessage(Settings.Username, "Connected Successfully!");
                twitchClient.OnMessageReceived += TwitchClientOnOnMessageReceived;
                twitchClient.OnChatCommandReceived += TwitchClientOnOnChatCommandReceived;
                //Start a timer to autopost messages one per hour
                autoPost.Interval = TimeSpan.FromSeconds(5).TotalMilliseconds;
                autoPost.Enabled = true;
                autoPost.Elapsed += AutoPostOnElapsed;
                autoPost.Start();
                ClearXKCD.Enabled = false;
                ClearXKCD.Interval = TimeSpan.FromSeconds(10).TotalMilliseconds;
                ClearXKCD.Elapsed += ClearXKCDOnElapsed;
                ClearXKCD.Enabled = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Your Username or OAuth Token was incorrect.");
                MessageBox.Show(ex.Message);
            }

        }

        private void ClearXKCDOnElapsed(object sender, ElapsedEventArgs e)
        {
            File.Delete("/home/krutonium/StreamAssets/xkcd.png");
            //File.Delete("/home/krutonium/StreamAssets/xkcd.txt");
            File.WriteAllText("/home/krutonium/StreamAssets/xkcd.txt", "");
            ClearXKCD.Enabled = false;
        }

        static bool Message = true;

        private void AutoPostOnElapsed(object sender, ElapsedEventArgs e)
        {
            autoPost.Interval = TimeSpan.FromMinutes(15).TotalMilliseconds;
            if (Message)
            {
                twitchClient.SendMessage(Settings.Username, "Remember to Follow if you like what you see!");

            }
            else
            {
                twitchClient.SendMessage(Settings.Username, "Did you know you can subscribe with Twitch Prime?");
            }

            Message = !Message;
        }

        private void TwitchClientOnOnChatCommandReceived(object sender, OnChatCommandReceivedArgs e)
        {
            //Implement your commands that start with ! here.
            if (e.Command.CommandText.ToLower() == "xkcd")
            {
                //https://xkcd.com/2358/info.0.json
                if (e.Command.ArgumentsAsString != null)
                {
                    var wc = new WebClient();

                    try
                    {

                        var myXKCD = JsonConvert.DeserializeObject<XKCD>(
                            wc.DownloadString(@"https://xkcd.com/" + e.Command.ArgumentsAsString + "/info.0.json"));
                        wc.DownloadFile(myXKCD.img, "/home/krutonium/StreamAssets/xkcd.png");
                        File.WriteAllText("/home/krutonium/StreamAssets/xkcd.txt", myXKCD.alt);
                        ClearXKCD.Enabled = true;
                    }
                    catch (Exception ex)
                    {
                        twitchClient.SendMessage(Settings.Username, "Invalid XKCD Comic", false);
                    }
                }
            }
        }

        private void TwitchClientOnOnMessageReceived(object sender, OnMessageReceivedArgs e)
        {
            if (e.ChatMessage.Bits > 0)
            {
                twitchClient.SendMessage(Settings.Username,
                    "Thanks for the Twiddlies, " + e.ChatMessage.Username + "!");
            }

            if (e.ChatMessage.Message.ToLower().Contains("owo") || e.ChatMessage.Message.ToLower().Contains("lewd"))
            {
                twitchClient.SendMessage(Settings.Username, "Wow, That's Ultra Lewd!");
            }

            //Really you can do whatever you want in here that you would normally do in chat. Including Moderation, if that
            //is your goal. /timeout and such works here.
        }

        private void OpenOAuthOnClick(object sender, EventArgs e)
        {
            if (Platform.IsGtk)
            {
                Process.Start("xdg-open", "https://twitchapps.com/tmi/");
            }
            else if (Platform.IsWinForms || Platform.IsWpf)
            {
                Process.Start("https://twitchapps.com/tmi/");
            }
        }

        private void TwitchOnOpenNewWindow(object sender, WebViewNewWindowEventArgs e)
        {
            if (Platform.IsGtk) //Linux needs xdg-open
            {
                Process.Start("xdg-open", e.Uri.ToString());
            }

            if (Platform.IsWpf || Platform.IsWinForms) //Windows will just use whatever is configured as default!
            {
                Process.Start(e.Uri.ToString());
            }
        }
    }

    internal class XKCD
    {
        public string month { get; set; }
        public int num { get; set; }
        public string link { get; set; }
        public string year { get; set; }
        public string news { get; set; }
        public string safe_title { get; set; }
        public string transcript { get; set; }
        public string alt { get; set; }
        public string img { get; set; }
        public string title { get; set; }
        public string day { get; set; }
    }

    class MySettings : JsonSettings
    {
        public override string FileName { get; set; } = "Config.json";

        #region Settings

        public string oAuthToken { get; set; }
        public string Username { get; set; } = "Twitch";
        public Size windowSize { get; set; } = new Size(600, 400);
        public double splitPosition { get; set; } = 0.75;

        #endregion

        public MySettings()
        {
        }

        public MySettings(string fileName) : base(fileName)
        {
        } //Override Parent Things.
    }
}
