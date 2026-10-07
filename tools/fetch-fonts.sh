#!/usr/bin/env bash
# Скачивает статические TTF (OFL) из Google Fonts и тексты лицензий.
set -euo pipefail
OUT=src/Iskra.App/Fonts
mkdir -p "$OUT/licenses"
declare -A DIRS=( ["Inter"]=inter ["Open Sans"]=opensans ["Caveat"]=caveat ["Marck Script"]=marckscript ["Amatic SC"]=amaticsc ["JetBrains Mono"]=jetbrainsmono ["Press Start 2P"]=pressstart2p ["Russo One"]=russoone ["Exo 2"]=exo2 ["Comfortaa"]=comfortaa ["Manrope"]=manrope ["Lora"]=lora ["Pacifico"]=pacifico ["Neucha"]=neucha )
declare -A W=( ["Inter"]="400;600" ["Open Sans"]="300;400;600" ["Caveat"]="400;700" ["Marck Script"]="" ["Amatic SC"]="400;700" ["JetBrains Mono"]="400;700" ["Press Start 2P"]="" ["Russo One"]="" ["Exo 2"]="300;600" ["Comfortaa"]="400;700" ["Manrope"]="400;700" ["Lora"]="400;600" ["Pacifico"]="" ["Neucha"]="" )
for fam in "${!DIRS[@]}"; do
  q=${fam// /+}; [ -n "${W[$fam]}" ] && q="$q:wght@${W[$fam]}"
  css=$(curl -fsS "https://fonts.googleapis.com/css2?family=$q&subset=cyrillic")
  echo "$css" | awk '/font-weight/{w=$2} /src:/{match($0,/https[^)]*/); print w" "substr($0,RSTART,RLENGTH)}' | tr -d ';' | while read -r w url; do
    f="$OUT/${fam// /}-$w.ttf"; curl -fsSL "$url" -o "$f"; echo "$f"
  done
  d=${DIRS[$fam]}
  curl -fsSL "https://raw.githubusercontent.com/google/fonts/main/ofl/$d/OFL.txt" -o "$OUT/licenses/${fam// /}-OFL.txt" || echo "NO LICENSE $fam"
done
