'use strict';
const $ = (id) => document.getElementById(id);
const defaults = {host: location.hostname, protocol: 'webrtc', autoplay: false};
const storageKey = 'drone-stream-preferences-v1';
let settings = {...defaults};
try {
  const saved = JSON.parse(localStorage.getItem(storageKey) || localStorage.getItem('dji-stream-preferences-v1'));
  if (saved && validIPv4(saved.host) && ['webrtc', 'hls'].includes(saved.protocol)) {
    settings = {host: saved.host, protocol: saved.protocol, autoplay: saved.autoplay === true};
  }
} catch { /* Armazenamento bloqueado: usar as preferências desta sessão. */ }
let toastTimer;
function toast(message) {
  $('toast').textContent = message;
  $('toast').hidden = false;
  clearTimeout(toastTimer);
  toastTimer = setTimeout(() => { $('toast').hidden = true; }, 4000);
}
function validIPv4(host) {
  return typeof host === 'string' && /^(\d{1,3}\.){3}\d{1,3}$/.test(host) && host.split('.').every(part => Number(part) <= 255) && !['0', '127'].includes(host.split('.')[0]);
}
function renderSettings() {
  $('server-ip').value = settings.host;
  $('protocol').value = settings.protocol;
  $('autoplay').checked = settings.autoplay;
  $('rtmp-url').value = `rtmp://${settings.host}:1935/drone`;
  $('server-host').textContent = settings.host;
  $('mode-label').textContent = settings.protocol === 'hls' ? 'HLS' : 'WebRTC';
  $('protocol-tag').textContent = settings.protocol.toUpperCase();
}
function openPlayer() {
  const host = settings.host;
  // O hostname vem da URL do painel ou de um IPv4 validado, nunca de HTML livre.
  const url = new URL(location.href);
  url.protocol = 'http:';
  url.hostname = host;
  url.port = settings.protocol === 'hls' ? '8888' : '8889';
  url.pathname = '/drone/';
  url.search = '?autoplay=true&muted=true';
  url.hash = '';
  $('player').src = url.href;
  $('player').hidden = false;
  $('placeholder').hidden = true;
  $('player-state').textContent = 'Player aberto · aguardando vídeo do drone';
}
function navigate() {
  const requested = location.hash.slice(1);
  const view = ['transmissao', 'configuracoes', 'sobre'].includes(requested) ? requested : 'transmissao';
  document.querySelectorAll('.view').forEach(section => { section.hidden = section.id !== view; });
  document.querySelectorAll('[data-view]').forEach(link => {
    const active = link.dataset.view === view;
    link.classList.toggle('active', active);
    if (active) link.setAttribute('aria-current', 'page'); else link.removeAttribute('aria-current');
  });
  $('breadcrumb').textContent = {transmissao: 'Transmissão', configuracoes: 'Configurações', sobre: 'Sobre o projeto'}[view];
}
function persist() {
  try { localStorage.setItem(storageKey, JSON.stringify(settings)); return true; }
  catch { toast('Preferências aplicadas nesta sessão. O navegador bloqueou o salvamento.'); return false; }
}
$('play').addEventListener('click', openPlayer);
$('reconnect').addEventListener('click', openPlayer);
$('fullscreen').addEventListener('click', async () => {
  try { await $('player-stage').requestFullscreen(); }
  catch { toast('Tela cheia indisponível neste navegador.'); }
});
$('copy').addEventListener('click', async () => {
  const input = $('rtmp-url');
  try {
    if (navigator.clipboard && window.isSecureContext) await navigator.clipboard.writeText(input.value);
    else { input.focus(); input.select(); if (!document.execCommand('copy')) throw new Error('copy'); }
    toast('Endereço RTMP copiado!');
  } catch { input.focus(); input.select(); toast('Use Ctrl+C ou copie o endereço selecionado.'); }
});
$('settings-form').addEventListener('submit', event => {
  event.preventDefault();
  const host = $('server-ip').value.trim();
  if (!validIPv4(host)) { toast('Informe um IPv4 válido da rede, como 192.168.15.2.'); $('server-ip').focus(); return; }
  settings = {host, protocol: $('protocol').value, autoplay: $('autoplay').checked};
  const saved = persist();
  renderSettings();
  if (!$('player').hidden || settings.autoplay) openPlayer();
  if (saved) toast('Preferências salvas neste navegador.');
});
$('reset').addEventListener('click', () => {
  settings = {...defaults};
  const saved = persist();
  renderSettings();
  $('player').removeAttribute('src'); $('player').hidden = true; $('placeholder').hidden = false;
  $('player-state').textContent = 'Player em espera';
  if (saved) toast('Preferências restauradas.');
});
window.addEventListener('hashchange', navigate);
renderSettings(); navigate();
if (settings.autoplay) openPlayer();
fetch('about.json').then(response => { if (!response.ok) throw new Error('about'); return response.json(); }).then(about => {
  if (typeof about.name === 'string') $('creator-name').textContent = about.name;
  if (typeof about.description === 'string') $('creator-description').textContent = about.description;
  if (typeof about.email === 'string' && /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(about.email)) {
    $('creator-email').textContent = about.email;
    $('creator-email').href = `mailto:${about.email}`;
    $('creator-email').hidden = false;
  }
  if (typeof about.pix === 'string' && about.pix.trim()) $('pix-key').value = about.pix.trim();
  if (about.url) {
    const url = new URL(about.url);
    if (['https:', 'http:'].includes(url.protocol)) { $('creator-link').href = url.href; $('creator-link').hidden = false; }
  }
}).catch(() => { /* Manter a apresentação padrão quando não houver dados do criador. */ });
