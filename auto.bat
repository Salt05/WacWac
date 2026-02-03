@echo off
:: Cố định đường dẫn chạy lệnh tại thư mục chứa file .bat này
cd /d "%~dp0"

echo --- DANG KIEM TRA THAY DOI ---
git status

echo.
echo --- DANG GOM DU LIEU VA COMMIT ---
git add .

:: Lấy ngày giờ hệ thống làm lời nhắn mặc định
set datetime=%date% %time%
git commit -m "Auto-update: %datetime%"

echo.
echo --- DANG DAY DU LIEU LEN GITHUB ---
:: Thêm lệnh pull để tránh lỗi xung đột (Conflict) trước khi push
git pull origin main --rebase
git push origin main

echo.
echo === HOAN THANH ===
pause