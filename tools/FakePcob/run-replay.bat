@echo off
title FakePcob replay - 16 team match
cd /d "%~dp0"
bin\Debug\net8.0-windows\FakePcob.exe serve --replay recordings\20260923-232832 --paused --port 10086
pause
