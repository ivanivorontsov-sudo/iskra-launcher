#!/usr/bin/env python3
# Рендерит представительные мокапы тем Искры в PNG из определений тем (цвета/шрифты настоящие, OFL).
import json, math, os, random
from PIL import Image, ImageDraw, ImageFont, ImageFilter

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
FONTS = os.path.join(ROOT, "src/Iskra.App/Fonts")
OUT = os.path.join(ROOT, "screens")
os.makedirs(OUT, exist_ok=True)

FONT_FILE = {
 "Inter":"Inter-400.ttf","Inter SemiBold":"Inter-600.ttf","Open Sans":"OpenSans-400.ttf","Open Sans Light":"OpenSans-300.ttf",
 "Caveat":"Caveat-400.ttf","Marck Script":"MarckScript-400.ttf","Amatic SC":"AmaticSC-700.ttf","Neucha":"Neucha-400.ttf",
 "JetBrains Mono":"JetBrainsMono-400.ttf","Press Start 2P":"PressStart2P-400.ttf","Russo One":"RussoOne-400.ttf",
 "Exo 2":"Exo2-300.ttf","Comfortaa":"Comfortaa-400.ttf","Manrope":"Manrope-400.ttf","Lora":"Lora-400.ttf",
 "Lora SemiBold":"Lora-600.ttf","Pacifico":"Pacifico-400.ttf",
}
def font(name, size):
    f = FONT_FILE.get(name, "Inter-400.ttf")
    try: return ImageFont.truetype(os.path.join(FONTS, f), size)
    except Exception: return ImageFont.truetype(os.path.join(FONTS,"Inter-400.ttf"), size)

def argb(h):
    h=h.lstrip('#')
    if len(h)==8: a,r,g,b=int(h[0:2],16),int(h[2:4],16),int(h[4:6],16),int(h[6:8],16)
    else: r,g,b=int(h[0:2],16),int(h[2:4],16),int(h[4:6],16); a=255
    return (r,g,b,a)

def rc(draw, box, radius, fill):
    draw.rounded_rectangle(box, radius=radius, fill=fill)

def blend(c1,c2,t):
    return tuple(int(c1[i]*(1-t)+c2[i]*t) for i in range(3))

def wallpaper(t, W, H):
    p=t["Palette"]; bg1=argb(p["Background"]); bg2=argb(p["Background2"]); ac=argb(p["Accent"]); ac2=argb(p["Accent2"])
    img=Image.new("RGB",(W,H),bg1[:3]); d=ImageDraw.Draw(img)
    kind=t["System"]["Wallpaper"]
    for y in range(H):
        tt=y/H; d.line([(0,y),(W,y)], fill=blend(bg1[:3],bg2[:3],tt))
    rnd=random.Random(hash(t["Id"])&0xffff)
    if kind in ("Space",):
        for _ in range(260):
            x,y=rnd.randint(0,W),rnd.randint(0,H); r=rnd.choice([1,1,1,2])
            b=rnd.randint(120,255); d.ellipse([x,y,x+r,y+r], fill=(b,b,b))
    if kind=="NeonCity":
        hy=int(H*0.6)
        for i in range(-16,17):
            d.line([(W//2+i*10,hy),(W//2+i*(W//10),H)], fill=blend(bg2[:3],ac[:3],0.5), width=1)
        for i in range(1,12):
            yy=hy+int((i/12)**2*(H-hy)); d.line([(0,yy),(W,yy)], fill=blend(bg2[:3],ac[:3],0.5))
        d.ellipse([W//2-70,hy-70,W//2+70,hy+70], fill=ac2[:3])
    if kind in ("Aurora","MicaWaves"):
        glow=Image.new("RGB",(W,H),bg1[:3]); gd=ImageDraw.Draw(glow)
        for i in range(4):
            cx=int(W*(0.2+0.2*i)); cy=int(H*(0.3)); col=blend(bg1[:3], ac[:3] if i%2 else ac2[:3],0.5)
            gd.ellipse([cx-220,cy-160,cx+220,cy+160], fill=col)
        glow=glow.filter(ImageFilter.GaussianBlur(90))
        img=Image.blend(img,glow,0.5); d=ImageDraw.Draw(img)
    if kind=="Paper":
        for y in range(60,H,34): d.line([(0,y),(W,y)], fill=blend(bg1[:3],ac[:3],0.25))
        d.line([(90,0),(90,H)], fill=(217,83,79), width=2)
    if kind=="Terminal":
        for y in range(0,H,3): d.line([(0,y),(W,y)], fill=blend(bg1[:3],ac[:3],0.06))
    if kind=="Pixel":
        for _ in range(120):
            s=14; x=rnd.randint(0,W//s)*s; y=rnd.randint(0,H//s)*s
            d.rectangle([x,y,x+s,y+s], fill=blend(bg1[:3], rnd.choice([ac[:3],ac2[:3]]),0.5))
    if kind=="Sakura":
        for _ in range(60):
            x,y=rnd.randint(0,W),rnd.randint(0,H); s=rnd.randint(4,9)
            d.ellipse([x,y,x+s*2,y+s], fill=blend(bg1[:3],ac[:3],0.6))
    if kind=="Sunset":
        for i in range(6): d.rectangle([W//2-90,int(H*0.6)+i*9,W//2+90,int(H*0.6)+i*9+4], fill=bg1[:3])
        d.ellipse([W//2-80,int(H*0.6)-80,W//2+80,int(H*0.6)+80], fill=ac2[:3])
    return img

ICONS=["\uf0e0","A","B","C"]
def draw_launcher(t):
    W,H=760,560
    p=t["Palette"]; sh=t["Shape"]; fo=t["Fonts"]
    base=wallpaper(t,W,H)
    # полупрозрачная карточка
    card=Image.new("RGBA",(W-80,H-120),(0,0,0,0)); cd=ImageDraw.Draw(card)
    bg=argb(p["Background"]); op=float(p["BackgroundOpacity"])
    rad=int(sh["Radius"])+4
    rc(cd,[0,0,card.width-1,card.height-1], rad, (bg[0],bg[1],bg[2],int(op*255)))
    fg=argb(p["Foreground"]); fgd=argb(p["ForegroundDim"]); ac=argb(p["Accent"]); surf=argb(p["Surface"]); surfh=argb(p["SurfaceHover"])
    ui=fo["Ui"]; title=fo["Title"]
    # строка поиска
    sb=[18,18,card.width-18,18+64]; rc(cd,sb,int(sh["Radius"]), surf)
    sf=font(ui,int(fo["SearchSize"]))
    cd.text((sb[0]+52, 30), "Поиск приложений…", font=sf, fill=fgd[:3])
    cd.ellipse([sb[0]+16,38,sb[0]+34,56], outline=fgd[:3], width=2); cd.line([sb[0]+31,53,sb[0]+40,62], fill=fgd[:3], width=2)
    # заголовок секции
    hf=font(ui, 13)
    sec="РЕЗУЛЬТАТЫ" if fo.get("TitleCase")=="Upper" else ("результаты" if fo.get("TitleCase")=="Lower" else "Результаты")
    cd.text((26, 96), sec, font=hf, fill=fgd[:3])
    # строки результатов
    rows=[("Параметры","ms-settings","\uE713"),("Калькулятор","= 42","\uE8EF"),("Проводник","папка","\uE8B7"),("Терминал","команда","\uE756"),("Заметки","файл","\uE70B")]
    rowf=font(ui, int(fo["BaseSize"])+2); subf=font(ui,12)
    y=120
    for i,(name,sub,gl) in enumerate(rows):
        rh=52
        if i==0:
            rc(cd,[14,y,card.width-14,y+rh], int(sh["TileRadius"])+2, surfh)
        # значок
        ib=[26,y+9,26+34,y+9+34]; rc(cd,ib,int(sh["TileRadius"])+2,(ac[0],ac[1],ac[2],60))
        cd.text((ib[0]+8,ib[1]+6), chr(0x25C6), font=font(ui,16), fill=ac[:3])
        cd.text((76,y+8), name, font=rowf, fill=fg[:3])
        cd.text((76,y+28), sub, font=subf, fill=fgd[:3])
        if i==0:
            # подсветка совпадения первых букв
            cd.text((76,y+8), name[:3], font=font("Inter SemiBold" if ui=="Inter" else ui,int(fo["BaseSize"])+2), fill=ac[:3])
        y+=rh+4
    # рамка/свечение
    border=argb(p["Border"])
    if border[3]>20:
        cd.rounded_rectangle([0,0,card.width-1,card.height-1], radius=rad, outline=border[:3], width=max(1,int(sh["BorderThickness"])))
    base=base.convert("RGBA")
    base.alpha_composite(card,(40,70))
    # заголовок темы снизу
    d=ImageDraw.Draw(base)
    tf=font(title, 30)
    nm=t["Name"]
    d.text((44, H-46), nm, font=tf, fill=fg[:3])
    return base.convert("RGB")

themes=json.load(open("/tmp/themes.json"))
for t in themes:
    img=draw_launcher(t)
    path=os.path.join(OUT, f"theme-{t['Id']}.png")
    img.save(path)
    print("saved", path)
# контактный лист
cols=3; rows=math.ceil(len(themes)/cols)
tw,th=760,560; sc=0.5
sheet=Image.new("RGB",(int(cols*tw*sc)+40, int(rows*th*sc)+40),(18,18,22))
for i,t in enumerate(themes):
    im=Image.open(os.path.join(OUT,f"theme-{t['Id']}.png")).resize((int(tw*sc),int(th*sc)))
    x=20+(i%cols)*int(tw*sc); y=20+(i//cols)*int(th*sc)
    sheet.paste(im,(x,y))
sheet.save(os.path.join(OUT,"all-themes.png"))
print("saved contact sheet")
