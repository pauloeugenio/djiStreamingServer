using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Web.Script.Serialization;
using System.IO;

namespace DroneStreamingServer
{
    public sealed class Preferences
    {
        public string Address { get; set; }
        public string Protocol { get; set; }
        public Preferences() { Address = ""; Protocol = "webrtc"; }
    }

    public sealed class Creator
    {
        public string name { get; set; }
        public string description { get; set; }
        public string email { get; set; }
        public string url { get; set; }
        public string pix { get; set; }
    }

    public sealed class NetworkChoice
    {
        public string Address { get; set; }
        public string Name { get; set; }
        public bool DefaultRoute { get; set; }
        public override string ToString() { return Address + " — " + Name; }
    }

    public static class ServerConfig
    {
        public static bool ValidAddress(string value)
        {
            if (String.IsNullOrWhiteSpace(value)) return false;
            string[] octets = value.Split('.');
            if (octets.Length != 4) return false;
            foreach (string octet in octets)
            {
                int number;
                if (octet.Length < 1 || octet.Length > 3 || !octet.All(c => c >= '0' && c <= '9') ||
                    (octet.Length > 1 && octet[0] == '0') || !Int32.TryParse(octet, out number) || number > 255) return false;
            }
            int first = Int32.Parse(octets[0]);
            return first > 0 && first < 224 && first != 127 && value != "255.255.255.255";
        }

        public static string BuildYaml(string address)
        {
            if (!ValidAddress(address)) throw new ArgumentException("Selecione um IPv4 válido da sua rede.");
            return "logLevel: info\nlogDestinations: [stdout]\n" +
                "rtsp: no\nsrt: no\nmoq: no\n" +
                "rtmp: yes\nrtmpAddress: :1935\n" +
                "hls: yes\nhlsAddress: :8888\n" +
                "webrtc: yes\nwebrtcAddress: :8889\nwebrtcLocalUDPAddress: :8189\n" +
                "webrtcAdditionalHosts: ['" + address + "']\n" +
                "paths:\n  drone:\n    source: publisher\n";
        }

        public static string StreamUrl(string address)
        {
            if (!ValidAddress(address)) throw new ArgumentException("IPv4 inválido.");
            return "rtmp://" + address + ":1935/drone";
        }

        public static Uri PlayerUrl(string protocol)
        {
            if (protocol != "webrtc" && protocol != "hls") throw new ArgumentException("Protocolo inválido.");
            return new Uri("http://127.0.0.1:" + (protocol == "hls" ? "8888" : "8889") + "/drone/?autoplay=true&muted=true");
        }

        public static List<NetworkChoice> Networks()
        {
            var result = new List<NetworkChoice>();
            foreach (NetworkInterface network in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (network.OperationalStatus != OperationalStatus.Up ||
                    (network.NetworkInterfaceType != NetworkInterfaceType.Ethernet && network.NetworkInterfaceType != NetworkInterfaceType.Wireless80211)) continue;
                string description = (network.Name + " " + network.Description).ToLowerInvariant();
                if (new[] {"virtual", "vmware", "virtualbox", "hyper-v", "vethernet", "docker", "tap-", "vpn", "wsl"}.Any(description.Contains)) continue;
                var properties = network.GetIPProperties();
                foreach (UnicastIPAddressInformation unicast in properties.UnicastAddresses)
                {
                    string address = unicast.Address.ToString();
                    if (unicast.Address.AddressFamily != AddressFamily.InterNetwork || !ValidAddress(address) || address.StartsWith("169.254.")) continue;
                    result.Add(new NetworkChoice { Address = address, Name = network.Name, DefaultRoute = properties.GatewayAddresses.Any(g => g.Address.AddressFamily == AddressFamily.InterNetwork && !g.Address.Equals(IPAddress.Any)) });
                }
            }
            return result.OrderByDescending(n => n.DefaultRoute).ThenBy(n => n.Name).ToList();
        }

        public static List<string> OccupiedPorts()
        {
            var properties = IPGlobalProperties.GetIPGlobalProperties();
            var occupied = properties.GetActiveTcpListeners().Where(e => new[] {1935, 8888, 8889}.Contains(e.Port)).Select(e => "TCP " + e.Port).Distinct().ToList();
            if (properties.GetActiveUdpListeners().Any(e => e.Port == 8189)) occupied.Add("UDP 8189");
            return occupied;
        }

        static Creator ReadCreator(string path)
        {
            try { return new JavaScriptSerializer().Deserialize<Creator>(File.ReadAllText(path)); }
            catch { return null; }
        }

        public static Creator LoadCreator(string folder)
        {
            var defaults = ReadCreator(Path.Combine(folder, "about-defaults.json"));
            var creator = ReadCreator(Path.Combine(folder, "about.json")) ?? defaults ?? new Creator { name = "Paulo Eugenio", email = "pauloeugenio@hotmail.com.br", url = "https://github.com/pauloeugenio" };
            // Migrar o Pix vazio da versão anterior, preservando os demais campos personalizados.
            if (String.IsNullOrWhiteSpace(creator.pix)) creator.pix = defaults != null ? defaults.pix : "";
            return creator;
        }

        public static bool QrMatchesCreator(Creator creator, string folder)
        {
            var defaults = ReadCreator(Path.Combine(folder, "about-defaults.json"));
            return defaults != null && !String.IsNullOrWhiteSpace(defaults.pix) && String.Equals(creator.pix, defaults.pix, StringComparison.OrdinalIgnoreCase);
        }

        public static Preferences LoadPreferences(string path)
        {
            try
            {
                var data = new JavaScriptSerializer().Deserialize<Preferences>(File.ReadAllText(path));
                if (data != null && (data.Address == "" || ValidAddress(data.Address)) && (data.Protocol == "webrtc" || data.Protocol == "hls")) return data;
            }
            catch (Exception ex) when (ex is IOException || ex is ArgumentException || ex is InvalidOperationException || ex is UnauthorizedAccessException) { }
            return new Preferences();
        }
    }
}
