const assert = require('assert');
const fs = require('fs');
const vm = require('vm');
const path = require('path');
const elements = {};
function element(id) {
  if (!elements[id]) elements[id] = {
    id, hidden: id === 'player', value: '', checked: false, dataset: {}, handlers: {},
    classList: {toggle() {}}, addEventListener(event, handler) {this.handlers[event] = handler;},
    setAttribute(name, value) {this[name] = value;}, removeAttribute(name) {delete this[name];},
    focus() {}, select() {}, requestFullscreen() {return Promise.resolve();}
  };
  return elements[id];
}
const data = new Map();
const context = {
  document: {getElementById: element, querySelectorAll: () => [], execCommand: () => true},
  location: {hostname: '192.168.15.2', href: 'http://192.168.15.2:8080/', hash: ''},
  localStorage: {getItem: key => data.get(key) || null, setItem: (key, value) => data.set(key, value)},
  window: {addEventListener() {}, isSecureContext: false}, navigator: {}, URL,
  setTimeout: () => 1, clearTimeout() {},
  fetch: () => Promise.resolve({ok: true, json: () => Promise.resolve({
    name: 'Paulo Eugenio', email: 'pauloeugenio@hotmail.com.br', url: 'https://github.com/pauloeugenio', pix: ''
  })})
};
vm.createContext(context);
vm.runInContext(fs.readFileSync(path.join(__dirname, '../web/app.js'), 'utf8'), context);
assert.equal(element('rtmp-url').value, 'rtmp://192.168.15.2:1935/drone');
element('play').handlers.click();
assert.equal(element('player').src, 'http://192.168.15.2:8889/drone/?autoplay=true&muted=true');
element('server-ip').value = '10.0.0.4';
element('protocol').value = 'hls';
element('settings-form').handlers.submit({preventDefault() {}});
assert.equal(element('player').src, 'http://10.0.0.4:8888/drone/?autoplay=true&muted=true');
assert.equal(JSON.parse(data.get('drone-stream-preferences-v1')).host, '10.0.0.4');
element('server-ip').value = '999.0.0.1';
element('settings-form').handlers.submit({preventDefault() {}});
assert.equal(element('rtmp-url').value, 'rtmp://10.0.0.4:1935/drone');
element('reset').handlers.click();
assert.equal(element('player').hidden, true);
assert.equal(element('rtmp-url').value, 'rtmp://192.168.15.2:1935/drone');
setImmediate(() => {
  assert.equal(element('creator-name').textContent, 'Paulo Eugenio');
  assert.equal(element('creator-email').href, 'mailto:pauloeugenio@hotmail.com.br');
  assert.equal(element('creator-link').href, 'https://github.com/pauloeugenio');
  console.log('OK: URLs, player WebRTC/HLS, preferências, validação de IP, restauração e dados do criador.');
});
