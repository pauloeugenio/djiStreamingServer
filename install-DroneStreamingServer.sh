#!/usr/bin/env bash
set -Eeuo pipefail

SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_DIR="$SCRIPT_DIR/docker-install"
# Manter a identidade Compose anterior à reorganização das pastas.
PROJECT_NAME="${COMPOSE_PROJECT_NAME:-$(basename -- "$SCRIPT_DIR" | tr '[:upper:]' '[:lower:]')}"
# Compatibilidade com configurações anteriores do projeto.
export DRONE_SERVER_IP="${DRONE_SERVER_IP:-${DJI_SERVER_IP:-}}"
DOCKER=(docker)

fail() { printf 'Erro: %s\n' "$*" >&2; return 1; }
root() {
    if (( EUID == 0 )); then "$@"; else
        command -v sudo >/dev/null || { fail 'Instale sudo ou execute como root.'; return 1; }
        sudo "$@"
    fi
}
compose() { "${DOCKER[@]}" compose --project-name "$PROJECT_NAME" --project-directory "$PROJECT_DIR" -f "$PROJECT_DIR/docker-compose.yml" "$@"; }

check_files() {
    [[ -f "$PROJECT_DIR/docker-compose.yml" && -f "$PROJECT_DIR/mediamtx.yml" && -f "$PROJECT_DIR/web/index.html" ]] ||
        fail 'A pasta docker-install deve estar ao lado do instalador e conter docker-compose.yml, mediamtx.yml e web/index.html.'
}

connect_docker() {
    command -v docker >/dev/null || { fail 'Docker ausente. Execute a opção 1 (Instalar).'; return 1; }
    DOCKER=(docker)
    if ! docker info >/dev/null 2>&1; then
        printf 'Verificando acesso administrativo ao Docker...\n'
        root docker info >/dev/null || { fail 'Docker indisponível. Execute Instalar ou verifique o serviço/contexto Docker.'; return 1; }
        if (( EUID != 0 )); then DOCKER=(sudo --preserve-env=DRONE_SERVER_IP docker); fi
    fi
    "${DOCKER[@]}" compose version >/dev/null || { fail 'Docker Compose v2 ausente. Execute Instalar.'; return 1; }
}

install_dependencies() {
    check_files
    [[ -r /etc/os-release ]] || { fail 'Instalação automática disponível apenas para Ubuntu e Debian com systemd.'; return 1; }
    # Arquivo de identificação fornecido pelo sistema operacional.
    . /etc/os-release
    case "${ID:-}" in
        ubuntu|debian) ;;
        *) fail 'Instalação automática disponível apenas para Ubuntu e Debian. Instale Docker e Compose v2 manualmente neste sistema.'; return 1 ;;
    esac
    [[ -d /run/systemd/system ]] || { fail 'A instalação automática requer systemd ativo. Em WSL, habilite systemd ou use Docker Desktop.'; return 1; }
    printf 'Instalando dependências em %s (pode solicitar senha do sudo)...\n' "${PRETTY_NAME:-$ID}"
    root apt-get update
    root apt-get install -y ca-certificates curl iproute2 xdg-utils

    if ! command -v docker >/dev/null || ! docker compose version >/dev/null 2>&1; then
        # Não substituir automaticamente instalações de outras origens.
        local package conflicts=()
        for package in docker.io docker-compose docker-compose-v2 docker-doc docker-buildx podman-docker containerd runc; do
            if [[ "$(dpkg-query -W -f='${Status}' "$package" 2>/dev/null || true)" == 'install ok installed' ]]; then
                conflicts+=("$package")
            fi
        done
        if ((${#conflicts[@]})); then
            fail "Pacotes que conflitam com Docker CE: ${conflicts[*]}. Revise a instalação existente antes de migrar para o repositório oficial."
            return 1
        fi
        local codename="${UBUNTU_CODENAME:-${VERSION_CODENAME:-}}" architecture
        [[ -n "$codename" ]] || { fail 'Não foi possível identificar a versão da distribuição.'; return 1; }
        architecture="$(dpkg --print-architecture)"
        root install -m 0755 -d /etc/apt/keyrings
        root curl -fsSL "https://download.docker.com/linux/$ID/gpg" -o /etc/apt/keyrings/docker.asc
        root chmod a+r /etc/apt/keyrings/docker.asc
        printf 'Types: deb\nURIs: https://download.docker.com/linux/%s\nSuites: %s\nComponents: stable\nArchitectures: %s\nSigned-By: /etc/apt/keyrings/docker.asc\n' \
            "$ID" "$codename" "$architecture" | root tee /etc/apt/sources.list.d/docker.sources >/dev/null
        root apt-get update
        root apt-get install -y docker-ce docker-ce-cli containerd.io docker-buildx-plugin docker-compose-plugin
    fi
    root systemctl enable --now docker
    connect_docker
    compose config --quiet
    compose pull
    printf '\nInstalação concluída. Escolha 2 — Iniciar servidor para iniciar a transmissão e abrir o painel.\n'
}

valid_ipv4() {
    local value="$1" octet
    [[ "$value" =~ ^[0-9]{1,3}(\.[0-9]{1,3}){3}$ ]] || return 1
    local -a octets
    IFS=. read -r -a octets <<< "$value"
    for octet in "${octets[@]}"; do ((10#$octet <= 255)) || return 1; done
    [[ "${octets[0]}" != 127 && "${octets[0]}" != 0 ]] || return 1
}

select_ip() {
    if [[ -n "${DRONE_SERVER_IP:-}" ]]; then
        valid_ipv4 "$DRONE_SERVER_IP" || { fail 'DRONE_SERVER_IP deve ser um IPv4 válido da rede do servidor.'; return 1; }
        export DRONE_SERVER_IP
        return
    fi
    command -v ip >/dev/null || { fail 'Comando ip ausente. Execute Instalar ou defina DRONE_SERVER_IP.'; return 1; }
    local -a addresses=()
    local line choice default_interface
    default_interface="$(ip -4 route show default | awk '{for(i=1;i<=NF;i++) if($i=="dev") {print $(i+1); exit}}')"
    while IFS= read -r line; do
        [[ -n "$line" ]] && addresses+=("$line")
    done < <(ip -o -4 addr show up scope global | awk '$2 !~ /^(lo|docker.*|veth.*|br-.*|virbr.*|vnet.*|tun.*|tap.*|wg.*)$/ {split($4,a,"/"); print a[1] " (" $2 ")"}')
    ((${#addresses[@]})) || { fail 'Nenhum IPv4 encontrado. Conecte-se à rede ou defina DRONE_SERVER_IP.'; return 1; }
    # Preferir a conexão principal, desde que ela esteja entre as interfaces elegíveis.
    for line in "${addresses[@]}"; do
        if [[ "$line" == *" ($default_interface)" ]]; then
            DRONE_SERVER_IP="${line%% *}"
            export DRONE_SERVER_IP
            return
        fi
    done
    if ((${#addresses[@]} == 1)); then
        DRONE_SERVER_IP="${addresses[0]%% *}"
    else
        printf '\nEscolha o endereço da rede acessível pelo controle do drone e pelos espectadores:\n'
        local index
        for index in "${!addresses[@]}"; do printf '%d) %s\n' "$((index+1))" "${addresses[index]}"; done
        read -r -p 'Endereço: ' choice || return 1
        [[ "$choice" =~ ^[1-9][0-9]*$ && ${#choice} -le 3 ]] || { fail 'Seleção inválida.'; return 1; }
        ((choice <= ${#addresses[@]})) || { fail 'Seleção inválida.'; return 1; }
        DRONE_SERVER_IP="${addresses[choice-1]%% *}"
    fi
    export DRONE_SERVER_IP
}

start_server() {
    printf '\n[1/4] Verificando arquivos e acesso ao Docker...\n'
    check_files
    connect_docker
    printf '[2/4] Identificando o IP da rede...\n'
    select_ip
    compose config --quiet
    printf '[3/4] Iniciando os containers (o primeiro uso pode baixar imagens)...\n'
    compose up -d
    printf '[4/4] Aguardando o painel web responder...\n'
    local panel_url="http://$DRONE_SERVER_IP:8080/"
    if ! command -v curl >/dev/null; then
        fail 'curl ausente. Execute a opção Instalar e tente novamente.'
        return 1
    fi
    local attempt ready=0
    for ((attempt=0; attempt<15; attempt++)); do
        if curl --noproxy '*' --fail --silent --output /dev/null --connect-timeout 1 --max-time 2 "$panel_url"; then
            ready=1
            break
        fi
        sleep 1
    done
    if ((ready == 0)); then
        compose ps
        compose logs --tail=30
        fail "O painel não respondeu em $panel_url. Confira os logs acima e possíveis conflitos na porta 8080."
        return 1
    fi
    printf '\nServidor iniciado. IP: %s\n' "$DRONE_SERVER_IP"
    printf '\nColoque este endereço no seu drone (controle/aplicativo do drone, opção RTMP):\n  rtmp://%s:1935/drone\n' "$DRONE_SERVER_IP"
    printf '\nAcesse no seu navegador através deste endereço para ver o vídeo:\n  http://%s:8889/drone/\n' "$DRONE_SERVER_IP"
    printf '\nEndereço alternativo para assistir pelo navegador (HLS):\n  http://%s:8888/drone/\n' "$DRONE_SERVER_IP"
    printf '\nInicie a transmissão no drone para aparecer o vídeo.\n'
    printf '\nPainel do servidor:\n  %s\n' "$panel_url"
    if [[ -n "${DISPLAY:-}${WAYLAND_DISPLAY:-}" ]] && command -v xdg-open >/dev/null && (( EUID != 0 )); then
        local browser_log browser_pid
        browser_log="$(mktemp "${TMPDIR:-/tmp}/drone-browser.XXXXXX.log")"
        printf 'Abrindo o painel no navegador padrão...\nLog do navegador: %s\n' "$browser_log"
        nohup xdg-open "$panel_url" >"$browser_log" 2>&1 </dev/null &
        browser_pid=$!
        sleep 0.5
        if ! kill -0 "$browser_pid" 2>/dev/null; then
            if ! wait "$browser_pid"; then
                printf 'Não foi possível abrir o navegador. Abra manualmente: %s\nDetalhes: %s\n' "$panel_url" "$browser_log" >&2
            fi
        fi
    else
        printf 'Abertura automática indisponível nesta sessão (root, sem ambiente gráfico ou sem xdg-open).\nAbra o endereço do painel no navegador de um dispositivo da mesma rede.\n'
    fi
}

stop_server() {
    check_files
    connect_docker
    compose stop
    printf '\nServidor parado.\n'
}

main() {
    local choice
    while true; do
        printf '\n=== Drone Streaming Server ===\n1) Instalar\n2) Iniciar servidor\n3) Parar servidor\n4) Sair\n'
        read -r -p 'Opção: ' choice || break
        # Cada ação roda em subprocesso para interromper na primeira falha,
        # mantendo o menu disponível para uma nova tentativa.
        case "$choice" in
            1|2|3)
                local action
                case "$choice" in 1) action=install_dependencies ;; 2) action=start_server ;; 3) action=stop_server ;; esac
                set +e
                ( set -e; "$action" )
                local status=$?
                set -e
                if ((status != 0)); then printf '\nOperação não concluída (código %s). Confira a mensagem acima.\n' "$status" >&2; fi
                ;;
            4) printf 'Até logo!\n'; break ;;
            *) printf 'Opção inválida. Escolha de 1 a 4.\n' ;;
        esac
    done
}

if [[ "${BASH_SOURCE[0]}" == "$0" ]]; then main "$@"; fi
