# satis/Lisans-Sozlesmesi.txt -> kurulum/Lisans.rtf (kurulum sihirbazında gösterilen metin).
# Kullanım (proje kökünden): python satis/rtf_uret.py
# Satır başları: "# " başlık, "## " ara başlık, "! " kalın uyarı; diğerleri paragraf.
import pathlib

kok = pathlib.Path(__file__).resolve().parent.parent
metin = (kok / "satis" / "Lisans-Sozlesmesi.txt").read_text(encoding="utf-8")


def kac(s: str) -> str:
    out = []
    for ch in s:
        if ch in "\\{}":
            out.append("\\" + ch)
        elif ord(ch) < 128:
            out.append(ch)
        else:
            kod = ord(ch)
            out.append(f"\\u{kod if kod < 32768 else kod - 65536}?")
    return "".join(out)


satirlar = [r"{\rtf1\ansi\ansicpg1254\deff0{\fonttbl{\f0\fswiss\fcharset162 Segoe UI;}}",
            r"\viewkind4\uc1\pard\sa120\f0\fs18"]
for satir in metin.splitlines():
    satir = satir.rstrip()
    if not satir:
        continue
    if satir.startswith("## "):
        satirlar.append(r"{\b " + kac(satir[3:]) + r"}\par")
    elif satir.startswith("# "):
        satirlar.append(r"{\b\fs22 " + kac(satir[2:]) + r"}\par")
    elif satir.startswith("! "):
        satirlar.append(r"{\b\i " + kac(satir[2:]) + r"}\par")
    else:
        satirlar.append(kac(satir) + r"\par")
satirlar.append("}")
(kok / "kurulum" / "Lisans.rtf").write_text("\n".join(satirlar), encoding="ascii")
print("kurulum/Lisans.rtf yazıldı")
