powershell -ex AllSigned -c "Invoke-RestMethod 'https://aka.ms/install-azd.ps1' -OutFile 'install-azd.ps1'
./install-azd.ps1 -Version 'daily'"
del ./install-azd.ps1