#!/bin/zsh
cd "${0:A:h}"
export DOTNET_CLI_HOME="${TMPDIR:-/tmp}/nhse-dotnet-home"
export DOTNET_CLI_TELEMETRY_OPTOUT=1
if lsof -nP -iTCP:8765 -sTCP:LISTEN >/dev/null 2>&1; then
  echo "NHSE 网页工具已在运行。请关闭之前的终端窗口，再双击此文件启动新版本。"
  open 'http://127.0.0.1:8765'
  read '?按回车键关闭此窗口。'
  exit 1
fi
if [[ -x ./.dotnet/dotnet ]]; then
  DOTNET_BIN="./.dotnet/dotnet"
elif command -v dotnet >/dev/null 2>&1; then
  DOTNET_BIN="$(command -v dotnet)"
else
  echo "没有找到 .NET 10 SDK。请安装 .NET 10 SDK 后重新运行。"
  read '?按回车键关闭此窗口。'
  exit 1
fi
if [[ ! -f publish/NHSE.Web.dll || Program.cs -nt publish/NHSE.Web.dll || EditorApi.cs -nt publish/NHSE.Web.dll || index.html -nt publish/index.html ]]; then
  "$DOTNET_BIN" publish NHSE.Web.csproj -c Release -o publish
  if [[ $? -ne 0 ]]; then
    read '?编译失败，按回车键关闭此窗口。'
    exit 1
  fi
fi
(sleep 2; open 'http://127.0.0.1:8765') &
"$DOTNET_BIN" publish/NHSE.Web.dll
