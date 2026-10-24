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
        readonly Button fullscreenButton = new Button();
        readonly Button save = new Button();
        readonly Button refresh = new Button();
        readonly WebView2 player = new WebView2();
        readonly Label runtimeNote = new Label();
        readonly Panel playerHost = new Panel();
        bool fullscreen;
        Rectangle windowBounds;
        FormWindowState windowState;
        FormBorderStyle windowBorder;
        bool windowTopMost;
        Size windowMinimum;
        TableLayoutPanel playerLayout;
        Control sidebar;
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
            Icon = System.Drawing.Icon.ExtractAssociatedIcon(Application.ExecutablePath);
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
        PictureBox BrandLogo(int width, int height)
        {
            var picture = new PictureBox { Size = new Size(width, height), SizeMode = PictureBoxSizeMode.Zoom, BackColor = Color.Transparent, Margin = new Padding(0, 0, 0, 15), AccessibleName = "Logomarca Drone Streaming Server" };
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logo.png");
            if (File.Exists(path)) {
                using (var original = Image.FromFile(path)) picture.Image = new Bitmap(original);
                Disposed += (sender, e) => { if (picture.Image != null) picture.Image.Dispose(); };
            }
            return picture;
        }

        void BuildLayout()
        {
            var side = new FlowLayoutPanel { Dock = DockStyle.Left, Width = 210, BackColor = Card, Padding = new Padding(20, 32, 10, 20), FlowDirection = FlowDirection.TopDown, WrapContents = false };
            side.Controls.Add(BrandLogo(156, 110));
            side.Controls.Add(LabelText("DRONE STREAM", 16, Accent));
            side.Controls.Add(LabelText("SERVIDOR LOCAL", 8, Muted));
            int viewIndex = 0;
            foreach (string title in new[] {"Transmissão", "Configurações", "Sobre"})
            {
                int index = viewIndex++;
                var button = new Button(); StyleButton(button, title); button.Width = 170; button.AutoSize = false; button.Height = 48; button.Margin = new Padding(0, 15, 0, 0);
                button.Click += (sender, e) => screens.SelectedIndex = index;
                side.Controls.Add(button);
            }
            side.Controls.Add(new Label { Text = "Sem Docker.\nSem navegador externo.\nSeu vídeo, na sua rede.", ForeColor = Muted, AutoSize = true, Margin = new Padding(0, 50, 0, 0) });
            screens.Dock = DockStyle.Fill; screens.Appearance = TabAppearance.FlatButtons;
            screens.ItemSize = new Size(0, 1); screens.SizeMode = TabSizeMode.Fixed;
            sidebar = side;
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
            playerLayout = layout;
            layout.Controls.Add(LabelText("Uma nova perspectiva.", 25), 0, 0);
            var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false };
            StyleButton(start, "Iniciar servidor", true); StyleButton(stop, "Parar servidor"); StyleButton(reconnect, "Reconectar vídeo"); StyleButton(fullscreenButton, "Tela cheia");
            start.Click += async (sender, e) => await StartServer();
            stop.Click += (sender, e) => StopServer();
            reconnect.Click += async (sender, e) => await OpenPlayer();
            fullscreenButton.Click += (sender, e) => ToggleFullscreen();
            actions.Controls.AddRange(new Control[] {start, stop, reconnect, fullscreenButton}); layout.Controls.Add(actions, 0, 1);
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
            column.Controls.Add(BrandLogo(210, 145));
            column.Controls.Add(LabelText("FEITO PARA CONECTAR", 9, Accent));
            column.Controls.Add(LabelText("Uma ideia que aproxima.", 27));
            column.Controls.Add(LabelText("Drone Streaming Server · Windows · 1.1.1", 11, Muted));
            var introduction = LabelText("Da câmera do drone para a sua tela. Um servidor local, simples de operar e feito para compartilhar novas perspectivas.", 12, Muted);
            introduction.MaximumSize = new Size(700, 0); column.Controls.Add(introduction);
            var creator = ServerConfig.LoadCreator(AppDomain.CurrentDomain.BaseDirectory);

            var profile = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, BackColor = Card, Padding = new Padding(24), Margin = new Padding(0, 16, 0, 16) };
            profile.Controls.Add(LabelText("CRIADOR DO PROJETO", 9, Accent));
            profile.Controls.Add(LabelText(creator.name ?? "Paulo Eugenio", 23));
            var bio = LabelText(creator.description ?? "Tecnologia para conectar pessoas e novas perspectivas.", 11, Muted);
            profile.Controls.Add(bio);
            var contacts = new FlowLayoutPanel { AutoSize = true, WrapContents = true, Margin = new Padding(0, 10, 0, 0) };
            if (!String.IsNullOrEmpty(creator.email)) {
                var email = new Button(); StyleButton(email, creator.email); email.Click += (sender, e) => OpenLink("mailto:" + creator.email); contacts.Controls.Add(email);
            }
            Uri creatorUrl;
            if (Uri.TryCreate(creator.url, UriKind.Absolute, out creatorUrl) && (creatorUrl.Scheme == "https" || creatorUrl.Scheme == "http")) {
                var github = new Button(); StyleButton(github, "Conheça no GitHub"); github.Click += (sender, e) => OpenLink(creatorUrl.AbsoluteUri); contacts.Controls.Add(github);
            }
            profile.Controls.Add(contacts); column.Controls.Add(profile);

            var donation = new TableLayoutPanel { ColumnCount = 2, RowCount = 1, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, BackColor = Card, Padding = new Padding(24), Margin = new Padding(0, 0, 0, 18) };
            donation.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            donation.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 240));
            var message = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoSize = true, Margin = new Padding(0, 0, 18, 0) };
            message.Controls.Add(LabelText("APOIE O DESENVOLVIMENTO", 9, Accent));
            message.Controls.Add(LabelText("Ajude esta ideia a voar.", 20));
            var contribution = LabelText("Se este projeto foi útil para você, considere contribuir com uma doação via Pix. Seu apoio ajuda a manter o desenvolvimento e trazer novas melhorias.", 11, Muted);
            message.Controls.Add(contribution);
            message.Controls.Add(LabelText("CHAVE PIX · ALEATÓRIA", 9, Accent));
            var pix = Input(new TextBox(), true); pix.Text = creator.pix; pix.Font = new Font("Consolas", 10); message.Controls.Add(pix);
            var copy = new Button(); StyleButton(copy, "Copiar chave Pix", true);
            copy.Click += (sender, e) => { try { Clipboard.SetText(pix.Text); copy.Text = "Chave copiada!"; } catch { pix.Focus(); pix.SelectAll(); ShowError("Selecione a chave e use Ctrl+C para copiar."); } };
            message.Controls.Add(copy);
            var thanks = LabelText("Contribuição voluntária. Obrigado por fazer parte desta ideia.", 10, Muted); message.Controls.Add(thanks);
            donation.Controls.Add(message, 0, 0);
            var qrColumn = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoSize = true, Dock = DockStyle.Fill, Padding = new Padding(8), Margin = new Padding(0) };
            var qr = new PictureBox { Size = new Size(216, 216), BackColor = Color.White, SizeMode = PictureBoxSizeMode.Zoom, Margin = new Padding(0, 0, 0, 12), AccessibleName = "QR Code para contribuição via Pix" };
            string qrPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "qrcode-pix.png");
            if (ServerConfig.QrMatchesCreator(creator, AppDomain.CurrentDomain.BaseDirectory) && File.Exists(qrPath)) {
                using (var original = Image.FromFile(qrPath)) qr.Image = new Bitmap(original);
                Disposed += (sender, e) => { if (qr.Image != null) qr.Image.Dispose(); };
            }
            qrColumn.Controls.Add(qr);
            if (qr.Image == null) {
                qr.Visible = false;
                var unavailable = LabelText("QR Code indisponível para esta chave.\nUse o botão Copiar chave Pix.", 10, Muted);
                unavailable.MaximumSize = new Size(210, 0); qrColumn.Controls.Add(unavailable);
            }
            var enlarge = new Button(); StyleButton(enlarge, "Ampliar QR Code"); enlarge.Enabled = qr.Image != null;
            enlarge.Click += (sender, e) => {
                using (var dialog = new Form { Text = "Contribuir via Pix · Paulo Eugenio", Size = new Size(620, 660), MinimumSize = new Size(360, 400), StartPosition = FormStartPosition.CenterParent, BackColor = Color.White, MinimizeBox = false, ShowInTaskbar = false }) {
                    var picture = new PictureBox { Dock = DockStyle.Fill, Image = qr.Image, SizeMode = PictureBoxSizeMode.Zoom, BackColor = Color.White };
                    dialog.Controls.Add(picture); dialog.ShowDialog(this);
                }
            };
            qrColumn.Controls.Add(enlarge); donation.Controls.Add(qrColumn, 1, 0); column.Controls.Add(donation);
            var footer = LabelText("RTMP → WebRTC / HLS · MediaMTX + Microsoft WebView2\nProjeto independente, sem afiliação com fabricantes de drones. Licenças disponíveis na pasta de instalação.", 9, Muted);
            column.Controls.Add(footer);
            Action resize = () => {
                int width = Math.Max(600, column.ClientSize.Width - column.Padding.Horizontal - 24);
                profile.Width = donation.Width = width;
                bio.MaximumSize = new Size(width - 52, 0); contacts.MaximumSize = new Size(width - 52, 0);
                int textWidth = Math.Max(270, width - donation.Padding.Horizontal - 260);
                contribution.MaximumSize = thanks.MaximumSize = new Size(textWidth, 0); pix.Width = textWidth;
                footer.MaximumSize = introduction.MaximumSize = new Size(width, 0);
            };
            column.SizeChanged += (sender, e) => resize(); resize();
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
                    player.CoreWebView2.ContainsFullScreenElementChanged += (sender, e) => SetFullscreen(player.CoreWebView2.ContainsFullScreenElement);
                    player.KeyDown += (sender, e) => {
                        if (e.KeyCode == Keys.F11 || (e.KeyCode == Keys.Escape && fullscreen)) {
                            e.Handled = true; e.SuppressKeyPress = true;
                            Keys key = e.KeyCode;
                            BeginInvoke(new Action(() => { if (key == Keys.F11) ToggleFullscreen(); else ExitFullscreen(); }));
                        }
                    };
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
            finally { initializingPlayer = false; if (!closing) { fullscreenButton.Enabled = IsRunning && playerReady; reconnect.Enabled = IsRunning; start.Enabled = !IsRunning && !starting; } }
        }

        void StopServer()
        {
            if (fullscreen) SetFullscreen(false);
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

        protected override bool ProcessCmdKey(ref Message message, Keys keyData)
        {
            if (keyData == Keys.F11 && IsRunning && playerReady) { ToggleFullscreen(); return true; }
            if (keyData == Keys.Escape && fullscreen) { ExitFullscreen(); return true; }
            return base.ProcessCmdKey(ref message, keyData);
        }

        void ToggleFullscreen()
        {
            if (fullscreen) ExitFullscreen();
            else if (IsRunning && playerReady) SetFullscreen(true);
        }

        async void ExitFullscreen()
        {
            SetFullscreen(false);
            if (playerReady && !closing) {
                try { await player.CoreWebView2.ExecuteScriptAsync("if(document.fullscreenElement) document.exitFullscreen();"); }
                catch (Exception ex) { Log("Ao sair de tela cheia: " + ex.Message); }
            }
        }

        void SetFullscreen(bool enabled)
        {
            if (enabled == fullscreen || (enabled && (!IsRunning || closing))) return;
            SuspendLayout();
            try {
                if (enabled) {
                    windowState = WindowState; windowBounds = WindowState == FormWindowState.Normal ? Bounds : RestoreBounds;
                    windowBorder = FormBorderStyle; windowTopMost = TopMost; windowMinimum = MinimumSize;
                    fullscreen = true;
                    playerLayout.Controls.Remove(playerHost);
                    screens.Visible = false; sidebar.Visible = false;
                    Controls.Add(playerHost); playerHost.Dock = DockStyle.Fill; playerHost.BringToFront();
                    WindowState = FormWindowState.Normal; MinimumSize = Size.Empty;
                    FormBorderStyle = FormBorderStyle.None; Bounds = System.Windows.Forms.Screen.FromControl(this).Bounds;
                    TopMost = true; player.Focus();
                } else {
                    fullscreen = false;
                    Controls.Remove(playerHost); playerLayout.Controls.Add(playerHost, 0, 4);
                    screens.Visible = true; sidebar.Visible = true;
                    TopMost = windowTopMost; FormBorderStyle = windowBorder; MinimumSize = windowMinimum;
                    WindowState = FormWindowState.Normal; Bounds = windowBounds; WindowState = windowState;
                }
            } finally { ResumeLayout(true); }
        }

        void ToggleControls(bool active)
        {
            start.Enabled = !active && !starting && !initializingPlayer;
            fullscreenButton.Enabled = active && playerReady && !initializingPlayer;
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
