# Drone Streaming Server

Servidor de streaming para receber vídeo de drones ou dispositivos compatíveis com publicação RTMP e disponibilizar a transmissão na rede local. O projeto executa o **MediaMTX em Docker**, com configuração em YAML.

## Início rápido com menu

Na raiz do projeto (um nível acima deste README), execute:

```bash
bash install-DroneStreamingServer.sh
```

O menu oferece quatro opções:

1. **Instalar**: instala as dependências de sistema, verifica Docker e Compose v2, instala os componentes ausentes pelo repositório oficial Docker, habilita o serviço Docker e baixa as imagens. Não inicia o servidor nem abre o navegador.
2. **Iniciar servidor**: identifica o IPv4 da conexão principal, inicia o container e exibe o endereço para colocar no drone (RTMP) e os endereços para assistir pelo navegador (WebRTC/HLS). Ignora interfaces virtuais comuns, incluindo `virbr0`. Se não houver uma conexão principal elegível e restarem vários endereços, permite escolher a rede.
3. **Parar servidor**: para o MediaMTX e o painel web deste projeto, preservando os containers.
4. **Sair**: fecha o menu. Um servidor já iniciado continua em execução.

A instalação automática suporta **Ubuntu e Debian com systemd ativo**, acesso à internet e privilégios de administrador (root ou sudo). Ela pode pedir a senha do sudo. Em outros sistemas, instale Docker e Compose v2 manualmente; o script exige Bash e o comando `ip` para detectar endereços automaticamente.

A instalação segue os repositórios oficiais para [Ubuntu](https://docs.docker.com/engine/install/ubuntu/) e [Debian](https://docs.docker.com/engine/install/debian/). Pacotes existentes que conflitem com Docker CE são informados para revisão manual, sem remoção automática. O script não adiciona o usuário ao grupo Docker: usa sudo quando necessário. A opção Instalar altera pacotes e habilita o Docker na inicialização do sistema.

Para escolher o IP explicitamente, inclusive quando houver VPN ou interfaces virtuais (a variável antiga `DJI_SERVER_IP` continua aceita por compatibilidade):

```bash
DRONE_SERVER_IP=192.168.15.2 bash install-DroneStreamingServer.sh
```

Use um endereço do servidor acessível pelo controle e pelos espectadores. O script fornece esse IP ao MediaMTX pela variável `MTX_WEBRTCADDITIONALHOSTS` no Compose, que tem precedência sobre o valor em `mediamtx.yml`, sem editar o arquivo. Sem `DRONE_SERVER_IP`, o Compose usa `192.168.15.2` como padrão. Para iniciar manualmente com outro IP, execute `DRONE_SERVER_IP=SEU_IP docker compose up -d`.

O script aguarda o container entrar em execução; a confirmação de vídeo depende de iniciar a publicação no DJI. Ele não altera firewall ou roteador. Se ocorrer erro, a ação é interrompida e o menu permanece disponível. O guia abaixo também permite operar sem o menu.

## Painel web

Somente ao escolher **2 — Iniciar servidor**, o script inicia os containers e tenta abrir **`http://IP_DO_SERVIDOR:8080/`** no navegador. Em sessões sem ambiente gráfico, por SSH ou executadas como root, o endereço é exibido para abrir manualmente. Em outro computador ou celular, use o mesmo endereço pela rede local. A saída do navegador fica em um arquivo temporário `drone-browser.*.log`, cujo caminho é exibido no terminal, para não misturar mensagens do navegador ao menu.

- **Transmissão**: endereço RTMP com botão para copiar, instruções para conectar o DJI e player integrado. Clique em **Abrir player** após iniciar a transmissão no DJI. O player começa sem áudio para facilitar a reprodução automática.
- **Configurações**: IP de destino, escolha entre WebRTC e HLS e abertura automática do player. São preferências locais do navegador; não modificam a configuração do MediaMTX nem controlam o container.
- **Sobre o projeto**: descrição do software e informações do criador, editáveis em `web/about.json`. Os campos são `name`, `description`, `email`, `url` (link opcional HTTP/HTTPS) e `pix`. A chave Pix já está preenchida e o QR Code original acompanha o painel em `web/assets/qrcode-pix.png`. A seção Sobre oferece copiar a chave e ampliar o QR em uma nova aba. Se a chave for alterada para outra, o QR original é ocultado para evitar dados divergentes.

O painel é servido pelo Nginx e incorpora os players conforme a [documentação de integração do MediaMTX](https://mediamtx.org/docs/read/web-browsers). Não depende de bibliotecas, fontes ou serviços externos. A indicação de player aberto não confirma a chegada do vídeo; mensagens de conexão aparecem no próprio player. O controle de início e parada continua disponível pelo `install-DroneStreamingServer.sh`.

Para personalizar a interface, edite `web/index.html`, `web/styles.css` e `web/app.js`; atualize a página para ver as alterações. Não há etapa de compilação.

## Estrutura de diretórios

```text
.
├── install-DroneStreamingServer.sh
└── docker-install/
    ├── README.md
    ├── docker-compose.yml
    ├── mediamtx.yml
    ├── tests/
    └── web/
```

O instalador encontra `docker-install` em relação à sua própria localização, mesmo quando executado de outra pasta. Mantenha essa pasta junto ao instalador. O nome do projeto Compose continua baseado na pasta raiz, preservando a identificação dos containers existentes.

Para os comandos manuais de Docker e testes deste guia, entre na pasta de configuração e preserve o nome do projeto:

```bash
export COMPOSE_PROJECT_NAME="$(basename "$PWD" | tr '[:upper:]' '[:lower:]')"
cd "docker-install"
```

## Como funciona

```text
Drone DJI → controle / aplicativo compatível → RTMP → MediaMTX
                                                       ├─ HLS → navegador / player
                                                       └─ WebRTC → navegador
```

O aplicativo ou controle DJI envia o vídeo para o servidor. O MediaMTX recebe esse fluxo no caminho `drone` e o disponibiliza aos espectadores. O servidor não controla o drone.

## Arquivos do projeto

| Arquivo | Responsabilidade |
| --- | --- |
| `docker-compose.yml` | Define a imagem, o container, as portas e a montagem da configuração. |
| `mediamtx.yml` | Configura os protocolos e o caminho de publicação do vídeo. |
| `../install-DroneStreamingServer.sh` | Instala dependências e oferece o menu para iniciar e parar o servidor. |
| `web/` | Interface do painel, preferências do navegador e informações do criador. |

## Pré-requisitos

- Docker Engine ou Docker Desktop com Docker Compose v2 (`docker compose`).
- Computador servidor com endereço IP acessível pelo dispositivo que publica o vídeo e pelos espectadores.
- Drone, controle e aplicativo DJI que ofereçam transmissão para um endereço RTMP personalizado. A compatibilidade depende do conjunto de equipamentos e das versões do aplicativo e do firmware; o servidor requer uma origem RTMP compatível, independentemente do nome do modelo.
- Para começar, coloque o servidor, o dispositivo DJI que envia a transmissão e os espectadores na mesma rede local, com comunicação entre eles permitida.

Confira a instalação do Docker:

```bash
docker --version
docker compose version
```

## Passo a passo

Execute os comandos manuais a seguir dentro de `docker-install`, onde está o `docker-compose.yml`, com `COMPOSE_PROJECT_NAME` definido conforme explicado acima.

### 1. Descobrir o IP do servidor

No Linux, consulte os endereços das interfaces:

```bash
ip -4 addr
```

Escolha o IPv4 da interface conectada à rede usada pelo dispositivo DJI, como a interface Wi-Fi ou Ethernet. Não utilize o IP de uma interface interna do Docker.

Os exemplos deste README usam **`192.168.15.2`**, que é o endereço presente na configuração atual. Substitua esse endereço pelo IP real do seu servidor em todos os exemplos. Reservar o endereço no DHCP do roteador evita precisar atualizá-lo a cada mudança de IP.

### 2. Ajustar o endereço anunciado pelo WebRTC

No arquivo `mediamtx.yml`, altere:

```yaml
webrtcAdditionalHosts:
  - 192.168.15.2
```

Esse endereço é anunciado aos clientes para estabelecer a conexão de mídia WebRTC. Use um IP do servidor que os espectadores consigam alcançar.

No Compose deste projeto, ajuste também o padrão de `DRONE_SERVER_IP` em `docker-compose.yml` ou defina essa variável ao iniciar, pois `MTX_WEBRTCADDITIONALHOSTS` sobrescreve o valor do YAML. Ao usar o menu, essa configuração é automática.

### 3. Conferir as portas da rede

O Compose publica as seguintes portas do servidor:

| Porta | Protocolo | Uso |
| --- | --- | --- |
| `1935` | TCP | Receber a publicação RTMP do DJI. |
| `8888` | TCP | Acessar HLS por HTTP. |
| `8889` | TCP | Acessar a página e a sinalização WebRTC por HTTP. |
| `8189` | UDP | Transportar a mídia WebRTC. |
| `8080` | TCP | Acessar o painel web servido pelo Nginx. |

Se houver firewall, permita o acesso necessário a partir da rede local. Verifique também se o Wi-Fi usa isolamento de clientes, pois isso pode impedir a comunicação entre dispositivos.

### 4. Iniciar o servidor

Valide a estrutura do Compose e inicie o serviço:

```bash
docker compose config --quiet
docker compose up -d
```

Na primeira execução, o Docker baixa a imagem caso ela ainda não esteja disponível localmente. Verifique o estado e acompanhe os logs:

```bash
docker compose ps
docker compose logs -f mediamtx
```

O container recebe o nome `drone-mediamtx`. Use `Ctrl+C` para sair do acompanhamento dos logs; isso mantém o servidor em execução. O estado em execução confirma que o processo iniciou, mas ainda é necessário publicar vídeo para testar a transmissão.

### 5. Configurar a transmissão no DJI

No DJI Fly compatível, abra a visualização da câmera e procure as configurações de transmissão de imagem, plataformas de transmissão ao vivo e a opção **RTMP**. Os nomes dos menus podem variar conforme a versão e o idioma.

Informe o endereço completo:

```text
rtmp://192.168.15.2:1935/drone
```

O trecho `/drone` deve corresponder ao caminho definido em `paths` no `mediamtx.yml`. Se o aplicativo utilizado separar endereço do servidor e chave de transmissão, preencha os campos de modo que a URL final seja exatamente a indicada acima, sem acrescentar outro caminho, como `/live`.

Selecione a resolução e a taxa de bits disponíveis, conforme a capacidade da rede, e inicie a transmissão. Confira nos logs do MediaMTX se a publicação no caminho `drone` foi aceita.

Se a opção RTMP não estiver disponível, consulte a compatibilidade do equipamento no [guia oficial de transmissão do DJI Fly](https://repair.dji.com/help/content?customId=en-us03400006727&documentType=artical&lang=en&paperDocType=paper&re=US&spaceId=34).

### 6. Assistir à transmissão

Com o DJI publicando vídeo, abra um dos endereços abaixo em um dispositivo com acesso ao servidor:

| Forma de acesso | Endereço |
| --- | --- |
| Painel com player e configurações | `http://192.168.15.2:8080/` |
| Página HLS | `http://192.168.15.2:8888/drone/` |
| Playlist HLS para um player compatível | `http://192.168.15.2:8888/drone/index.m3u8` |
| Página WebRTC | `http://192.168.15.2:8889/drone/` |

WebRTC costuma ser a opção de menor atraso, mas depende da conectividade UDP e da compatibilidade dos codecs com o navegador. HLS também depende de codecs compatíveis e pode apresentar maior atraso. Consulte os detalhes de [reprodução no MediaMTX](https://mediamtx.org/docs/features/publish-and-read) e de [conectividade WebRTC](https://mediamtx.org/docs/features/webrtc-specific-features).

## O que cada configuração faz

### Docker Compose

- `image: bluenviron/mediamtx:latest`: usa a imagem do MediaMTX. A tag `latest` pode apontar para versões diferentes ao baixar a imagem novamente; para uma instalação reproduzível, fixe uma versão validada.
- `container_name: drone-mediamtx`: define o nome do container.
- `restart: "no"`: os containers não iniciam automaticamente ao reiniciar o Docker ou o computador. Use a opção **Iniciar servidor** do menu. O Docker precisa estar ativo no computador.
- `ports`: mapeia as quatro portas de streaming do computador para o container MediaMTX. O serviço `painel` publica separadamente a porta `8080` e monta `web/` em modo somente leitura no Nginx.
- `./mediamtx.yml:/mediamtx.yml:ro`: fornece o arquivo local ao MediaMTX em modo somente leitura dentro do container. Edite o arquivo no computador e reinicie o serviço para aplicar as alterações.

### MediaMTX

```yaml
rtmp: yes
rtmpAddress: :1935

hls: yes
hlsAddress: :8888

webrtc: yes
webrtcAddress: :8889

webrtcAdditionalHosts:
  - 192.168.15.2

paths:
  drone:
    source: publisher
```

As opções `rtmp`, `hls` e `webrtc` habilitam os protocolos; os campos `Address` definem suas portas de escuta. Em `paths`, `source: publisher` indica que o caminho `drone` aguarda um publicador externo, neste caso o dispositivo DJI. Não há endereço de câmera para o servidor buscar automaticamente.

A porta UDP `8189` é publicada pelo Compose e depende do valor padrão correspondente da imagem MediaMTX, pois não foi definida explicitamente neste YAML. Opções omitidas seguem os padrões da versão da imagem utilizada.

## Operação do servidor

```bash
# Consultar o estado
docker compose ps

# Consultar as últimas 100 linhas de log
docker compose logs --tail=100 mediamtx

# Aplicar alterações no mediamtx.yml
docker compose restart mediamtx

# Reaplicar alterações no docker-compose.yml
docker compose up -d

# Parar os containers sem removê-los
docker compose stop

# Iniciar novamente os containers parados
docker compose start

# Parar e remover os containers e a rede criada pelo Compose
docker compose down
```

## Solução de problemas

| Sintoma | O que verificar |
| --- | --- |
| O container não inicia | Consulte os logs; verifique o YAML, a montagem de `mediamtx.yml` e possíveis conflitos de portas. A validação do Compose não valida as opções internas do MediaMTX. |
| O DJI não conecta | Confira o IP, a porta TCP `1935`, o firewall, a conectividade da rede e a URL terminando em `/drone`. |
| A página abre, mas não há vídeo | Confirme que o DJI iniciou a publicação e que os logs mostram um publicador no caminho `drone`. |
| HLS funciona, mas WebRTC não | Confira `webrtcAdditionalHosts`, o acesso à porta UDP `8189` e a compatibilidade do codec no navegador. |
| A transmissão trava | Verifique o sinal Wi-Fi e a capacidade da rede; experimente reduzir a taxa de bits ou a resolução no DJI. |
| O acesso parou após mudança de rede | Atualize o IP nas URLs e em `webrtcAdditionalHosts`, depois reinicie o serviço. |

## Escopo da configuração atual

- A configuração foi preparada para acesso por rede local. Acesso pela internet exige planejamento adicional de roteamento, proteção de acesso e conectividade WebRTC.
- Não há autenticação nem criptografia configuradas explicitamente para os acessos documentados. Não exponha essas portas diretamente à internet sem configurar a proteção adequada.
- Não há gravação em disco configurada; o projeto distribui a transmissão ao vivo.
- Não há transcodificação configurada. A reprodução depende dos codecs enviados pelo DJI e aceitos pelo cliente.
- O projeto não configura encaminhamento para YouTube, Twitch ou outras plataformas.

## Validação

A estrutura do `docker-compose.yml` foi validada com `docker compose config --quiet`. A transmissão completa precisa ser confirmada com o equipamento DJI e um espectador na rede; essa validação de estrutura não comprova compatibilidade com o drone nem reprodução de vídeo.

Para verificar a lógica da interface sem instalar dependências adicionais, use `node tests/web.test.cjs` (Node.js é necessário apenas para esse teste). O teste simula os elementos da página e cobre URLs de reprodução, preferências, IP inválido, restauração e informações do criador. Ele não substitui um teste visual em navegador nem uma transmissão real.

A logomarca do projeto está em `web/assets/logo.png`, com o original preservado. Ela aparece na navegação e na seção Sobre; `web/assets/favicon.ico` define o ícone da aba do navegador.
