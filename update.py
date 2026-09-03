import os
import shutil
import subprocess
import time
import json
import sys
import re

def print_step(msg):
    print(f"\n======== {msg} ========")

def run_cmd(cmd, cwd=None):
    print(f"Executing: {cmd}")
    res = subprocess.run(cmd, shell=True, cwd=cwd, text=True, capture_output=True)
    if res.returncode != 0:
        print(f"ERROR executing command:\n{res.stderr}")
        return False
    print(res.stdout)
    return True

def main():
    workspace = os.path.dirname(os.path.abspath(__file__))
    dist_dir = os.path.join(workspace, "dist")

    print_step("1. TEMİZLİK (Clean dist & build folders)")
    if os.path.exists(dist_dir):
        try:
            shutil.rmtree(dist_dir)
            print("Eski dist klasörü silindi.")
        except Exception as e:
            print(f"Silme uyarısı: {e}")

    print_step("2. YÜKLENME VE DERLEME ZAMANI ENJEKSİYONU (Build Timestamp)")
    now_str = time.strftime("%d.%m.%Y %H:%M:%S")
    home_razor_path = os.path.join(workspace, "AkilliMetinDuzenleyici.Web", "Pages", "Home.razor")
    if os.path.exists(home_razor_path):
        with open(home_razor_path, "r", encoding="utf-8") as f:
            content = f.read()
        content = re.sub(
            r'private string BuildTimestamp \{ get; set; \} = "[^"]*";',
            f'private string BuildTimestamp {{ get; set; }} = "{now_str}";',
            content
        )
        with open(home_razor_path, "w", encoding="utf-8") as f:
            f.write(content)
        print(f"Home.razor içerisine derleme zamanı yazıldı: {now_str}")

    print_step("3. BLAZOR WEBASSEMBLY DERLEME (dotnet publish)")
    publish_cmd = (
        f'dotnet publish "{os.path.join(workspace, "AkilliMetinDuzenleyici.Web", "AkilliMetinDuzenleyici.Web.csproj")}" '
        f'-c Release -o "{dist_dir}" /p:CompressBlazorUncompressedFiles=false'
    )
    if not run_cmd(publish_cmd, cwd=workspace):
        print("Derleme başarısız oldu!")
        sys.exit(1)

    print_step("4. SIKIŞTIRILMIŞ (.br/.gz) ÇAKIŞMA TEMİZLİĞİ")
    wwwroot = os.path.join(dist_dir, "wwwroot")
    for root, dirs, files in os.walk(wwwroot):
        for file in files:
            if file.endswith(".br") or file.endswith(".gz"):
                file_path = os.path.join(root, file)
                try:
                    os.remove(file_path)
                except Exception as e:
                    pass

    print_step("5. VERCEL CONFIGURATION (vercel.json)")
    vercel_config = {
        "outputDirectory": "dist/wwwroot",
        "cleanUrls": True,
        "headers": [
            {
                "source": "/_framework/(.*)",
                "headers": [
                    { "key": "Cache-Control", "value": "no-cache, no-store, must-revalidate" },
                    { "key": "Access-Control-Allow-Origin", "value": "*" }
                ]
            }
        ],
        "routes": [
            { "handle": "filesystem" },
            { "src": "/.*", "dest": "/index.html" }
        ]
    }
    with open(os.path.join(workspace, "vercel.json"), "w", encoding="utf-8") as f:
        json.dump(vercel_config, f, indent=2)
    print("vercel.json güncellendi.")

    print_step("6. GİT VERSİYONLAMA VE PUSH")
    version_tag = time.strftime("v1.0.%Y%m%d%H%M%S")
    run_cmd("git add .", cwd=workspace)
    run_cmd(f'git commit -m "Automated Deploy {version_tag}: Timestamp {now_str}"', cwd=workspace)
    if run_cmd("git push origin main", cwd=workspace):
        print_step(f"BAŞARILI! Derleme Saati: {now_str} (Versiyon: {version_tag}) GitHub'a Pushlandı!")
        print("Vercel birkaç saniye içinde bu versiyonu otomatik yayınlayacak.")
    else:
        print("Push hatası oluştu!")

if __name__ == "__main__":
    main()
