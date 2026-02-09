# Hướng dẫn sử dụng `GameRequest` (Request)

File `GameRequest` là một `ScriptableObject` dùng để lưu nội dung và metadata của game (tiêu đề, mô tả, danh sách level, skin vịt, prefabs...).

Các trường chính:
- `gameTitle`: Tên game.
- `version`: Phiên bản.
- `description`: Mô tả ngắn (có thể nhiều dòng).
- `levels`: Danh sách `LevelInfo` (tên level, đường dẫn scene, độ khó).
- `duckSkins`: Danh sách `Sprite` cho skin vịt.
- `prefabs`: Danh sách `GameObject` prefab (ví dụ `DuckPrefab`, `Finish`).

Tạo asset `GameRequest`:
- Dùng menu `WacWac -> Create GameRequest Asset` (sau khi import script `CreateGameRequestAsset.cs`).
- Asset sẽ được tạo dưới `Assets/Resources/GameRequest.asset` và tự select trong Project window.

Gợi ý sử dụng:
- Tải asset bằng `Resources.Load<GameRequest>("GameRequest")` ở runtime nếu cần truy cập từ code không-Editor.
- Trong Editor, chỉnh trực tiếp các trường trong Inspector để trỏ tới scene / prefab / sprite có sẵn.

Lưu ý:
- Script tạo asset cố gắng tìm các prefab/sprite trong các thư mục chuẩn (`Assets/Prefabs`, `Assets/Resource/DuckSkin`) nhưng nếu project của bạn lưu tài nguyên ở chỗ khác, hãy mở asset trong Inspector và kéo thả các tài nguyên mong muốn.
