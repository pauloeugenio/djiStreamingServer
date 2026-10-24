# Drone Streaming Server — Windows

Aplicativo desktop independente da versão Linux/Docker. Executa o MediaMTX diretamente e mostra o vídeo dentro de uma janela nativa com Microsoft WebView2. Não abre navegador externo para assistir ao vídeo.

## Instalar e usar

1. Copie apenas `install-windows.exe`, localizado na raiz do projeto, para o computador Windows.
2. Execute o instalador e siga o assistente. São criados atalhos na área de trabalho e no menu Iniciar.
3. Abra **Drone Streaming Server**. A instalação e a abertura do programa não iniciam o servidor.
4. Em **Configurações**, confira o IPv4 da rede usada pelo controle do drone. Interfaces virtuais comuns são filtradas.
5. Clique em **Iniciar servidor**. Copie o endereço `rtmp://IP:1935/drone` para o controle/aplicativo compatível e inicie a publicação RTMP.
6. Acompanhe o vídeo no player integrado. Se necessário, pare o servidor, selecione HLS nas configurações e inicie novamente.
7. Use **Parar servidor** para encerrar a transmissão. Ao fechar o aplicativo, o MediaMTX iniciado por ele também é encerrado.

O programa nunca inicia o servidor automaticamente na inicialização do Windows. Não é necessário Docker, WSL, Python, Node.js ou Nginx no computador Windows.

## Requisitos

- Windows 10/11 x64 (processador Intel/AMD de 64 bits); não é uma distribuição nativa para Windows ARM64 ou x86.
- .NET Framework 4.7.2 ou posterior (verificado pelo instalador; em sistemas antigos, atualize pelo Windows Update).
- Microsoft Edge WebView2 Runtime. O instalador verifica e executa o bootstrapper oficial quando necessário; essa etapa requer internet. Com o runtime já instalado, a instalação e a operação podem ser feitas offline.
- Rede local com comunicação entre o computador e o publicador RTMP do drone.
- No primeiro início, o Windows pode solicitar acesso no firewall para `mediamtx.exe`. Permita a rede privada utilizada pelo drone. Regras corporativas podem exigir um administrador.

O programa é instalado por usuário em `%LOCALAPPDATA%\Programs\DroneStreamingServer`, sem instalar serviço no Windows. Os arquivos do Linux permanecem separados.

## Interface

- **Transmissão:** iniciar/parar, copiar URL RTMP, player integrado, reconectar vídeo, tela cheia e logs. Use o botão do player, **Tela cheia** ou **F11** para expandir. Use **Esc** ou **F11** para voltar; a posição e o tamanho anteriores são restaurados.
- **Configurações:** selecionar a rede física ou informar um IPv4 pertencente ao computador, escolher WebRTC/HLS, salvar preferências e abrir a pasta de logs.
- **Sobre:** apresentação do projeto, perfil de Paulo Eugenio, e-mail, GitHub e contribuição via Pix, com botão de copiar a chave e QR Code ampliável. A chave incluída é `d1d04da4-56a0-4216-a83d-d24a2ff0d9ff`. O QR Code original fica em `assets/qrcode-pix.png` nos fontes e acompanha o instalador. O arquivo `about.json` instalado continua personalizável e é preservado nas atualizações; se a versão anterior tiver Pix vazio, o programa usa o novo padrão de `about-defaults.json`. Se a chave for personalizada para outra, o QR fornecido é ocultado para não exibir dados divergentes.

O estado “servidor em execução” indica que o MediaMTX está respondendo; não confirma que um drone já está enviando vídeo. Os erros de publicação/reprodução aparecem no log e no player. O player começa sem áudio para facilitar o autoplay; o controle de áudio fica disponível no player.

## Rede e dados

| Porta | Uso |
| --- | --- |
| TCP 1935 | Publicação RTMP do drone |
| TCP 8888 | HLS |
| TCP 8889 | Página e sinalização WebRTC |
| UDP 8189 | Mídia WebRTC |

O programa verifica conflitos de portas antes de iniciar e não encerra processos de outros aplicativos. Para usar no mesmo computador da versão Docker, pare os containers antes, pois as portas são iguais.

Preferências, configuração gerada, logs e perfil do player ficam em `%LOCALAPPDATA%\DroneStreamingServer`. As configurações só são aplicadas quando o servidor é iniciado; alterações de rede/protocolo são feitas com ele parado. Não há gravação ou transcodificação configuradas. A compatibilidade depende dos codecs enviados e do suporte do WebView2. O acesso de rede é sem autenticação/TLS nesta versão, destinado à rede local.

A desinstalação remove o aplicativo e os atalhos, preservando logs e preferências. O WebView2 Runtime é compartilhado com outros aplicativos e não é removido.

## Fontes e compilação

```text
windows-install/
├── src/                  # Aplicativo C# / WinForms e player WebView2
├── tests/                # Testes de IPv4, YAML, URLs e preferências
├── about.json            # Informações do criador e chave Pix
├── assets/qrcode-pix.png # QR Code original fornecido pelo criador
├── installer.nsi         # Assistente de instalação e desinstalação NSIS
├── Dockerfile.build      # Ferramentas isoladas de compilação
├── prepare.py            # Download e verificação das dependências oficiais
├── build.sh              # Compilação, testes e geração do EXE
├── dependencies.json     # Versões e hashes utilizados na compilação
└── installer.sha256      # Hash SHA-256 do instalador gerado
```

Na raiz do repositório, em Linux com Docker:

```bash
docker build -t drone-windows-builder -f windows-install/Dockerfile.build windows-install
docker run --rm --user "$(id -u):$(id -g)" -v "$PWD:/work" drone-windows-builder
```

O resultado é **`install-windows.exe` na raiz**. Binários intermediários ficam em `windows-install/build/` e downloads em `.cache/`, ignorados pelo Git. A compilação usa versões fixas do MediaMTX e do SDK WebView2; os arquivos são verificados contra os checksums oficiais. O bootstrapper Evergreen acompanha a versão disponibilizada pela Microsoft e seu hash é registrado em `dependencies.json`.

O executável não tem assinatura Authenticode. A instalação, o comportamento do firewall, o player/WebView2 e uma transmissão real precisam ser validados em Windows; compilar no Linux não comprova funcionamento gráfico no Windows.

## Componentes

- [MediaMTX: instalação oficial e binários Windows](https://mediamtx.org/docs/kickoff/install).
- [Microsoft WebView2 em Windows Forms](https://learn.microsoft.com/en-us/microsoft-edge/webview2/get-started/winforms).
- [Distribuição do WebView2 Runtime](https://learn.microsoft.com/en-us/microsoft-edge/webview2/concepts/distribution).
- [NSIS](https://nsis.sourceforge.io/Main_Page).

As licenças do MediaMTX e do WebView2 acompanham os arquivos instalados. Os links de e-mail/GitHub na seção Sobre podem abrir o aplicativo padrão ao serem clicados; o player permanece dentro do programa.

## Atualização 1.1.0

Feche o aplicativo no Windows e execute novamente `install-windows.exe`. A atualização mantém as preferências e o perfil personalizado, inclui o Pix/QR Code e integra o evento de [tela cheia do WebView2](https://learn.microsoft.com/en-us/dotnet/api/microsoft.web.webview2.core.corewebview2.containsfullscreenelementchanged?view=webview2-dotnet-1.0.3650.58) com a janela nativa. O funcionamento visual e os atalhos precisam ser confirmados no Windows.

## Identidade visual · 1.1.1

A logomarca fornecida pelo criador está em `assets/logo.png`, preservada em seu formato original. O arquivo `assets/drone.ico` contém tamanhos de 16 a 256 pixels e é incorporado ao aplicativo, ao instalador e ao desinstalador. A janela usa o ícone do executável; os atalhos e a lista de aplicativos utilizam esse mesmo recurso. A marca também aparece na navegação e na seção Sobre.
