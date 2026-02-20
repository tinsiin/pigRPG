@echo off
REM この bat を 20 直下に置く前提

wt.exe new-tab -d "%~dp0." --title "Codex" cmd /k "codex --dangerously-bypass-approvals-and-sandbox" ; new-tab -d "%~dp0." --title "Claude" cmd /k "claude --dangerously-skip-permissions" ; new-tab -d "%~dp0." --title "MCP Server" cmd /k "C:\Users\teinshiiin\.local\bin\uvx.exe --from mcpforunityserver==9.0.3 mcp-for-unity --transport http --http-url http://localhost:8080"
