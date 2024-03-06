using SharpPcap.LibPcap;
using SharpPcap;
using System;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using System.Net;
using Leaf.xNet;
using System.Runtime.InteropServices;
using System.Text;

namespace ph_GTAV
{
    public partial class MainFRM : Form
    {
        #region "Mouse Move Events"
        private bool _dragging = false;
        private Point _start_point = new Point(0, 0);
        private void Object_MouseDown(object sender, MouseEventArgs e)
        {
            _dragging = true;  // _dragging is your variable flag
            _start_point = new Point(e.X, e.Y);
        }
        private void Object_MouseUp(object sender, MouseEventArgs e)
        {
            _dragging = false;
        }
        private void Object_MouseMove(object sender, MouseEventArgs e)
        {
            if (_dragging)
            {
                Point p = PointToScreen(e.Location);
                Location = new Point(p.X - this._start_point.X, p.Y - this._start_point.Y);
            }
        }
        private void ExitBTN_Click(object sender, EventArgs e) => Application.Exit();
        #endregion

        private static MainFRM instance;
        private ICaptureDevice selectedDevice;
        private LibPcapLiveDevice liveDevice;
        public bool puller = false;

        public static List<string> Filters = new List<string>();
        public static List<string> Results = new List<string>();
        //gonna add gta puller soon UwU
        public static List<string> gta_blacklist = new List<string>()
        {
            "Microsoft",
            "Amazon",
            "Take-Two",
            "Interactive",
            "Software",
            "i3D.net",
            "Google",
            "Take-Two Interactive Software",
            "Cloudflare",
            "Amazon.com, Inc.",
            "Microsoft Corporation",
            ""
        };

        [DllImport("Gdi32.dll", EntryPoint = "CreateRoundRectRgn")]
        private static extern IntPtr CreateRoundRectRgn(int nLeftRect, int nTopRect, int nRightRect, int nBottomRect, int nWidthEllipse, int nHeightEllipse);

        public MainFRM()
        {
            InitializeComponent();
            instance = this;

            this.Region = Region.FromHrgn(CreateRoundRectRgn(0, 0, Width, Height, 6, 6));
            pictureBox1.Region = Region.FromHrgn(CreateRoundRectRgn(0, 0, Width, Height, 6, 6));
            ExitBTN.Region = Region.FromHrgn(CreateRoundRectRgn(0, 0, Width, Height, 6, 6));
        }
        public static void OnPacketArrival(object s, PacketCapture e)
        {
            var packet = e.GetPacket();
            var _packet = PacketDotNet.Packet.ParsePacket(packet.LinkLayerType, packet.Data);
            var udp_packet = _packet.Extract<PacketDotNet.UdpPacket>();
            var tcp_packet = _packet.Extract<PacketDotNet.TcpPacket>();

            if (udp_packet != null)
            {
                var ip = (PacketDotNet.IPPacket)udp_packet.ParentPacket;
                IPAddress source_ip = ip.SourceAddress;
                IPAddress destination_ip = ip.DestinationAddress;

                if (destination_ip.ToString().Contains("192.168") 
                    || destination_ip.ToString().Contains("239.255") 
                    || destination_ip.ToString().Contains("10.9") 
                    || destination_ip.ToString().Contains("10.8")) return;

                if (udp_packet.DestinationPort == 6672 || udp_packet.SourcePort == 6672)
                {
                    string src = source_ip.ToString();
                    string dstip = destination_ip.ToString();
                    string dstport = udp_packet.DestinationPort.ToString();
                    int packets = 0;

                    _ = instance.Invoke(new Action(() =>
                    {
                        int columnIndex = instance.PartyList.Columns.Cast<DataGridViewColumn>()
                            .Where(c => c.Name.Equals("IP_Address"))
                            .Select(c => c.Index)
                            .FirstOrDefault();

                        int PortIndex = instance.PartyList.Columns.Cast<DataGridViewColumn>()
                            .Where(c => c.Name.Equals("Port"))
                            .Select(c => c.Index)
                            .FirstOrDefault();

                        int CountryIndex = instance.PartyList.Columns.Cast<DataGridViewColumn>()
                            .Where(c => c.Name.Equals("Country"))
                            .Select(c => c.Index)
                            .FirstOrDefault();

                        int RegionIndex = instance.PartyList.Columns.Cast<DataGridViewColumn>()
                            .Where(c => c.Name.Equals("State"))
                            .Select(c => c.Index)
                            .FirstOrDefault();

                        int CityIndex = instance.PartyList.Columns.Cast<DataGridViewColumn>()
                            .Where(c => c.Name.Equals("City"))
                            .Select(c => c.Index)
                            .FirstOrDefault();

                        int ISPIndex = instance.PartyList.Columns.Cast<DataGridViewColumn>()
                            .Where(c => c.Name.Equals("ISP"))
                            .Select(c => c.Index)
                            .FirstOrDefault();

                        int packetsIndex = instance.PartyList.Columns.Cast<DataGridViewColumn>()
                            .Where(c => c.Name.Equals("Packets"))
                            .Select(c => c.Index)
                            .FirstOrDefault();
                        DataGridViewRow existingRow = instance.PartyList.Rows.Cast<DataGridViewRow>()
                            .FirstOrDefault(r => r.Cells[columnIndex].Value != null && r.Cells[columnIndex].Value.ToString().Equals(dstip));

                        if (existingRow == null)
                        {
                            try
                            {
                                using (var iploc = new HttpRequest())
                                {
                                    iploc.IgnoreProtocolErrors = true;
                                    string res = iploc.Get($"http://ip-api.com/json/{dstip}?fields=country,regionName,city,isp").ToString();
                                    var json = JObject.Parse(res);
                                    if ((string)json["country"] == "" || gta_blacklist.Contains((string)json["isp"])) return;
                                    
                                    DataGridViewRow newRow = new DataGridViewRow();
                                    newRow.CreateCells(instance.PartyList);
                                    newRow.Cells[columnIndex].Value = dstip;
                                    newRow.Cells[PortIndex].Value = dstport;
                                    newRow.Cells[CountryIndex].Value = (string)json["country"];
                                    newRow.Cells[RegionIndex].Value = (string)json["regionName"];
                                    newRow.Cells[CityIndex].Value = (string)json["city"];
                                    newRow.Cells[ISPIndex].Value = (string)json["isp"];
                                    newRow.Cells[packetsIndex].Value = packets;
                                    instance.PartyList.Rows.Add(newRow);
                                }
                            }
                            catch
                            {

                            }
                        }
                        else
                        {
                            int.TryParse(existingRow.Cells[packetsIndex].Value?.ToString(), out packets);
                            packets++;
                            existingRow.Cells[packetsIndex].Value = packets;
                        }
                        instance.label4.Text = "IP's Pulled: " + instance.PartyList.RowCount;
                    }));
                }
            }
        }
        public void InitializeSniffer()
        {
            if (liveDevice != null && liveDevice.Opened)
            {
                liveDevice.StopCapture();
                liveDevice.Close();
            }

            string selectedInterface = (string)comboBox1.SelectedItem;
            selectedDevice = CaptureDeviceList.Instance.FirstOrDefault(device => device.Description == selectedInterface);
            if (selectedDevice == null) return;
          
            liveDevice = (LibPcapLiveDevice)selectedDevice;
            liveDevice.OnPacketArrival += new PacketArrivalEventHandler(OnPacketArrival);
            liveDevice.Open();
            liveDevice.StartCapture();
            puller = true;
            status.Text = "Status: Active!";
        }
        public void DeinitializeSniffer()
        {
            if (liveDevice != null && liveDevice.Opened)
            {
                liveDevice.StopCapture();
                liveDevice.Close();
                puller = false;
                status.Text = "Status: Inactive!";
            }
        }
        private void MainFRM_Load(object sender, EventArgs e)
        {
            CaptureDeviceList devices = CaptureDeviceList.Instance;

            foreach (LibPcapLiveDevice device in devices)
            {
                if (device.Interface.Addresses.Count > 0)
                {
                    if (!device.Description.Contains("loopback")) comboBox1.Items.Add(device.Description);
                }
            }
            if (comboBox1.Items.Count > 0) comboBox1.SelectedIndex = 0;
        }
        private void startbtn_Click(object sender, EventArgs e) => InitializeSniffer();
        private void stopbtn_Click(object sender, EventArgs e) => DeinitializeSniffer();
        private void clearbtn_Click_1(object sender, EventArgs e) => PartyList.Rows.Clear();
    }
}