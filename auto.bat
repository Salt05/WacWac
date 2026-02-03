@echo off

git add .

set datetime=%date% %time%
git commit -m "Auto-update: %datetime%"

git push origin main

echo.
pause