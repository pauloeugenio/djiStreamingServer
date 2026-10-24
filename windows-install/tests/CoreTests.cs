using System;
using System.IO;
using DroneStreamingServer;

static class CoreTests
{
    static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    static void Main(string[] args)
    {
        foreach (string ip in new[] {"192.168.15.2", "10.0.0.4", "172.16.1.3"}) Check(ServerConfig.ValidAddress(ip), "IPv4 válido rejeitado: " + ip);
        foreach (string ip in new[] {"", "localhost", "127.0.0.1", "0.0.0.0", "255.255.255.255", "224.0.0.1", "999.1.2.3", "192.168.1.2\nrtsp: yes", "192.168.1", "1.2.3.-1", "01.2.3.4"}) Check(!ServerConfig.ValidAddress(ip), "IPv4 inválido aceito: " + ip);
        Check(ServerConfig.StreamUrl("10.0.0.4") == "rtmp://10.0.0.4:1935/drone", "URL RTMP incorreta");
        Check(ServerConfig.PlayerUrl("webrtc").Port == 8889, "Porta WebRTC incorreta");
        Check(ServerConfig.PlayerUrl("hls").Port == 8888, "Porta HLS incorreta");
        Check(ServerConfig.PlayerUrl("hls").Host == "127.0.0.1", "Player deve permanecer local");
        bool rejected = false;
        try { ServerConfig.BuildYaml("10.0.0.4\npaths: {}"); } catch (ArgumentException) { rejected = true; }
        Check(rejected, "Injeção YAML aceita");
        string yaml = ServerConfig.BuildYaml("192.168.15.2");
        Check(yaml.Contains("webrtcAdditionalHosts: ['192.168.15.2']"), "Host WebRTC incorreto");
        Check(yaml.Contains("webrtcLocalUDPAddress: :8189"), "Porta UDP incorreta");
        Check(yaml.Contains("  drone:\n    source: publisher"), "Caminho de publicação incorreto");
        string temporary = Path.GetTempFileName();
        try {
            File.WriteAllText(temporary, "{broken}");
            Check(ServerConfig.LoadPreferences(temporary).Protocol == "webrtc", "Preferências corrompidas sem fallback");
            File.WriteAllText(temporary, "{\"Address\":\"10.0.0.4\",\"Protocol\":\"hls\"}");
            Check(ServerConfig.LoadPreferences(temporary).Address == "10.0.0.4", "Preferências válidas não carregadas");
            Check(ServerConfig.LoadPreferences(temporary).Protocol == "hls", "Protocolo não carregado");
        } finally { File.Delete(temporary); }
        if (args.Length > 0) File.WriteAllText(args[0], yaml);
        Console.WriteLine("OK: IPv4, rejeição de injeção YAML, configuração, portas, URLs e persistência.");
    }
}
