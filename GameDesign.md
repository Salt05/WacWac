# Duck Race – Game Design & Technical Overview

---

## 1. GIỚI THIỆU TỔNG QUAN (OVERVIEW)

### Loại game
- **Thể loại:** spectator / betting / casual.
- Người chơi **không trực tiếp điều khiển** nhân vật trong lúc đua, mà chủ yếu:
  - Cấu hình cuộc đua (thời lượng, số lượng vịt, tên từng con, skin).
  - (Tuỳ thiết kế sản phẩm) "đặt cược" trong đầu xem con nào sẽ thắng.
  - Xem cuộc đua diễn ra với nhiều yếu tố bất ngờ, kịch tính.

### Trải nghiệm cốt lõi
- **Trải nghiệm chính:** xem một cuộc đua ngắn gọn, dễ hiểu, đầy biến động:
  - Đầu race đội hình tương đối sát nhau, không có chênh lệch quá lớn.
  - Giữa race xuất hiện những pha bứt tốc/tụt lại tạo cảm giác "plot twist".
  - Cuối race xuất hiện **final sprint** – một hoặc vài con dồn lực bứt phá.
  - Kết thúc bằng leaderboard rõ ràng, dễ đọc.
- Người chơi có thể **replay nhiều lần** với các cấu hình khác nhau để xem các kịch bản mới.

### Mục tiêu thiết kế chính
- **Dễ xem, dễ hiểu:**
  - Người chơi chỉ cần nhìn là hiểu ngay con nào đang dẫn, còn bao lâu thì kết thúc.
- **Ngắn gọn, lặp lại được:**
  - Một race thường 10–60 giây, khuyến khích chạy lại nhiều lần.
- **Tính "đặt cược" nhẹ nhàng:**
  - Người chơi có thể tự chọn 1–2 con mà mình "thích" rồi xem chúng biểu diễn, không cần hệ thống bet phức tạp.
- **Dễ mở rộng:**
  - Thiết kế sẵn khả năng thêm skin, map, hiệu ứng camera, UI tỷ lệ cược… sau này.

### Cảm xúc mong muốn
- **Kịch tính:**
  - Không cho phép người chơi biết chắc 100% kết quả từ sớm; đội hình được điều khiển để luôn có cảm giác có thể lật kèo.
- **Bất ngờ:**
  - Thỉnh thoảng con đang dẫn bị tụt lại, con ở giữa bỗng tăng tốc, con ở cuối bất ngờ bứt phá.
- **Vui nhộn, nhẹ nhàng:**
  - Hình ảnh vịt, animation, background tạo cảm giác hài hước, relax.
- **Thoả mãn khi đoán đúng:**
  - Nếu người chơi chọn/bet đúng chú vịt thắng, cảm giác "mình tinh mắt" được củng cố.

---

## 2. TRIẾT LÝ THIẾT KẾ (DESIGN PHILOSOPHY)

### Outcome-driven – Race là màn trình diễn
- **Outcome-driven:**
  - Kết quả (thứ hạng từng con) được **quyết định ngay khi Start Race**.
  - Toàn bộ cuộc đua chỉ là quá trình **diễn hoạt (presentation)** để dẫn đội hình từ trạng thái xuất phát tới kết quả cuối cùng đó.
- Lợi ích:
  - Dễ bảo đảm *fairness* và tính toán xác suất (nếu có hệ thống betting).
  - Dễ kiểm soát nhịp kịch tính: biết trước ai thắng/thua → chủ động dàn dựng.
  - Tránh phát sinh bug khó lường từ vật lý/va chạm.

### Simulation-based race vs Directed race

**Simulation-based race (mô phỏng thuần):**
- Mỗi vịt có lực, vận tốc, gia tốc, va chạm, friction…
- Kết quả phụ thuộc vào rất nhiều bước mô phỏng nhỏ → khó dự đoán, khó debug.
- Muốn tạo kịch tính cụ thể (ví dụ 3s cuối luôn có sprint) rất khó, dễ bị lệch do vật lý.

**Directed race (cuộc đua được "đạo diễn")**
- Kết quả và đường cong tiến độ (progress) đã được quy hoạch.
- Hệ thống chỉ tìm cách **nội suy** từ thời gian → progress → vị trí.
- Có thể thiết kế các pha như trong phim:
  - Phase 1: bám sát nhau.
  - Phase 2: đội hình giãn ra.
  - Phase 3: final sprint.

### Vì sao chọn Directed Race
- **Kiểm soát trải nghiệm:**
  - Bảo đảm mỗi race đều có đủ các khoảnh khắc đáng nhớ, không có race nào "chán" (dẫn trước từ đầu đến cuối, không twist).
- **Đơn giản hoá kỹ thuật:**
  - Logic nằm ở dữ liệu progress, không phụ thuộc vào tọa độ, va chạm, physics.
  - Dễ port sang 2D/3D/đường đua cong/chéo mà không phải sửa core logic.
- **Ổn định & deterministic:**
  - Cùng một seed → cùng kết quả → dễ replay/debug.

---

## 3. TỔNG QUAN KIẾN TRÚC HỆ THỐNG (HIGH-LEVEL ARCHITECTURE)

### Các layer chính

1. **Input / UI Layer**
   - Màn Setup: nhập thời lượng đua, số lượng vịt, tên từng con.
   - Màn Race: nút Start/Pause/Clear/Back, hiển thị countdown, leaderboard.
   - Không chứa logic đua, chỉ gửi sự kiện và hiển thị trạng thái.

2. **Game Flow Controller**
   - Điều phối **state tổng** của game:
     - Setup → Ready → Running → Finished → (Replay).
   - Điều khiển chuyển scene (nếu có), khởi tạo RaceLogic, reset data.

3. **Race Logic (Progress-based Core)**
   - Trách nhiệm:
     - Tạo kết quả đua (winner + ranking) dựa trên random & rule.
     - Sinh ra `FinalProgress` cho từng vịt.
     - Thay đổi `CurrentProgress` theo thời gian & theo phase.
     - Cung cấp API dạng: `GetDuckProgress(duckId)`.
   - **Không quan tâm coordinate, không biết gì về camera, animation.**

4. **Rendering & Animation Layer**
   - Nhận `CurrentProgress` từ Race Logic và chuyển thành:
     - Vị trí trên đường đua (2D hoặc 3D), theo lane.
     - Animation chạy, rung, bobbing.
     - Hiệu ứng nước.
   - Một chiều: **chỉ đọc** từ core, không ghi ngược logic.

### Nguyên tắc phân tách

- **Logic không phụ thuộc tọa độ**
  - Mọi tính toán thứ hạng, tiến độ, phase đều hoạt động trên không gian 0–1 (Progress).
  - Đường đua thay đổi (thẳng, cong, chéo, camera xoay) không ảnh hưởng logic.

- **Rendering chỉ đọc Progress**
  - Công thức chung:
    - $Position = Lerp(StartPoint, EndPoint, CurrentProgress)$
  - Nếu muốn đường đua cong:
    - Dùng 1 spline/đường cong `TrackCurve(t)` rồi map `CurrentProgress` → `t` của spline.
  - Đảm bảo không có logic gameplay nào nằm trong animation (frame event, collision…).

---

## 4. GAME FLOW TỔNG THỂ (END-TO-END FLOW)

### Sơ đồ tổng quát (text)

```text
[Launch Game]
    ↓
[Setup Screen]
    ↓ (cấu hình thời gian, số vịt, tên, skin)
[Start Race]
    ↓ (khởi tạo Race Data + chọn Winner + FinalProgress)
[Race Phase 1: Opening]
    ↓ (đội hình bám sát)
[Race Phase 2: Midgame]
    ↓ (giãn đội hình, xuất hiện overtakes)
[Race Phase 3: Final 3s]
    ↓ (Final Sprint + tiến gần Finish)
[Finish]
    ↓ (khoá kết quả)
[Leaderboard + Replay / Back to Setup]
```

### Chi tiết từng bước

1. **Launch Game**
   - Khởi động vào màn **Setup**.
   - Tạo (hoặc nạp) cấu hình mặc định: thời gian đua, số vịt, skin, seed random.

2. **Setup Race**
   - Người chơi điều chỉnh:
     - `RaceDuration` (ví dụ 15–120 giây).
     - `DuckCount` (ví dụ 3–20).
     - Tên từng vịt (tùy chọn), hoặc để hệ thống tự đánh số.
   - Khi nhấn **Start**:
     - Lưu các thông số vào `RaceConfig` (data model).

3. **Start Race**
   - Game Flow Controller:
     - Sinh ra **RaceInstance** mới với seed random.
     - Gọi Race Logic để:
       - Chọn winner.
       - Random `FinalProgress` cho từng vịt (phần 6).
       - Sinh ra **curve Progress theo thời gian** cho mỗi vịt (phần 7).
     - Chuyển state → `Running`.

4. **Race Running – chia phase**
   - Thời gian đua `T` được chia thành các đoạn, ví dụ:
     - Phase 1 (Opening): 0 → 40% T.
     - Phase 2 (Midgame): 40% → 80% T.
     - Phase 3 (Final): 80% → 100% T.
   - Mỗi frame:
     - Cập nhật `CurrentTime`.
     - Từ `CurrentTime` + PhaseController → `CurrentProgressRangeAllowed` (progress min/max có thể hiển thị ở thời điểm đó).
     - Cập nhật `CurrentProgress` của từng vịt, đảm bảo **không vượt quá** `FinalProgress` và **tôn trọng phase**.

5. **Final Sprint (cuối race)**
   - Trong khoảng **3 giây cuối** (hoặc phần trăm T cố định):
     - Bật chế độ Final Sprint:
       - Winner được đẩy dần lên dẫn đầu nếu đang ở giữa.
       - Các vịt khác được điều chỉnh nhẹ để tạo cảm giác rượt đuổi.

6. **Finish & Ranking**
   - Khi `CurrentTime >= RaceDuration`:
     - Tất cả `CurrentProgress` được **snap** về đúng `FinalProgress`.
     - Tính **ranking** dựa trên `FinalProgress` (và tie-breaker).
     - Hiển thị leaderboard.

7. **Reset / Replay**
   - Người chơi có thể:
     - Bấm **Replay** với cùng cấu hình → chỉ random lại seed (hoặc giữ seed để replay y hệt).
     - Bấm **Back to Setup** để đổi thông số.

---

## 5. CORE RACE MECHANIC (CƠ CHẾ ĐUA CỐT LÕI)

### Khái niệm Progress
- Mỗi vịt có hai đại lượng:
  - `CurrentProgress` ∈ [0, 1]: tiến độ hiện tại của vịt trong race.
  - `FinalProgress` ∈ [0, 1+ε]: mốc tiến độ cuối cùng sau khi race kết thúc (dùng để xếp hạng, có thể >1 cho một số hiệu ứng nhưng thường 1 là "qua vạch đích").
- 0% → vị trí xuất phát, 100% → đúng vạch đích.

### Lane riêng cho mỗi vịt
- Mỗi vịt chạy trên 1 **lane độc lập**, không có va chạm:
  - Mỗi lane có `StartPoint` và `EndPoint` riêng trong không gian 2D/3D.
  - Lane chỉ ảnh hưởng **tọa độ hiển thị**, không ảnh hưởng logic.

### Logic spawn vịt (Diagonal Spacing)

Để đội hình xuất phát **đẹp, đều và không đè hình**, ta dùng một khung sinh (Spawning Frame) hình chữ nhật trên Canvas và phân bố vịt theo một đường chéo "/".

#### 1. Thiết lập không gian (Spawning Frame)

- Xác định một hình chữ nhật giả định với kích thước $(A, B)$ trên Canvas:
  - $A$: chiều ngang – độ dãn/độ rộng đội hình theo trục X.
  - $B$: chiều dọc – độ dốc/chiều cao đội hình theo trục Y.
- Toàn bộ vịt sẽ được đặt **bên trong** hình chữ nhật này tại thời điểm xuất phát.

#### 2. Thuật toán phân làn (Lane Partitioning)

Cho $n$ con vịt. Ta chia cạnh ngang và cạnh dọc của hình chữ nhật thành $n+1$ phần bằng nhau:

- Bước nhảy ngang:
  $$a = \frac{A}{n+1}$$
- Bước nhảy dọc:
  $$b = \frac{B}{n+1}$$

Ý tưởng: bỏ trống 1 khoảng ở đầu và 1 khoảng ở cuối, các vịt sẽ nằm lần lượt trên đường chéo từ gần góc trái–dưới tới gần góc phải–trên.

#### 3. Công thức toạ độ con vịt thứ $k$

Với $k$ chạy từ $1 \to n$, vị trí base (gốc) của con vịt thứ $k$ là:

$$x_k = k \cdot a$$
$$y_k = k \cdot b$$

Ở bước render, có thể offset hoặc transform thêm (scale, translate) để đưa $(x_k, y_k)$ về đúng hệ trục của Canvas/World tuỳ engine.

#### 4. Ý nghĩa kỹ thuật

- **Tính trật tự**:
  - Mỗi con vịt có một $y_k$ **duy nhất**, bảo đảm không đè hình dọc theo trục Y.
  - Trong suốt cuộc đua, vịt **chỉ thay đổi OffsetX** (dịch trái–phải) tương đối so với $x_k$, giữ nguyên lane chéo của mình.
  - Kết quả là đội hình luôn tạo thành một đường chéo "/" rõ ràng, không bị chồng sprite.
- **Tính linh hoạt**:
  - Chỉ cần thay đổi $A$ hoặc $B$, toàn bộ đội hình tự động co giãn/đổi độ dốc mà **không phải sửa logic di chuyển**.
  - Có thể dùng các preset khác nhau cho mobile/PC, tỉ lệ màn hình khác nhau.
- **Base Position (originPosition)**:
  - Cặp $(x_k, y_k)$ được lưu như **vị trí gốc** của vịt thứ $k$.
  - Mọi chuyển động sau này (tiến về đích, lùi lại, rung lắc) được tính **tương đối** quanh origin này, giúp code di chuyển đơn giản và ổn định hơn.

### Công thức xác định vị trí
- Ở thời điểm t, vị trí hiển thị của vịt được tính:
  - $Position(t) = Lerp(StartPoint, EndPoint, CurrentProgress(t))$
- `Lerp` có thể là:
  - Lerp tuyến tính trên đoạn thẳng.
  - Lerp theo tham số của 1 đường cong (spline) nếu track không thẳng.

### Vì sao hệ Progress giải quyết tốt các vấn đề

1. **Đường đua chéo / cong / camera xoay**
   - Logic **không cần biết** đường đua có dạng gì; chỉ quan tâm `CurrentProgress` ∈ [0,1].
   - Khi đổi art hay góc camera, chỉ cần đổi cách map `Progress → Position`.

2. **Đồng bộ thời gian**
   - Mọi vịt dùng chung `CurrentTime` và `RaceDuration`.
   - Bằng cách thiết kế các hàm `Progress(t)` phù hợp, đảm bảo race luôn kết thúc đúng thời gian.

3. **Sai lệch về đích (finish)**
   - Vì `FinalProgress` của winner luôn = 1 (hoặc lớn nhất), còn các con khác ≤ 1:
     - Không có chuyện "chưa kịp về đích" – simply snap mọi `CurrentProgress` về `FinalProgress` tại T=RaceDuration.
   - Không phụ thuộc vị trí thực; không bị lệch do lỗi vật lý.

---

## 6. WINNER & RANKING LOGIC

### Thời điểm chọn Winner
- **Ngay lúc Start Race**, hệ thống:
  1. Lấy danh sách tất cả vịt (DuckID 0..N-1).
  2. Random chọn 1 DuckID làm **Winner** dựa trên rule (có thể đồng đều hoặc weighted theo tier).
  3. Tạo `FinalProgress` cho tất cả các vịt.

### Cách random FinalProgress
- Quy ước:
  - Winner: `FinalProgressWinner = 1.0` (100%).
  - Các vịt khác: `FinalProgressOther` được chia thành **3 band xác suất** để tránh đứng chụm nhau khi số lượng vịt lớn.
- Thuật toán hiện tại:
  1. Chọn `Winner`.
  2. Đếm số vịt còn lại ($N$) và phân bổ chúng theo tỉ lệ **50% thấp / 30% trung bình / 20% cao** (dùng floor + chia phần dư để luôn đủ $N$ con).
  3. Với từng band, random giá trị **uniform** trong khoảng của band rồi trộn (shuffle) lại trước khi gán về từng vịt.
  4. Kết quả: luôn có đủ số vịt chậm (có thể âm), số vịt trung bình và số vịt bám đích, nhìn race dễ phân tầng hơn.

### Quy ước range
- Winner luôn đạt `1.0`.
- Non-winner bị clamp trong 3 band sau (đơn vị Progress, tương đương 0%–100%):
  - **Low:** `[-0.5, 0]`
  - **Mid:** `[0, 0.5]`
  - **High:** `[0.5, 0.8]`
- Band Low giúp tăng xác suất xuất hiện `P < 0`, tạo nhóm vịt tụt sâu rõ rệt.

### Xử lý trùng hạng
- Khi build ranking cuối:
  - Sort theo `FinalProgress` **giảm dần**.
  - Nếu `|FinalProgressA − FinalProgressB| < epsilon` (rất nhỏ):
    - Dùng tie-breaker là `DuckID` (hoặc seed phụ) để quyết định thứ hạng.

### Vì sao ranking luôn deterministic
- Với cùng:
  - Seed random ban đầu.
  - Danh sách DuckID ban đầu.
- Hệ thống sẽ:
  - Chọn Winner giống nhau.
  - Sinh cùng tập `FinalProgress`.
  - Sort với cùng rule → kết quả **hoàn toàn giống nhau**.
- Điều này giúp:
  - Dễ replay, debug.
  - Dễ đồng bộ nếu có backend quyết định kết quả, frontend chỉ render.

---

## 7. RACE PHASE & ĐIỀU KHIỂN ĐỘI HÌNH

### Phân chia Giai đoạn (Refined Phase Timing)

Thời gian cuộc đua $T$ được chia thành 3 giai đoạn dựa trên **mốc nước rút $T_3$** do người dùng thiết lập:

#### Công thức tính thời gian các giai đoạn

1. **$T_{12}$ (Tổng thời gian đầu):**
   $$T_{12} = T - T_3$$

2. **Giai đoạn 1 ($T_1$):**
   - Chiếm **cố định 40%** của $T_{12}$.
   $$T_1 = T_{12} \times 0.4$$

3. **Giai đoạn 2 ($T_2$):**
   - Chiếm **cố định 60%** của $T_{12}$.
   $$T_2 = T_{12} \times 0.6$$

4. **Giai đoạn 3 ($T_3$):**
   - Giai đoạn **nước rút (Sprint)** cố định cho đến khi kết thúc race.
   - Giá trị do người dùng thiết lập trước khi bắt đầu.
   $$T_3 = T - (T_1 + T_2)$$

#### Ví dụ kịch bản 20s

| Input | Giá trị |
|-------|--------|
| $T$ | 20s |
| $T_3$ | 3s |
| $DuckCount$ | 3 |

**Logic xử lý:**
- $T_{12} = 20 - 3 = 17s$
- $T_1 = 17 \times 0.4 = 6.8s$
- $T_2 = 17 \times 0.6 = 10.2s$
- $T_3 = 3s$ (cố định)

**Kết quả:** 3 con vịt sẽ được random tần suất hành động và tốc độ riêng cho từng phase.

### Mục tiêu từng giai đoạn

1. **Phase 1 – Opening (0 → $T_1$)**
   - Mục tiêu: giữ đội hình **tương đối sát nhau**.
   - Mỗi con vịt được random **tần suất thay đổi hành động** và **khoảng tốc độ** riêng cho phase này.
   - Khoảng giá trị tốc độ **thấp hơn** Phase 2 (để đội hình chưa giãn nhiều).

2. **Phase 2 – Midgame ($T_1$ → $T_1 + T_2$)**
   - Mục tiêu: cho thấy **khả năng phân hoá** nhưng vẫn còn cơ hội lật kèo.
   - Tương tự Phase 1, nhưng các khoảng giá trị tốc độ **cao hơn**, tạo sự giãn đội hình rõ rệt hơn.
   - **Quy tắc chuyển phase:** nếu vịt đang thực hiện dở một chu kỳ hành động khi Phase 1 kết thúc, nó sẽ chờ hoàn thành chu kỳ đó rồi mới cập nhật khoảng giá trị của Phase 2.

3. **Phase 3 – Final Sprint ($T_1 + T_2$ → $T$)**
   - Mục tiêu: **final sprint** – nước rút quyết định.
   - **Quy tắc chuyển phase:** tất cả vịt **lập tức hủy** mọi hành động đang dở và chuyển sang di chuyển tới điểm kết thúc (FinalProgress) của chính nó.
   - Mỗi vịt sử dụng hàm Cubic để di chuyển từ vị trí hiện tại tới FinalProgress trong khoảng thời gian $T_3$.

---

## 7.1. HỆ THỐNG HÀNH ĐỘNG ĐƠN GIẢN (SIMPLIFIED ACTION SYSTEM)

Không sử dụng hệ thống thể lực (Stamina) hay cá tính (Personality). Mỗi con vịt chỉ có **3 thuộc tính chủ đạo** đơn giản:

### Thuộc tính chính của mỗi vịt

| Thuộc tính | Kiểu | Mô tả |
|------------|------|-------|
| **Thứ hạng (FinalProgress)** | float | Giữ nguyên như cũ – quyết định vị trí cuối cùng |
| **Tần suất random hành động** | float (public) | Khoảng thời gian (giây) giữa các lần random hành động mới (Lùi / Đứng yên / Tiến) |
| **Tốc độ random** | float (public) | Tốc độ di chuyển được random trong khoảng cho phép của phase hiện tại |

### 3 Hành động (Actions)

| Hành động | Mô tả |
|-----------|-------|
| **Lùi** | Di chuyển lùi một đoạn nhỏ trong thời gian ngắn |
| **Đứng yên** | Không di chuyển trong một khoảng thời gian |
| **Tiến** | Di chuyển tiến lên với tốc độ random |
