using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace OTAUpdater
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }

        string FOLDER_OTA { get => textBox_folder.Text; set => textBox_folder.Text = value; }
        int PORT { get => (int)numericUpDown_port.Value; set => numericUpDown_port.Value = value; }
        string IP4 { get => textBox_ip4.Text; set => textBox_ip4.Text = value; }
        string msg_send { get => textBox_msg.Text; set => textBox_msg.Text = value; }

        eStateServer _stateServer;

        public eStateServer StateServer
        {
            get => _stateServer;
            set
            {
                _stateServer = value;
                label_state.Text = value.ToString();
                button_open.Enabled = _stateServer == eStateServer.Closed;
                button_close.Enabled = _stateServer == eStateServer.Opened;
                numericUpDown_port.ReadOnly = _stateServer != eStateServer.Closed;
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            if (openFileDialog1.ShowDialog() == DialogResult.OK)
            {
                FOLDER_OTA = openFileDialog1.FileName;
            }
        }

        CancellationTokenSource tokenSource_main = new CancellationTokenSource();
        private UdpClient _server;
        Regex regex_getID;
        private string auth;

        private void button2_Click(object sender, EventArgs e)
        {
            try
            {
                tokenSource_main?.Cancel();
                tokenSource_main?.Dispose();
                tokenSource_main = null;

                _server?.Client?.Disconnect(true);
                _server?.Client?.Dispose();
                _server?.Close();
                _server?.Dispose();
                _server = null;
                _server = new UdpClient(PORT, AddressFamily.InterNetwork);

                tokenSource_main = new CancellationTokenSource();
                tokenSource_main.Token.ThrowIfCancellationRequested();
                StateServer = eStateServer.Opened;

                _ = Task.Run(async () =>
                {
                    while (!tokenSource_main.IsCancellationRequested)
                    {
                        var client = await _server.ReceiveAsync();
                        var client_data = Encoding.UTF8.GetString(client.Buffer);
                        Debug.WriteLine(client_data);

                        try
                        {
                            _setStatus($"[{client.RemoteEndPoint}]>> {client_data}");
                            var r = regex_getID.Match(client_data);
                            var ID = r?.Success == true ? r.Groups[Math.Min(r.Groups.Count, 1)].Value : client_data;
                            var text = $"{client.RemoteEndPoint.Address} #{ID}";
                            if (checkBox1.Checked)
                            {
                                var data = Encoding.UTF8.GetBytes(msg_send);
                                _server.Send(data, data.Length, client.RemoteEndPoint);
                            }
                            _addClient(client.RemoteEndPoint.Address.ToString(), text);
                        }
                        catch (Exception ex)
                        {
                            _setStatus(ex.Message);
                            Console.Beep();
                            var cb = flowLayoutPanel_client.Controls.Cast<Control>().FirstOrDefault(q => q.Tag.ToString() == client.RemoteEndPoint.Address.ToString());
                            if (cb != null)
                            {
                                flowLayoutPanel_client.Invoke(new MethodInvoker(() =>
                                {
                                    flowLayoutPanel_client.Controls.Remove(cb);
                                }));
                            }
                        }
                    }
                }, tokenSource_main.Token);
            }
            catch (Exception ex)
            {
                _setStatus(ex.Message);
                MessageBox.Show(ex.Message);
            }
        }

        private void _setStatus(string client_data)
        {
            statusStrip1.Invoke(new MethodInvoker(() =>
            {
                toolStripStatusLabel_status.Text = $"{DateTime.Now.ToString("HH:mm:ss")}>> {client_data}";
            }));
        }

        void _addClient(string Address, string text)
        {
            var cb = flowLayoutPanel_client.Controls.Cast<CheckBox>().FirstOrDefault(q => q.Tag.ToString() == Address);
            if (cb == null)
            {
                flowLayoutPanel_client.Invoke(new MethodInvoker(() =>
                {
                    flowLayoutPanel_client.Controls.Add(new CheckBox()
                    {
                        AutoSize = true,
                        Tag = Address,
                        Text = text
                    });
                }));
            }
            else
            {
                cb.Invoke(new MethodInvoker(() =>
                {
                    cb.Text = text;
                }));
            }
        }
        private void _updateOTA(string address)
        {
            var Arguments = $"/C \"\"{AppDomain.CurrentDomain.BaseDirectory}OTA_cmd\\espota.exe\" --auth={auth} -d -i {address} -f \"{FOLDER_OTA}\" &pause \"";

            var process = Process.Start(new ProcessStartInfo
            {
                FileName = $"cmd.exe",
                Arguments = Arguments,
                CreateNoWindow = false,
                WindowStyle = ProcessWindowStyle.Normal,
                UseShellExecute = false
            });
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            StateServer = eStateServer.Closed;

            button2_Click(this, e);

            if (ConfigurationManager.AppSettings.AllKeys.Contains("regex_getID"))
            {
                regex_getID = new Regex(ConfigurationManager.AppSettings.Get("regex_getID"));
            }

            if (ConfigurationManager.AppSettings.AllKeys.Contains("auth"))
            {
                auth = ConfigurationManager.AppSettings.Get("auth");
            }
        }

        private void button_close_Click(object sender, EventArgs e)
        {
            if (MessageBox.Show("Hủy kết nối?", "Xác nhận", MessageBoxButtons.OKCancel, MessageBoxIcon.Question) == DialogResult.OK)
            {
                flowLayoutPanel_client.Controls.Clear();

                tokenSource_main?.Cancel();
                tokenSource_main?.Dispose();
                tokenSource_main = null;

                _server?.Close();
                _server?.Dispose();
                _server = null;

                StateServer = eStateServer.Closed;
            }
        }

        private void button2_Click_1(object sender, EventArgs e)
        {
            var lst = flowLayoutPanel_client.Controls.Cast<CheckBox>().Where(q => q.Checked).Select(q => q.Tag + "").ToList();
            foreach (var item in lst)
            {
                _updateOTA(item);
            }
        }
    }

    public enum eStateServer
    {
        Closed,
        Opened,
        Waiting,
    }
}
