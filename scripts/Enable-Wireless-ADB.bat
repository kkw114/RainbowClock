@echo off
chcp 936 >nul
setlocal EnableExtensions EnableDelayedExpansion
title 为VR头显启用无线ADB

rem ============================================================
rem  一键启用USB头显（Quest/Pico）的无线ADB调试
rem  原理：
rem   1. 从 adb devices 中挑选USB连接的设备
rem   2. 读取头显的Wi-Fi IP（wlan0）
rem   3. adb tcpip 5555  -让adbd监听TCP 5555端口（开启无线调试）
rem   4. adb connect IP:5555  -建立无线ADB会话
rem   5. adb get-state 验证
rem ============================================================

rem ---- 查找 adb.exe（优先PATH，然后是常见安装目录）----
set "ADB="
adb version >nul 2>&1 && set "ADB=adb"
if not defined ADB if exist "C:\Windows\system32\adb.exe" set "ADB=C:\Windows\system32\adb.exe"
if not defined ADB if exist "%LOCALAPPDATA%\Android\Sdk\platform-tools\adb.exe" set "ADB=%LOCALAPPDATA%\Android\Sdk\platform-tools\adb.exe"
if not defined ADB (
  echo [错误] 未找到 adb.exe。
  echo        请安装 Android platform-tools 并加入 PATH，
  echo        或修改本脚本顶部的 ADB= 路径。
  pause
  exit /b 1
)
"%ADB%" version >nul 2>&1
if errorlevel 1 (
  echo [错误] 在以下位置未找到 adb.exe：%ADB%
  pause
  exit /b 1
)

set "PORT=5555"
set "SERIAL="
set "IP="

echo.
echo [1/5] 正在查找通过USB连接的头显...
"%ADB%" devices > "%TEMP%\bs_devices.txt" 2>nul
rem 普通 adb devices 输出中序列号与状态之间是TAB分隔，
rem 因此只匹配状态子串 device（unauthorized/offline 行不含它）
findstr "device" "%TEMP%\bs_devices.txt" > "%TEMP%\bs_ok.txt" 2>nul
rem 只挑选USB序列号（形如 192.168.x.x:5555 的无线条目含冒号，跳过）
rem skip=1 跳过 “List of devices attached” 表头
for /f "usebackq skip=1 tokens=1" %%A in ("%TEMP%\bs_ok.txt") do (
  echo %%A|findstr ":" >nul
  if errorlevel 1 if not defined SERIAL set "SERIAL=%%A"
)
if not defined SERIAL (
  echo [错误] 未找到处于 device 状态的USB连接设备。
  echo        请检查：
  echo         - 用USB数据线连接头显（不要用只充电的线/口）
  echo         - 戴上头显，并在弹出的“允许USB调试”对话框中点“允许”
  echo         - 若仍未识别，先执行  adb kill-server  再重新插拔USB线
  pause
  exit /b 1
)
echo         USB设备      : %SERIAL%

echo [2/5] 正在读取头显的Wi-Fi IP...
"%ADB%" -s %SERIAL% shell ip addr show wlan0 > "%TEMP%\bs_ip.txt" 2>nul
for /f "tokens=2" %%A in ('findstr "inet " "%TEMP%\bs_ip.txt"') do if not defined IP set "IP=%%A"
if not defined IP (
  rem 备用方案：从路由表查询源地址
  "%ADB%" -s %SERIAL% shell ip route get 8.8.8.8 > "%TEMP%\bs_ip2.txt" 2>nul
  for /f "tokens=7" %%A in ('findstr " src " "%TEMP%\bs_ip2.txt"') do if not defined IP set "IP=%%A"
)
if defined IP (
  for /f "tokens=1 delims=/" %%A in ("!IP!") do set "IP=%%A"
  set "IP=!IP:addr:=!"
)
if not defined IP goto manual_ip
echo         Wi-Fi IP     : %IP%
goto after_ip

:manual_ip
echo [错误] 无法自动获取头显的Wi-Fi IP。
set /p "IP=请手动输入头显的Wi-Fi IP（例如 192.168.1.50）："
if not defined IP (
  echo [错误] 未输入IP，操作已中止。
  pause
  exit /b 1
)

:after_ip
echo [3/5] 正在将adbd切换为TCP模式（端口%PORT%）...
"%ADB%" -s %SERIAL% tcpip %PORT%
if errorlevel 1 (
  echo [错误] “adb tcpip” 执行失败。
  pause
  exit /b 1
)
ping -n 3 127.0.0.1 >nul

echo [4/5] 正在通过Wi-Fi连接：%IP%:%PORT%
"%ADB%" connect %IP%:%PORT%

echo [5/5] 正在验证无线连接...
set /a tries=0
:verify
set /a tries+=1
"%ADB%" -s %IP%:%PORT% get-state 2>nul | findstr /i "device" >nul
if not errorlevel 1 goto wireless_ok
if %tries% GEQ 10 (
  echo [错误] 10次尝试后仍未确认无线连接。
  "%ADB%" devices
  pause
  exit /b 1
)
ping -n 2 127.0.0.1 >nul
goto verify

:wireless_ok
echo.
echo ================================================================
echo   无线ADB已就绪  -  %IP%:%PORT%
echo   现在可以拔掉USB线了。
echo ================================================================
"%ADB%" devices
echo.
echo   关闭无线ADB的方法：  "%ADB%" disconnect %IP%:%PORT%
echo.
pause
endlocal
exit /b 0
