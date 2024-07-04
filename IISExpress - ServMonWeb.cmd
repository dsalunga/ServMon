@echo off

set AppPath=%cd%
echo %AppPath%

REM if exist "%ProgramFiles(x86)%" (
	REM "_IISExpress - Portal x86.lnk"
	REM CD /D "%ProgramFiles(x86)%\IIS Express"
REM ) else (
	REM "_IISExpress - Portal.lnk"
	REM CD /D "%ProgramFiles%\IIS Express"
REM )

REM CD /D "%ProgramFiles%\IIS Express"

CD ServMonWebV4

REM iisexpress.exe /config:%AppPath%\applicationhost.config /siteid:1
"C:\Program Files\IIS Express\iisexpress.exe"  /config:"C:\Workspace\github.com\ServMon\.vs\ServMon\config\applicationhost.config" /site:"ServMonWebV4" /apppool:"ServMonWebV4 AppPool"