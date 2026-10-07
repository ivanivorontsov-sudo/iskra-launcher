#!/usr/bin/env bash
# Собирает портативный zip из опубликованной папки (кроссплатформенно).
set -euo pipefail
VER="${1:-1.0.0}"
PUB="${2:-publish}"
OUT="${3:-dist}"
mkdir -p "$OUT"
STAGE=$(mktemp -d)/Iskra
mkdir -p "$STAGE"
cp "$PUB/Iskra.exe" "$STAGE/"
[ -d "$PUB/Fonts/licenses" ] && cp -r "$PUB/Fonts/licenses" "$STAGE/licenses"
[ -f THIRD-PARTY-NOTICES.md ] && cp THIRD-PARTY-NOTICES.md "$STAGE/"
[ -f LICENSE ] && cp LICENSE "$STAGE/"
cat > "$STAGE/ПРОЧТИ.txt" <<TXT
Искра $VER — портативная версия.
Запустите Iskra.exe. Права администратора не нужны.
Данные хранятся в %LOCALAPPDATA%\\Iskra.
Чтобы вернуть оформление Windows к исходному: значок в трее → «Вернуть Windows как было»,
либо запустите %LOCALAPPDATA%\\Iskra\\Вернуть-как-было.ps1.
TXT
( cd "$(dirname "$STAGE")" && zip -r -q "$OLDPWD/$OUT/Iskra-Portable-$VER.zip" Iskra )
echo "$OUT/Iskra-Portable-$VER.zip"
