#!/usr/bin/env bash
set -Eeuo pipefail
export LANG=C.UTF-8
cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.."
python3 windows-install/prepare.py
mcs -sdk:4.7.2 -langversion:7.2 -target:winexe -platform:x64 -optimize+ \
    -win32icon:windows-install/assets/drone.ico \
    -win32manifest:windows-install/src/app.manifest \
    -out:windows-install/build/DroneStreamingServer.exe \
    -r:System.Windows.Forms -r:System.Drawing -r:System.Core -r:System.Web.Extensions \
    -r:windows-install/build/Microsoft.Web.WebView2.Core.dll \
    -r:windows-install/build/Microsoft.Web.WebView2.WinForms.dll \
    windows-install/src/*.cs
mcs -sdk:4.7.2 -target:exe -out:windows-install/build/CoreTests.exe \
    -r:System.Core -r:System.Web.Extensions \
    windows-install/src/ServerConfig.cs windows-install/tests/CoreTests.cs
mono windows-install/build/CoreTests.exe windows-install/build/test-mediamtx.yml
makensis -V3 windows-install/installer.nsi
file install-windows.exe windows-install/build/DroneStreamingServer.exe
sha256sum install-windows.exe > windows-install/installer.sha256
