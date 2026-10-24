using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace DroneStreamingServer
{
    public sealed class MainWindow : Form
    {
        static readonly Color Background = Color.FromArgb(16, 19, 21);
        static readonly Color Card = Color.FromArgb(26, 30, 32);
        static readonly Color Ink = Color.FromArgb(235, 240, 232);
        static readonly Color Muted = Color.FromArgb(156, 167, 160);
        static readonly Color Accent = Color.FromArgb(212, 239, 134);
        readonly string dataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DroneStreamingServer");
        readonly object logLock = new object();
        readonly TabControl screens = new TabControl();
        readonly ComboBox networks = new ComboBox();
        readonly ComboBox protocol = new ComboBox();
        readonly TextBox address = new TextBox();
        readonly TextBox rtmp = new TextBox();
        readonly TextBox logs = new TextBox();
        readonly Label status = new Label();
        readonly Label placeholder = new Label();
        readonly Button start = new Button();
        readonly Button stop = new Button();
        readonly Button reconnect = new Button();
        readonly Button save = new Button();
        readonly Button refresh = new Button();
        readonly WebView2 player = new WebView2();
        readonly Label runtimeNote = new Label();
        readonly Panel playerHost = new Panel();
        Process server;
        ProcessJob job;
        CancellationTokenSource session;
        bool closing;
        bool playerReady;
        bool initializingPlayer;
        bool starting;

        string SettingsPath { get { return Path.Combine(dataDir, "preferences.json"); } }
        string LogPath { get { return Path.Combine(dataDir, "server.log"); } }
        string SelectedProtocol { get { return protocol.SelectedIndex == 1 ? "hls" : "webrtc"; } }
        bool IsRunning { get { try { return server != null && !server.HasExited; } catch { return false; } } }

        public MainWindow()
        {
            Text = "Drone Streaming Server";
            Size = new Size(1180, 810); MinimumSize = new Size(960, 700);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Background; ForeColor = Ink;
            Font = new Font("Segoe UI", 10F);
            AutoScaleMode = AutoScaleMode.Dpi;
            Directory.CreateDirectory(dataDir);
            RotateLog();
            BuildLayout();
            var preferences = ServerConfig.LoadPreferences(SettingsPath);
            address.Text = preferences.Address;
            protocol.SelectedIndex = preferences.Protocol == "hls" ? 1 : 0;
            RefreshNetworks(preferences.Address);
            UpdateAddress();
            SetStatus("Servidor parado. Clique em Iniciar servidor para começar.");
            ToggleControls(false);
            FormClosing += (sender, e) => { closing = true; StopServer(); player.Dispose(); };
            Log("Aplicativo aberto. O servidor aguarda o comando Iniciar servidor.");
        }

        static Label LabelText(string text, float size = 10, Color? color = null)
        {
            return new Label { Text = text, AutoSize = true, ForeColor = color ?? Ink, Font = new Font("Segoe UI", size), Margin = new Padding(0, 0, 0, 12) };
        }
        static void StyleButton(Button button, string text, bool primary = false)
        {
            button.Text = text; button.AutoSize = true; button.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            button.Padding = new Padding(16, 9, 16, 9); button.Margin = new Padding(0, 0, 10, 8);
            button.FlatStyle = FlatStyle.Flat; button.FlatAppearance.BorderColor = Color.FromArgb(66, 77, 67);
            button.BackColor = primary ? Accent : Card; button.ForeColor = primary ? Background : Ink;
            button.Cursor = Cursors.Hand;
        }
        static TextBox Input(TextBox input, bool readOnly = false)
        {
            input.BackColor = Background; input.ForeColor = Ink; input.BorderStyle = BorderStyle.FixedSingle;
            input.ReadOnly = readOnly; input.Width = 390; input.Margin = new Padding(0, 0, 0, 14);
            return input;
        }
        static FlowLayoutPanel Column()
        {
            return new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true, Padding = new Padding(28) };
        }
        TabPage Screen(string name)
        {
            var page = new TabPage(name) { BackColor = Background, ForeColor = Ink, Padding = new Padding(0) };
            screens.TabPages.Add(page); return page;
        }
        void BuildLayout()
        {
            var side = new FlowLayoutPanel { Dock = DockStyle.Left, Width = 210, BackColor = Card, Padding = new Padding(20, 32, 10, 20), FlowDirection = FlowDirection.TopDown, WrapContents = false };
            side.Controls.Add(LabelText("DRONE STREAM", 16, Accent));
            side.Controls.Add(LabelText("SERVIDOR LOCAL", 8, Muted));
            foreach (string title in new[] {"Transmissão", "Configurações", "Sobre"})
            {
                int index = side.Controls.Count - 2;
                var button = new Button(); StyleButton(button, title); button.Width = 170; button.AutoSize = false; button.Height = 48; button.Margin = new Padding(0, 15, 0, 0);
                button.Click += (sender, e) => screens.SelectedIndex = index;
                side.Controls.Add(button);
            }
            side.Controls.Add(new Label { Text = "Sem Docker.\nSem navegador externo.\nSeu vídeo, na sua rede.", ForeColor = Muted, AutoSize = true, Margin = new Padding(0, 50, 0, 0) });
            screens.Dock = DockStyle.Fill; screens.Appearance = TabAppearance.FlatButtons;
            screens.ItemSize = new Size(0, 1); screens.SizeMode = TabSizeMode.Fixed;
            Controls.Add(screens); Controls.Add(side);
            var transmission = Screen("Transmissão");
            var settings = Screen("Configurações");
            var about = Screen("Sobre");
            BuildTransmission(transmission); BuildSettings(settings); BuildAbout(about);
        }

        void BuildTransmission(TabPage page)
        {
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 6, Padding = new Padding(24), BackColor = Background };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 55));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 110));
            page.Controls.Add(layout);
            layout.Controls.Add(LabelText("Uma nova perspectiva.", 25), 0, 0);
            var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false };
            StyleButton(start, "Iniciar servidor", true); StyleButton(stop, "Parar servidor"); StyleButton(reconnect, "Reconectar vídeo");
            start.Click += async (sender, e) => await StartServer();
            stop.Click += (sender, e) => StopServer();
            reconnect.Click += async (sender, e) => await OpenPlayer();
            actions.Controls.AddRange(new Control[] {start, stop, reconnect}); layout.Controls.Add(actions, 0, 1);
            status.Dock = DockStyle.Fill; status.ForeColor = Accent; status.Padding = new Padding(0, 7, 0, 0); layout.Controls.Add(status, 0, 2);
            var connection = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false };
            connection.Controls.Add(new Label { Text = "No drone (RTMP):", AutoSize = true, Padding = new Padding(0, 8, 8, 0) });
            Input(rtmp, true); rtmp.Width = 315; rtmp.Margin = new Padding(0, 8, 10, 0); connection.Controls.Add(rtmp);
            var copy = new Button(); StyleButton(copy, "Copiar");
            copy.Click += (sender, e) => { if (!String.IsNullOrEmpty(rtmp.Text)) { try { Clipboard.SetText(rtmp.Text); SetStatus("Endereço copiado. Cole na opção RTMP do controle/aplicativo."); } catch { ShowError("Não foi possível acessar a área de transferência. Selecione e copie o endereço manualmente."); } } };
            connection.Controls.Add(copy); layout.Controls.Add(connection, 0, 3);
            playerHost.Dock = DockStyle.Fill; playerHost.BackColor = Color.Black;
            placeholder.Text = "PRONTO PARA CONECTAR\n\nClique em Iniciar servidor e envie vídeo\npelo RTMP do seu drone.\n\nO vídeo será exibido nesta janela.";
            placeholder.TextAlign = ContentAlignment.MiddleCenter; placeholder.Dock = DockStyle.Fill; placeholder.ForeColor = Muted;
            player.Dock = DockStyle.Fill; player.Visible = false; player.DefaultBackgroundColor = Color.Black;
            playerHost.Controls.Add(player); playerHost.Controls.Add(placeholder); layout.Controls.Add(playerHost, 0, 4);
            logs.Multiline = true; logs.ReadOnly = true; logs.ScrollBars = ScrollBars.Vertical; logs.Dock = DockStyle.Fill;
            logs.BackColor = Card; logs.ForeColor = Muted; logs.BorderStyle = BorderStyle.None; logs.Font = new Font("Consolas", 8F); logs.Margin = new Padding(0, 14, 0, 0);
            layout.Controls.Add(logs, 0, 5);
        }

        void BuildSettings(TabPage page)
        {
            var column = Column(); page.Controls.Add(column);
            column.Controls.Add(LabelText("Configurações", 25));
            column.Controls.Add(LabelText("Escolha a rede acessível pelo controle e pelos espectadores.", 10, Muted));
            column.Controls.Add(LabelText("Rede do computador"));
            networks.Width = 570; networks.DropDownStyle = ComboBoxStyle.DropDownList; networks.BackColor = Card; networks.ForeColor = Ink;
            networks.SelectedIndexChanged += (sender, e) => { var choice = networks.SelectedItem as NetworkChoice; if (choice != null) { address.Text = choice.Address; UpdateAddress(); } };
            column.Controls.Add(networks);
            StyleButton(refresh, "Atualizar redes"); refresh.Margin = new Padding(0, 12, 0, 16); refresh.Click += (sender, e) => RefreshNetworks(address.Text); column.Controls.Add(refresh);
            column.Controls.Add(LabelText("IPv4 do servidor (ajuste manual, se necessário)"));
            Input(address); address.TextChanged += (sender, e) => UpdateAddress(); column.Controls.Add(address);
            column.Controls.Add(LabelText("Protocolo do player"));
            protocol.Width = 390; protocol.DropDownStyle = ComboBoxStyle.DropDownList; protocol.BackColor = Card; protocol.ForeColor = Ink;
            protocol.Items.AddRange(new object[] {"WebRTC — menor atraso", "HLS — reprodução alternativa"}); column.Controls.Add(protocol);
            StyleButton(save, "Salvar configurações", true); save.Margin = new Padding(0, 20, 0, 12);
            save.Click += (sender, e) => { try { SavePreferences(); MessageBox.Show("Preferências salvas. Na próxima inicialização serão usadas estas configurações.", Text); } catch (Exception ex) { ShowError(ex.Message); } };
            column.Controls.Add(save);
            column.Controls.Add(LabelText("Pare o servidor para alterar a rede ou o protocolo.\nNo primeiro início, o Windows pode solicitar permissão no firewall.\nPermita o acesso na rede privada usada pelo drone.\n\nPortas: TCP 1935, 8888 e 8889; UDP 8189.\nO programa não grava vídeo automaticamente.", 10, Muted));
            runtimeNote.AutoSize = true; runtimeNote.ForeColor = Muted; runtimeNote.MaximumSize = new Size(620, 0); column.Controls.Add(runtimeNote);
            var folder = new Button(); StyleButton(folder, "Abrir pasta de logs e preferências"); folder.Click += (sender, e) => OpenLink(dataDir); column.Controls.Add(folder);
        }

        void BuildAbout(TabPage page)
        {
            var column = Column(); page.Controls.Add(column);
            column.Controls.Add(LabelText("Sobre o projeto", 25));
            column.Controls.Add(LabelText("Drone Streaming Server · Windows · 1.0.0", 14, Accent));
            column.Controls.Add(LabelText("Uma ponte entre a câmera do drone e quem precisa acompanhar.\nPublicação RTMP, reprodução WebRTC/HLS e servidor MediaMTX local.\nProjeto independente, sem afiliação com fabricantes de drones.", 10, Muted));
            Creator creator;
            try { creator = new JavaScriptSerializer().Deserialize<Creator>(File.ReadAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "about.json"))); }
            catch { creator = new Creator { name = "Paulo Eugenio", email = "pauloeugenio@hotmail.com.br", url = "https://github.com/pauloeugenio", pix = "" }; }
            if (creator == null) creator = new Creator();
            column.Controls.Add(LabelText(creator.name ?? "Paulo Eugenio", 20));
            var description = LabelText(creator.description ?? "Criador do projeto.", 10, Muted); description.MaximumSize = new Size(650, 0); column.Controls.Add(description);
            if (!String.IsNullOrEmpty(creator.email))
            {
                var mail = new Button(); StyleButton(mail, creator.email); mail.Click += (sender, e) => OpenLink("mailto:" + creator.email); column.Controls.Add(mail);
            }
            Uri creatorUrl;
            if (Uri.TryCreate(creator.url, UriKind.Absolute, out creatorUrl) && (creatorUrl.Scheme == "https" || creatorUrl.Scheme == "http"))
            {
                var github = new Button(); StyleButton(github, "GitHub do criador"); github.Click += (sender, e) => OpenLink(creatorUrl.AbsoluteUri); column.Controls.Add(github);
            }
            column.Controls.Add(LabelText("Ajude esta ideia a voar.", 19));
            var contribution = LabelText("Se este projeto foi útil para você, considere fazer uma doação via Pix.\nSeu apoio ajuda na continuidade e nas melhorias do projeto.", 10, Muted); contribution.MaximumSize = new Size(650, 0); column.Controls.Add(contribution);
            var pix = Input(new TextBox(), true); pix.Width = 570; pix.Text = String.IsNullOrWhiteSpace(creator.pix) ? "Em breve — chave Pix será adicionada" : creator.pix; column.Controls.Add(pix);
            column.Controls.Add(LabelText("Componentes e respectivas licenças estão na pasta de instalação.\nMediaMTX e WebView2 são distribuídos com suas licenças originais.", 9, Muted));
        }

        void RefreshNetworks(string preferred)
        {
            try
            {
                networks.Items.Clear(); var options = ServerConfig.Networks();
                foreach (var option in options) networks.Items.Add(option);
                int index = options.FindIndex(n => n.Address == preferred);
                if (index >= 0) networks.SelectedIndex = index;
                else if (options.Count > 0) networks.SelectedIndex = 0;
                else { networks.Items.Add("Nenhuma rede física ativa. Informe um IPv4 local abaixo."); networks.SelectedIndex = 0; }
            }
            catch (Exception ex) { Log("Falha ao listar redes: " + ex.Message); }
        }
        void UpdateAddress() { rtmp.Text = ServerConfig.ValidAddress(address.Text.Trim()) ? ServerConfig.StreamUrl(address.Text.Trim()) : "Selecione uma rede em Configurações"; }
        void SavePreferences()
        {
            if (!ServerConfig.ValidAddress(address.Text.Trim())) throw new ArgumentException("Selecione uma rede ou informe um IPv4 válido em Configurações.");
            File.WriteAllText(SettingsPath, new JavaScriptSerializer().Serialize(new Preferences { Address = address.Text.Trim(), Protocol = SelectedProtocol }), new UTF8Encoding(false));
        }

        async Task StartServer()
        {
            if (starting || IsRunning || initializingPlayer) return;
            starting = true; ToggleControls(true);
            try
            {
                SavePreferences();
                if (!System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces().SelectMany(n => n.GetIPProperties().UnicastAddresses).Any(a => a.Address.ToString() == address.Text.Trim()))
                    throw new InvalidOperationException("O IP escolhido não pertence a este computador. Atualize a lista de redes em Configurações.");
                var occupied = ServerConfig.OccupiedPorts();
                if (occupied.Count > 0) throw new InvalidOperationException("Portas já em uso: " + String.Join(", ", occupied) + ". Pare outras instâncias do MediaMTX ou containers Docker antes de iniciar.");
                string executable = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "mediamtx.exe");
                if (!File.Exists(executable)) throw new FileNotFoundException("mediamtx.exe ausente. Reinstale o aplicativo.");
                string configPath = Path.Combine(dataDir, "mediamtx.yml");
                File.WriteAllText(configPath, ServerConfig.BuildYaml(address.Text.Trim()), new UTF8Encoding(false));
                session = new CancellationTokenSource();
                var token = session.Token;
                job = new ProcessJob();
                var process = new Process { StartInfo = new ProcessStartInfo(executable, "\"" + configPath + "\"") {
                    WorkingDirectory = dataDir, UseShellExecute = false, CreateNoWindow = true,
                    RedirectStandardOutput = true, RedirectStandardError = true
                }};
                server = process;
                process.OutputDataReceived += (sender, e) => { if (e.Data != null) Log(e.Data); };
                process.ErrorDataReceived += (sender, e) => { if (e.Data != null) Log(e.Data); };
                process.Exited += (sender, e) => OnUI(() => { if (server == process) { Log("MediaMTX encerrou. Confira os logs."); StopServer(); } });
                SetStatus("Iniciando servidor...");
                Log("Iniciando MediaMTX. RTMP: " + ServerConfig.StreamUrl(address.Text.Trim()));
                process.Start(); job.Add(process); process.EnableRaisingEvents = true;
                process.BeginOutputReadLine(); process.BeginErrorReadLine();
                bool ready = false;
                for (int attempt = 0; attempt < 60; attempt++)
                {
                    token.ThrowIfCancellationRequested();
                    if (process.HasExited) throw new InvalidOperationException("O servidor encerrou durante a inicialização. Confira os logs abaixo.");
                    if (await ListenerReady()) { ready = true; break; }
                    await Task.Delay(250, token);
                }
                token.ThrowIfCancellationRequested();
                if (!ready) throw new TimeoutException("O servidor não respondeu dentro do tempo esperado. Confira os logs.");
                SetStatus("Servidor em execução. Inicie a publicação RTMP no drone para receber vídeo.");
                reconnect.Enabled = true;
                await OpenPlayer();
            }
            catch (OperationCanceledException) { }
            catch (Exception ex) { Log("Erro: " + ex.Message); StopServer(); if (!closing) ShowError(ex.Message); }
            finally { starting = false; if (!closing) ToggleControls(IsRunning); }
        }

        static async Task<bool> ListenerReady()
        {
            try
            {
                var request = (HttpWebRequest)WebRequest.Create("http://127.0.0.1:8889/");
                request.Proxy = null; request.Timeout = 1000; request.ReadWriteTimeout = 1000;
                try { using (var response = await request.GetResponseAsync()) { return true; } }
                catch (WebException ex) { if (ex.Response != null) { ex.Response.Dispose(); return true; } return false; }
            }
            catch { return false; }
        }

        async Task OpenPlayer()
        {
            if (!IsRunning || initializingPlayer || closing) return;
            initializingPlayer = true; reconnect.Enabled = false;
            try
            {
                if (!playerReady)
                {
                    CoreWebView2Environment.GetAvailableBrowserVersionString();
                    var environment = await CoreWebView2Environment.CreateAsync(null, Path.Combine(dataDir, "WebView2"));
                    await player.EnsureCoreWebView2Async(environment);
                    if (closing) return;
                    player.CoreWebView2.Settings.AreDevToolsEnabled = false;
                    player.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
                    player.CoreWebView2.NewWindowRequested += (sender, e) => e.Handled = true;
                    player.CoreWebView2.NavigationStarting += (sender, e) => {
                        Uri target;
                        if (!Uri.TryCreate(e.Uri, UriKind.Absolute, out target) || target.Scheme != "http" || target.Host != "127.0.0.1" || (target.Port != 8888 && target.Port != 8889)) e.Cancel = true;
                    };
                    player.CoreWebView2.NavigationCompleted += (sender, e) => {
                        if (!e.IsSuccess && IsRunning) SetStatus("Servidor ativo, mas o player não abriu. Use Reconectar vídeo e confira os logs.");
                    };
                    playerReady = true;
                    runtimeNote.Text = "Player integrado: Microsoft WebView2 " + player.CoreWebView2.Environment.BrowserVersionString;
                }
                if (!IsRunning || closing) return;
                player.Visible = true; placeholder.Visible = false; player.BringToFront();
                player.CoreWebView2.Navigate(ServerConfig.PlayerUrl(SelectedProtocol).AbsoluteUri);
            }
            catch (Exception ex)
            {
                Log("Player: " + ex.Message);
                if (!closing) {
                    placeholder.Text = "Não foi possível carregar o player integrado.\n\nReinstale o Microsoft Edge WebView2 Runtime\ne abra novamente o aplicativo.\n\nO servidor pode continuar recebendo RTMP.";
                    placeholder.Visible = true; placeholder.BringToFront(); player.Visible = false;
                    SetStatus("Servidor ativo. Player indisponível; consulte os logs.");
                }
            }
            finally { initializingPlayer = false; if (!closing) { reconnect.Enabled = IsRunning; start.Enabled = !IsRunning && !starting; } }
        }

        void StopServer()
        {
            var process = server; server = null;
            if (session != null) { session.Cancel(); session.Dispose(); session = null; }
            if (process != null)
            {
                try { if (!process.HasExited) { process.Kill(); process.WaitForExit(3000); } }
                catch (Exception ex) { Log("Ao parar: " + ex.Message); }
                finally { process.Dispose(); }
            }
            if (job != null) { job.Dispose(); job = null; }
            if (!closing)
            {
                if (playerReady) { try { player.CoreWebView2.Stop(); } catch { } }
                player.Visible = false; placeholder.Visible = true; placeholder.BringToFront();
                placeholder.Text = "SERVIDOR PARADO\n\nClique em Iniciar servidor para receber vídeo.";
                SetStatus("Servidor parado."); ToggleControls(false);
            }
            if (process != null) Log("Servidor parado pelo aplicativo.");
        }

        void ToggleControls(bool active)
        {
            start.Enabled = !active && !starting && !initializingPlayer;
            stop.Enabled = active; reconnect.Enabled = active && !starting && !initializingPlayer;
            networks.Enabled = address.Enabled = protocol.Enabled = save.Enabled = refresh.Enabled = !active;
        }
        void SetStatus(string text) { status.Text = text; }
        void ShowError(string message) { MessageBox.Show(this, message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning); }
        void OpenLink(string url) { try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); } catch (Exception ex) { ShowError(ex.Message); } }
        void OnUI(Action action)
        {
            if (closing || IsDisposed || !IsHandleCreated) return;
            try { if (InvokeRequired) BeginInvoke(action); else action(); } catch (InvalidOperationException) { }
        }
        void RotateLog()
        {
            try { if (File.Exists(LogPath) && new FileInfo(LogPath).Length > 2 * 1024 * 1024) File.Move(LogPath, Path.Combine(dataDir, "server-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".log")); }
            catch { }
        }
        void Log(string text)
        {
            string line = DateTime.Now.ToString("HH:mm:ss") + "  " + text + Environment.NewLine;
            try { lock (logLock) File.AppendAllText(LogPath, line); } catch { }
            OnUI(() => { if (logs.TextLength > 60000) logs.Clear(); logs.AppendText(line); });
        }
    }
}
