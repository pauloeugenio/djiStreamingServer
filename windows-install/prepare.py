"""Fetch official, version-pinned Windows dependencies and stage the installer."""
import base64
import hashlib
import json
import pathlib
import shutil
import urllib.request
import zipfile

ROOT = pathlib.Path(__file__).resolve().parent
CACHE = ROOT / '.cache'
BUILD = ROOT / 'build'
MTX_VERSION = '1.21.2'
WEBVIEW_VERSION = '1.0.3650.58'

def fetch(url, name):
    target = CACHE / name
    if not target.exists():
        request = urllib.request.Request(url, headers={'User-Agent': 'DroneStreamingServer-build/1.0'})
        with urllib.request.urlopen(request, timeout=120) as response, target.with_suffix('.partial').open('wb') as output:
            shutil.copyfileobj(response, output)
        target.with_suffix('.partial').replace(target)
    return target

def stage():
    CACHE.mkdir(exist_ok=True)
    BUILD.mkdir(exist_ok=True)
    release = json.loads(fetch(f'https://api.github.com/repos/bluenviron/mediamtx/releases/tags/v{MTX_VERSION}', f'mediamtx-{MTX_VERSION}.json').read_text())
    asset = next(a for a in release['assets'] if a['name'] == f'mediamtx_v{MTX_VERSION}_windows_amd64.zip')
    archive = fetch(asset['browser_download_url'], asset['name'])
    digest = hashlib.sha256(archive.read_bytes()).hexdigest()
    expected = asset.get('digest', '')
    if expected and expected.startswith('sha256:'):
        assert digest == expected.split(':', 1)[1], 'MediaMTX checksum mismatch'
    else:
        checksum_asset = next(a for a in release['assets'] if 'sha256' in a['name'].lower() or 'checksum' in a['name'].lower())
        checksums = fetch(checksum_asset['browser_download_url'], checksum_asset['name']).read_text()
        assert any(digest in line and asset['name'] in line for line in checksums.splitlines()), 'MediaMTX checksum mismatch'
    with zipfile.ZipFile(archive) as package:
        for source, output in [('mediamtx.exe', 'mediamtx.exe'), ('LICENSE', 'LICENSE-MediaMTX.txt')]:
            member = next(n for n in package.namelist() if pathlib.PurePosixPath(n).name == source)
            (BUILD / output).write_bytes(package.read(member))
    base = f'https://api.nuget.org/v3-flatcontainer/microsoft.web.webview2/{WEBVIEW_VERSION}/microsoft.web.webview2.{WEBVIEW_VERSION}.nupkg'
    nuget = fetch(base, f'webview2-{WEBVIEW_VERSION}.zip')
    registration = json.loads(fetch(f'https://api.nuget.org/v3/registration5-semver1/microsoft.web.webview2/{WEBVIEW_VERSION}.json', f'webview2-{WEBVIEW_VERSION}-registration.json').read_text())
    catalog = json.loads(fetch(registration['catalogEntry'], f'webview2-{WEBVIEW_VERSION}-catalog.json').read_text())
    assert catalog['packageHashAlgorithm'].upper() == 'SHA512', 'Unexpected NuGet hash algorithm'
    sha512 = catalog['packageHash']
    assert base64.b64encode(hashlib.sha512(nuget.read_bytes()).digest()).decode() == sha512, 'WebView2 checksum mismatch'
    with zipfile.ZipFile(nuget) as package:
        for source in ['lib/net462/Microsoft.Web.WebView2.Core.dll', 'lib/net462/Microsoft.Web.WebView2.WinForms.dll', 'runtimes/win-x64/native/WebView2Loader.dll']:
            (BUILD / pathlib.PurePosixPath(source).name).write_bytes(package.read(source))
        license_name = next(n for n in package.namelist() if n.lower() in ('license.txt', 'license'))
        (BUILD / 'LICENSE-WebView2.txt').write_bytes(package.read(license_name))
    setup = fetch('https://go.microsoft.com/fwlink/p/?LinkId=2124703', 'MicrosoftEdgeWebview2Setup.exe')
    assert setup.read_bytes()[:2] == b'MZ', 'WebView2 bootstrapper is not an executable'
    shutil.copy2(setup, BUILD / setup.name)
    shutil.copy2(ROOT / 'assets' / 'qrcode-pix.png', BUILD / 'qrcode-pix.png')
    for asset_name in ['logo.png', 'drone.ico']:
        shutil.copy2(ROOT / 'assets' / asset_name, BUILD / asset_name)
    # Padrões versionados separados de about.json, que o usuário pode personalizar.
    shutil.copy2(ROOT / 'about.json', BUILD / 'about-defaults.json')
    for source in ['about.json', 'README.md']:
        shutil.copy2(ROOT / source, BUILD / source)
    (BUILD / 'DroneStreamingServer.exe.config').write_text('''<?xml version="1.0" encoding="utf-8"?>
<configuration><startup useLegacyV2RuntimeActivationPolicy="true"><supportedRuntime version="v4.0" sku=".NETFramework,Version=v4.7.2" /></startup></configuration>\n''')
    manifest = {'mediamtx': MTX_VERSION, 'webview2_sdk': WEBVIEW_VERSION, 'sha256': {p.name: hashlib.sha256(p.read_bytes()).hexdigest() for p in [archive, nuget, setup]}}
    (ROOT / 'dependencies.json').write_text(json.dumps(manifest, indent=2) + '\n')
    print('Dependências oficiais verificadas e preparadas.', flush=True)

if __name__ == '__main__':
    stage()
