using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using wsl_usb_manager.Controller;

namespace wsl_usb_manager
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private System.Windows.Forms.NotifyIcon notifyIcon = new System.Windows.Forms.NotifyIcon();
        
        public MainWindow()
        {
            InitializeComponent();
            USBMonitor m = new(OnUSBEvent);
            m.Start();
            initNotifyIcon();
        }

        private void initNotifyIcon()
        {
            notifyIcon.Visible = true;
            notifyIcon.Icon = Properties.Resources.NotifyIcon;
            notifyIcon.Text = this.Title;

            notifyIcon.MouseClick += new MouseEventHandler(show_Click);
            notifyIcon.ContextMenuStrip = new ContextMenuStrip();

            var showItem = new ToolStripMenuItem("Show");
            showItem.Click += new EventHandler(show_Click);
            notifyIcon.ContextMenuStrip.Items.Add(showItem);

            var exitItem = new ToolStripMenuItem("Exit");
            exitItem.Click += new EventHandler(exit_Click);
            notifyIcon.ContextMenuStrip.Items.Add(exitItem);

        }

        private void exit_Click(object sender, EventArgs e)
        {
            Environment.Exit(0);
        }

        private void show_Click(object Sender, EventArgs e)
        {
            if (WindowState == WindowState.Minimized)
                WindowState = WindowState.Normal;
            Show();
            Activate();
        }

        protected override void OnStateChanged(EventArgs e)
        {
            if (WindowState == WindowState.Minimized) Hide();

            base.OnStateChanged(e);
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            e.Cancel = true;
            Hide();
            base.OnClosing(e);
        }


        private void OnUSBEvent(object sender, USBEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                string msg = "Device ";
                if (e.Name != null)
                {
                    msg += e.Name + "("+e.VID+":"+e.PID+")";
                }
                else
                {
                    msg += e.VID + ":" + e.PID;
                }
                msg += " ";
                if (e.IsConnected)
                {
                    msg += "connected";
                }
                else
                {
                    msg += "disconnected";
                }
                msg += ".";
                if (NotificationView.MessageQueue is { } messageQueue)
                {
                    //the message queue can be called from any thread
                    Task.Factory.StartNew(() => messageQueue.Enqueue(msg));
                }
            });
        }

    }
}