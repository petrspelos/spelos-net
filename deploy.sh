#!/bin/bash
dotnet publish -c Release -r linux-x64 --self-contained false -o deploy src/SpelosNet.WebApp/SpelosNet.WebApp.csproj
ssh root@spelos.net 'systemctl stop kestrel-spelos'
scp -r deploy/* root@spelos.net:/var/www/spelos/
ssh root@spelos.net 'systemctl start kestrel-spelos'
