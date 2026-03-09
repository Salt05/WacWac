# Duck Race

Unity duck racing game focused on short, replayable races with simple setup and a clear leaderboard.

## Quick Summary
- Spectator-style duck race with configurable time, count, names, and skins
- Deterministic outcome with directed race phases (opening, midgame, sprint)
- Lightweight UI flow: Setup → Race → Leaderboard

## Docs
- Detailed design and technical overview: [docs/GameDesign.md](docs/GameDesign.md)

### Cơ chế hoạt động trong Phase 1 & Phase 2

Khi bắt đầu mỗi phase (hoặc khi hoàn thành chu kỳ trước):

1. **Random tần suất hành động** cho từng con vịt trong khoảng cấu hình của phase đó.
2. **Random hành động:** chọn 1 trong 3 (Tiến / Đứng yên / Lùi).
3. **Nếu hành động là Tiến hoặc Lùi:** random thêm **tốc độ** trong khoảng cấu hình của phase.
4. **Thực hiện hành động** trong suốt khoảng thời gian tần suất đã random, sử dụng **hàm Cubic** để chuyển từ tốc độ 0 đến tốc độ đích.
5. Khi hết khoảng thời gian → quay lại bước 2 (random hành động mới).

### Khoảng giá trị theo Phase (Public – có thể chỉnh Inspector)

| Thông số | Phase 1 (Ví dụ) | Phase 2 (Ví dụ) |
|----------|-----------------|------------------|
| **Tần suất hành động** | 4 – 6 giây | 4 – 6 giây |
| **Tốc độ tiến (Forward)** | 0.1 – 0.5 | 0.3 – 0.8 |
| **Tốc độ lùi (Backward)** | 0.1 – 0.3 | 0.1 – 0.5 |

> **Lưu ý:** Các giá trị trên là ví dụ mặc định, tất cả đều được đặt `public` để chỉnh trực tiếp trong Unity Inspector.

### Ví dụ minh họa

Giả sử Phase 1, con vịt số 2:
1. Random tần suất: **4.5 giây**.
2. Random hành động: **Lùi (Backward)**.
3. Random tốc độ lùi: **0.4**.
4. → Vịt sử dụng hàm Cubic từ tốc độ 0 → 0.4 trong 4.5 giây.
5. Sau 4.5 giây → random hành động mới, random tốc độ mới, lặp lại.

### Quy tắc chuyển Phase

#### Phase 1 → Phase 2:
- Nếu vịt **đang thực hiện dở** một chu kỳ hành động (ví dụ đang ở giây thứ 2/4.5) khi Phase 1 kết thúc:
  - Vịt **chờ hoàn thành** chu kỳ hiện tại (chạy hết 4.5 giây).
  - Sau đó mới cập nhật khoảng giá trị random sang Phase 2.

#### Phase 2 → Phase 3:
- Tất cả vịt **lập tức hủy** mọi hành động đang dở.
- Chuyển sang di chuyển tới điểm kết thúc (FinalProgress) của chính nó bằng hàm Cubic trong thời gian $T_3$.

### Data Structure

```csharp
[System.Serializable]
public class PhaseConfig
{
    public Vector2 ActionFrequencyRange;   // Min-Max tần suất random hành động (giây)
    public Vector2 ForwardSpeedRange;      // Min-Max tốc độ tiến
    public Vector2 BackwardSpeedRange;     // Min-Max tốc độ lùi
}

public class DuckRuntimeData
{
    public int DuckID;
    public float FinalProgress;
    public float CurrentProgress;
    public float CurrentActionTimer;       // Thời gian còn lại của hành động hiện tại
    public float CurrentActionDuration;    // Tổng thời gian chu kỳ hành động
    public DuckAction CurrentAction;       // Forward / Idle / Backward
    public float TargetSpeed;              // Tốc độ đích của hành động hiện tại
}

public enum DuckAction { Forward, Idle, Backward }
```

---

## 7.2. NGUYÊN TẮC BẮT BUỘC (MANDATORY CONSTRAINTS)

### Cubic Ease-In ($t^3$)

**Trong mọi trường hợp**, việc thay đổi các thuộc tính sau đều phải sử dụng thuật toán **Cubic Ease-In** để đảm bảo sự mượt mà tuyệt đối:

1. **Thay đổi vận tốc** – Khi vịt tăng/giảm tốc
2. **Chuyển trạng thái giữa các giai đoạn** – Khi chuyển từ Phase 1 → Phase 2 → Phase 3
3. **Di chuyển tới vị trí đích** – Khi vịt tiến về đích hoặc lùi lại

### Công thức Cubic Ease-In

$$f(t) = t^3$$

Trong đó:
- $t \in [0, 1]$ là tiến trình chuẩn hóa
- $f(t)$ là giá trị nội suy đã được làm mượt

### Áp dụng cho Lerp

```csharp
// Thay vì dùng Lerp thông thường:
// value = Mathf.Lerp(start, end, t);

// Sử dụng Cubic Ease-In:
float easedT = t * t * t; // Cubic Ease-In
float value = Mathf.Lerp(start, end, easedT);
```

### Áp dụng cho chuyển đổi trạng thái

```csharp
public float ApplyCubicEaseIn(float currentValue, float targetValue, float progress)
{
    float easedProgress = progress * progress * progress;
    return Mathf.Lerp(currentValue, targetValue, easedProgress);
}
```

### Lợi ích của Cubic Ease-In

| Đặc điểm | Mô tả |
|----------|-------|
| **Khởi đầu chậm** | Vật thể bắt đầu di chuyển rất chậm, tạo cảm giác tự nhiên |
| **Tăng tốc dần** | Vận tốc tăng theo lũy thừa bậc 3, tạo gia tốc mượt |
| **Không giật** | Không có sự thay đổi đột ngột về vận tốc |
| **Chuyên nghiệp** | Tạo cảm giác polish cho animation và gameplay |

---

### Vì sao không cần rubber-banding truyền thống
- Rubber-banding truyền thống:
  - Điều chỉnh tốc độ theo vị trí thực (leader bị giảm tốc, kẻ sau được buff).
  - Dễ gây cảm giác "gian lận" nếu người chơi có điều khiển.
- Ở đây:
  - Người chơi **không trực tiếp điều khiển**.
  - Đội hình đã được *đạo diễn* bằng progress curve & phase.
  - Không cần auto-correct dựa trên vị trí – mọi thứ đã được encode ngay trong `FinalProgress` và mapping thời gian.

---

## 8. FINAL SPRINT & FINISH SYSTEM

### Khái niệm Rendezvous Point (RP)
- **Rendezvous Point (RP):** một điểm logic mà tại đó:
  - Winner **và** vạch đích (finish line) sẽ "gặp nhau" đúng vào thời điểm T = RaceDuration.
- Thay vì để vịt tự chạy tới vạch đích dựa trên vật lý, ta:
  - Tính sẵn quãng đường còn lại + thời gian còn lại.
  - Đạo diễn sao cho winner và finish cùng kết thúc tại RP.

### Cách xác định RP tại T − 3s

Giả sử:
- Thời gian còn lại cho final sprint: $\Delta t = 3s$.
- Tại $t = T − 3s$, ta biết:
  - `CurrentProgressWinner`.
  - `FinalProgressWinner = 1.0`.

Bước thiết kế:
1. Chọn 1 giá trị `RPProgress` gần 1.0 (ví dụ 0.95–1.0) cho winner.
2. Tính quãng *progress* cần đi thêm: `ΔP = RPProgress − CurrentProgressWinner`.
3. Tương ứng trên track (nếu cần), `RPPoint = Lerp(StartPoint, EndPoint, RPProgress)`.

### Logic Final Sprint

1. **Thiết lập sprint**
   - Ở $t = T − 3s$:
     - Winner được gán một hàm tăng tiến nhanh hơn để đạt `RPProgress` đúng ở t = T.
     - Các vịt khác:
       - Nếu `FinalProgress` gần 1.0 → cũng cho sprint nhẹ nhưng vẫn thua.
       - Nếu `FinalProgress` thấp → tiến chậm, thể hiện rõ thua.

2. **Finish Line**
   - Với mô hình progress-based:
     - **Không bắt buộc phải di chuyển vạch đích vật lý** – có thể để cố định.
     - Chỉ cần đảm bảo rằng mapping `Progress → Position` cho `Progress = 1.0` trùng với vị trí finish line.
   - Nếu muốn finish line "trôi" vào camera:
     - Có thể move camera hoặc bản thân finish line dựa trên `CurrentProgress` của winner sao cho:
       - Ở t = T, cả winner ở `Progress = 1.0` và finish line đều nằm ở vị trí đẹp trong khung hình.

3. **Lợi ích so với collision-based finish**
- Không phụ thuộc vào collider, speed frame-by-frame.
- Không có hiện tượng "vịt vượt quá vạch đích nhưng chưa trigger collider".
- Bảo đảm **tính toán chuẩn xác**: ai thắng đã được encode trong `FinalProgress`, không lệ thuộc FPS.

---

## 9. STATE & DATA MODEL

### State chính của game

1. **Setup**
   - Người chơi cấu hình thông số, nhập tên.
2. **Ready**
   - Race Logic đã được chuẩn bị (có thể đã random winner & FinalProgress), chờ Start.
3. **Running**
   - `CurrentTime` tăng từ 0 → `RaceDuration`.
   - `CurrentProgress` của từng vịt được cập nhật mỗi frame.
4. **Finished**
   - `CurrentTime ≥ RaceDuration`.
   - `CurrentProgress` = `FinalProgress`.
   - Ranking lock-in, hiển thị leaderboard.

### Data Model cho Duck

Cho mỗi vịt (Duck):
- `DuckID` (int)
  - Định danh duy nhất, dùng cho tie-breaker.
- `LaneIndex` (int)
  - Lane logic; dùng để map sang đúng vị trí hiển thị.
- `Name` (string)
  - Tên hiển thị trong UI.
- `FinalProgress` (float)
  - Tiến độ cuối cùng, quyết định hạng.
- `CurrentProgress` (float)
  - Tiến độ hiện tại, dùng để render.
- (Tùy chọn) `SkinID` để phục vụ presentation.

### Data Model cho Race

- `RaceConfig`
  - `Duration` (float): tổng thời gian race.
  - `DuckCount` (int): số lượng vịt.
  - (Tùy chọn) seed random.

- `RaceRuntimeState`
  - `CurrentTime` (float): thời gian đã trôi qua.
  - `Phase` (enum): Opening / Midgame / Final.
  - `Ducks[]`: mảng dữ liệu Duck (như trên).
  - `WinnerDuckID` (int).
  - `Ranking[]`: danh sách DuckID đã sắp xếp cuối race.

---

## 10. RENDERING & PRESENTATION

### Render vịt dựa trên Progress

1. **Từ Progress → Position**
   - Mỗi frame:
     - Lấy `CurrentProgress` của duck.
     - Tính $Position = Track.Evaluate(CurrentProgress, LaneIndex)$.
   - `Track.Evaluate` có thể:
     - Trả về điểm trên đường thẳng.
     - Hoặc dùng spline cho đường cong.

2. **Animation**
   - Tốc độ animation chạy (
     - Ví dụ: blend speed chạy nhanh/chậm dựa trên derivative của Progress.
   - Các hiệu ứng khác (lắc đầu, vẫy nước, bobbing) chỉ liên quan presentation.

### UI cần có

1. **Countdown / Timer**
   - Hiển thị thời gian còn lại: mm:ss hoặc hh:mm:ss.

2. **Finish Line**
   - Thể hiện rõ điểm kết thúc đường đua.
   - Có thể thêm hiệu ứng khi winner chạm mốc.

3. **Leaderboard**
   - Sau khi race kết thúc:
     - Hiển thị danh sách vịt theo thứ hạng.
     - Mỗi dòng: hạng, tên, (tuỳ chọn) khoảng cách tới winner.

4. **Control Buttons**
   - Start / Pause / Resume / Clear / Back.
   - Tác động **chỉ lên state**, không can thiệp trực tiếp vào tọa độ.

---

## 11. ĐỊNH HƯỚNG REBUILD TỪ ĐẦU

### Thứ tự build lại hệ thống

1. **Progress Engine (core logic)**
   - Xây 1 module thuần logic (không UI) có thể:
     - Nhận `RaceConfig`.
     - Random winner + sinh `FinalProgress` cho N vịt.
     - Cho mỗi frame (t), trả về `CurrentProgress` cho từng vịt.

2. **Winner & Ranking Resolver**
   - Viết hàm:
     - `ResolveWinner(seed, duckList)`.
     - `BuildRanking(finalProgressList, tieBreaker = DuckID)`.
   - Đảm bảo deterministic với cùng seed & input.

3. **Phase Controller**
   - Định nghĩa các phase theo t/T.
   - Thiết kế các hàm/curve mô tả `TargetProgress(t, duck)`.
   - Áp biên $[P_{min}(t), P_{max}(t)]$ để điều khiển độ giãn đội hình.

4. **Finish / Rendezvous System**
   - Cụ thể hoá logic final sprint:
     - Khi còn 3s, tính phần còn lại để vừa khớp `FinalProgress`.
     - Định nghĩa một hoặc nhiều RP nếu cần.

5. **Rendering & UI**
   - Sau khi core logic ổn định:
     - Gắn nó vào scene engine (Unity/Unreal/…):
       - Mỗi duck prefab đọc `CurrentProgress` để tự cập nhật vị trí.
     - Thêm background, camera, animation, SFX.
     - Xây UI Setup, Race HUD, Leaderboard.

### Những phần **KHÔNG** nên mang từ code cũ
- Các chi tiết quá phức tạp liên quan đến:
  - Va chạm, trigger vật lý cho finish.
  - Rubber-banding theo vị trí world X hàm nhiều tầng.
  - LOD update quá chi tiết (Full/Simplified/Minimal) khi số lượng vịt còn ít.
  - Các workaround liên quan tới anchor UI vs world space.
- Thay vào đó, nên:
  - Giữ **mô hình progress-based** tinh gọn.
  - Để mọi thứ còn lại là ánh xạ từ progress sang presentation.

---

## 12. TÓM TẮT CHO DEV & DESIGNER

### Nguyên tắc KHÔNG được phá vỡ
- **1. Outcome-driven:**
  - Winner & ranking được xác định **ngay khi start**, không phụ thuộc vật lý runtime.
- **2. Logic dùng Progress, không dùng tọa độ:**
  - Mọi quyết định gameplay dựa trên `CurrentProgress` / `FinalProgress`, không dựa trực tiếp vào vị trí hiển thị.
- **3. Rendering là một chiều:**
  - UI, animation, camera **không** thay đổi core state, chỉ đọc và hiển thị.
- **4. Determinism:**
  - Cùng seed & input → cùng kết quả.

### Những chỗ có thể tinh chỉnh thêm
- Phân phối xác suất Winner:
  - Có thể gắn với tier hoặc hệ thống odds (nếu có betting).
- Khoảng giá trị tốc độ & tần suất hành động theo phase:
  - Điều chỉnh qua Inspector để tạo trải nghiệm khác nhau.
- Phase design:
  - Thời lượng từng phase, độ "giãn" đội hình.
- Presentation:
  - Skin, hiệu ứng nước, camera shake, slow-motion ở khoảnh khắc winner cán đích.

### Checklist khi implement

**Logic:**
- [ ] Đã có module chọn Winner & sinh FinalProgress deterministic.
- [ ] Đã có PhaseController mapping t/T → PhaseConfig (khoảng tốc độ & tần suất).
- [ ] Mỗi vịt random hành động (Tiến/Đứng/Lùi) + tốc độ theo tần suất của phase.
- [ ] Toàn bộ di chuyển sử dụng hàm Cubic.
- [ ] Chuyển Phase 1→2: chờ hết chu kỳ hành động hiện tại rồi mới cập nhật.
- [ ] Chuyển Phase 2→3: hủy ngay hành động, chuyển sang di chuyển tới FinalProgress.
- [ ] Ranking cuối dựa trên FinalProgress + tie-breaker DuckID.

**Data & State:**
- [ ] Mô hình Duck lưu DuckID, LaneIndex, Name, FinalProgress, CurrentProgress.
- [ ] Mô hình Race lưu Duration, CurrentTime, Phase, WinnerDuckID, Ranking.
- [ ] State máy: Setup → Ready → Running → Finished được quản lý rõ ràng.

**Rendering & UI:**
- [ ] Vịt được đặt theo công thức Position = Track.Evaluate(CurrentProgress, Lane).
- [ ] Countdown hiển thị đúng thời gian còn lại.
- [ ] Finish line di chuyển với tốc độ không đổi, mapping tới Progress = 1.0.
- [ ] Leaderboard hiển thị theo Ranking cuối.
- [ ] Button Clear phải reset vạch đích về vị trí ban đầu.
- [ ] Không hiển thị UI load scenes.

**QA & Tuning:**
- [ ] Với cùng seed, nhiều lần chạy cho kết quả giống nhau.
- [ ] Không có frame nào mà vịt "vượt finish nhưng chưa được tính win".
- [ ] Race nào cũng có cảm giác đủ drama (đầu bám – giữa giãn – cuối bứt). 
- [ ] Thêm/tắt hiệu ứng hình ảnh không làm thay đổi kết quả.
- [ ] Vạch đích di chuyển tốc độ không đổi (không dùng Cubic cho vạch đích).

## Setup Scene Specifications

- **Hiển thị & Phản hồi Nút bấm (Button Visuals & Feedback)**:Sử dụng hiệu ứng đổi màu (tô màu tối hơn) đơn giản cho trạng thái nút được nhấn và đang được chọn/active.

- **Keypad & Bảng thông tin (Quantity Mode)**: Keypad được bổ sung nút Clear để reset bảng đang active (GUI_Time hoặc GUI_Ducks) về giá trị 0/mặc định. Khi chuyển sang chế độ Set Names, keypad **không bị ẩn đi**; tuy nhiên, GUI_Ducks sẽ **ngừng nhận input từ keypad** và tự động đồng bộ (auto-sync) để hiển thị **tổng số vịt hiện đang có trong Name List**.

- **UI Quản lý Tên (Name Management UI – Name Mode)**: Khi nhấn Set Names, phần UI nhập & danh sách tên (Name UI: list + input) sẽ trượt mượt từ bên phải màn hình vào, dừng ở cạnh phải màn hình. Mỗi tên vịt bị giới hạn tối đa **10 ký tự**. Mỗi item trong danh sách có thể **click trực tiếp để chỉnh sửa**, và có nút "X" riêng để **xoá từng con vịt**. Bên dưới danh sách có nút **Clear All** để **xóa toàn bộ tên** hiện có.

- **Lưu & Tải dữ liệu (Data Persistence – Save/Load)**: Danh sách tên vịt được lưu cục bộ xuống một file **.txt**. Khi vào lại Setup Scene, hệ thống sẽ đọc file này để **khôi phục lại roster trước đó**, đảm bảo danh sách tên không bị mất giữa các lần chơi.

- **Chuyển cảnh với hiệu ứng cờ đua (Scene Transition – Flag Wipe Effect)**: Trong Setup Scene, khi nhấn **Start Race!**, một lá cờ caro (checkered flag) khổ lớn sẽ trượt mượt để **che kín toàn bộ màn hình**, di chuyển tới vị trí **GameObject_Target_A**. Khi cờ đã che kín màn hình, hệ thống gọi `SceneManager.LoadScene("RaceScene")`. Ở Race Scene, cảnh bắt đầu với lá cờ **đang ở vị trí che toàn bộ màn hình** (GameObject_Target_A). Sau khi **toàn bộ vịt spawn xong**, lá cờ sẽ trượt mượt ra khỏi màn hình, di chuyển tới **GameObject_Target_B**, từ từ **lộ ra đường đua** và bắt đầu **countdown**.
