@echo off
REM この bat を 20 直下に置く前提

wt.exe new-tab -d "%~dp0." --title "Codex" cmd /k "codex --dangerously-bypass-approvals-and-sandbox"