import os
from flask import Flask, request, send_file

app = Flask(__name__)

file_path = "/home/jannik/Nextcloud/Schule/Forschen/Jugend Forscht/bin/Release/net10.0/linux-x64/publish/Jugend Forscht"
os.system("rm -rf /home/jannik/Nextcloud/Schule/Forschen/Jugend Forscht/bin/Release/net10.0/linux-x64/publish/*")
cmd = "dotnet publish -c Release -r linux-x64 --self-contained true /p:PublishSingleFile=true /p:IncludeNativeLibrariesForSelfExtract=true"
os.system(cmd)
@app.route('/download')
def return_file():
    if os.path.exists(file_path):
        return send_file(file_path, as_attachment=True)
    else:
        return "File not found", 404

if __name__ == '__main__':
    app.run(host='0.0.0.0', port=5648)