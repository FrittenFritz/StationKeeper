@echo off
setlocal
set CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
if not exist "%CSC%" set CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe
if not exist "%CSC%" (echo .NET Framework 4.x csc.exe not found & exit /b 1)
cd /d "%~dp0"
if not exist bin mkdir bin
"%CSC%" /nologo /optimize+ /target:winexe /win32icon:assets\StationKeeper.ico /out:bin\StationKeeper.exe /r:System.Windows.Forms.dll /r:System.Drawing.dll src\StationKeeper.cs
if errorlevel 1 (echo Build FAILED & exit /b 1)
echo Build OK: bin\StationKeeper.exe