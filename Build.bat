@echo off
setlocal
cd /d "%~dp0"
set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
if exist "%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe" set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" (
 echo ERROR: .NET Framework 4.x C# compiler was not found.
 pause
 exit /b 1
)
if not exist "bin" mkdir bin
"%CSC%" /nologo /target:winexe /optimize+ /platform:anycpu /out:"bin\StayOnTop.exe" /r:System.Windows.Forms.dll /r:System.Drawing.dll "StayOnTop.cs"
if errorlevel 1 (
 echo BUILD FAILED
 pause
 exit /b 1
)
echo.
echo Built: %CD%\bin\StayOnTop.exe
pause
