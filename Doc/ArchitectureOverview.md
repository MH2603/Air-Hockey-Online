# ARCHITECTURE DESIGN DOCUMENT: AIR HOCKEY MULTIPLAYER
**Project:** Real-time Air Hockey (1v1)
**Architecture Pattern:** Authoritative Server / Client-Server
**Tech Stack:** .NET 8 (Server), Unity (Client), LiteNetLib (Networking)

---

## 1. Tổng quan hệ thống (System Overview)

Hệ thống được thiết kế theo mô hình **Authoritative Server**. Server chịu trách nhiệm hoàn toàn về logic game và vật lý. Client đóng vai trò là "Dummy Terminal" - chỉ gửi input và hiển thị trạng thái nhận được từ Server.



### Nguyên tắc cốt lõi:
* **Networking:** Sử dụng UDP thông qua thư viện **LiteNetLib** để tối ưu hóa tốc độ.
* **Physics:** Không dùng Unity Physics trên Server. Sử dụng toán học Vector thuần túy trong Project Shared.
* **Sync:** Đồng bộ hóa trạng thái (State Synchronization) thay vì đồng bộ hóa sự kiện.

---

## 2. Cấu trúc Project (Project Structure)

Dự án được chia thành 3 phần chính để đảm bảo tính tái sử dụng code:

### 2.1. Shared Project (Class Library)
Chứa các logic mà cả Server và Client đều cần biết.
* **Packets:** Định nghĩa các class dữ liệu (ví dụ: `JoinPacket`, `PlayerInputPacket`, `GameStatePacket`).
* **Game Logic:** Các hằng số (Tốc độ bóng, bán kính Paddle, ma sát).
* **Math:** Xử lý va chạm hình tròn (Circle vs Circle) và hình chữ nhật (AABB).

### 2.2. Server Project (C# Console App)
Chạy trên môi trường .NET 8 độc lập.
* **Network Manager:** Quản lý Listener, chấp nhận kết nối, phân loại gói tin.
* **Room Engine:** Quản lý các phòng chơi (mỗi phòng 2 người).
* **Game Loop:** Chạy ở tần suất 60 FPS (Ticks) để tính toán vật lý.

### 2.3. Client Project (Unity)
* **Network Bridge:** Nhận dữ liệu từ Server và đẩy vào hàng đợi xử lý.
* **View Controller:** Cập nhật vị trí của Sprite dựa trên dữ liệu từ Server.
* **Interpolation:** Nội suy vị trí để xử lý hiện tượng giật (jitter) do độ trễ mạng.

---

## 3. Giao thức truyền thông (Communication)

### 3.1. Danh sách Gói tin (Data Packets)

| Packet Name | Logic | Mô tả dữ liệu |
| :--- | :--- | :--- |
| `PlayerInputPacket` | Client -> Server | Tọa độ đích của Paddle (X, Y) |
| `GameStatePacket` | Server -> Client | Vị trí (PuckX, PuckY, P1_X, P1_Y, P2_X, P2_Y) |
| `ScorePacket` | Server -> Client | Điểm số hiện tại của 2 người chơi |
| `GameEventPacket` | Server -> Client | Loại event: `Start`, `Goal`, `End` |

### 3.2. Chu kỳ xử lý (The Tick Loop)
Server chạy một vòng lặp cố định 60 Ticks/giây.
1.  **Read:** Đọc tất cả Input từ các Client.
2.  **Update:** Tính toán vị trí mới của Paddle và Puck.
3.  **Physics:** Kiểm tra va chạm và phản xạ.
4.  **Broadcast:** Gửi `GameStatePacket` cho tất cả Client trong Room.

---

## 4. Mô hình vật lý (Physics Model)

### 4.1. Va chạm Paddle và Puck (Circle vs Circle)
Sử dụng khoảng cách giữa hai tâm. Nếu khoảng cách nhỏ hơn tổng hai bán kính, va chạm xảy ra.
$$d = \sqrt{(x_2 - x_1)^2 + (y_2 - y_1)^2}$$
$$\text{Collision if } d < (R_{puck} + R_{paddle})$$

### 4.2. Phản xạ vận tốc
Khi Puck va chạm, vector vận tốc mới ($V_{new}$) sẽ phụ thuộc vào vector va chạm và vận tốc của Paddle tại thời điểm đó để tạo cảm giác "đẩy" bóng.

---

## 5. Lộ trình triển khai (Sprint 14 Days)

### Tuần 1: Cơ sở hạ tầng & Kết nối
* **Day 1-2:** Setup 3 projects, tích hợp LiteNetLib, làm Handshake (Connect/Disconnect).
* **Day 3-4:** Định nghĩa Packets, gửi Input từ Unity lên Console Server thành công.
* **Day 5-7:** Server xử lý di chuyển Paddle và Puck cơ bản, gửi State ngược lại Client.

### Tuần 2: Gameplay & Hoàn thiện
* **Day 8-9:** Viết logic va chạm (Tường & Paddle) trong Project Shared.
* **Day 10-11:** Xử lý ghi bàn (Goal), tính điểm và Reset Game State.
* **Day 12:** Cài đặt Interpolation trên Unity để làm mượt chuyển động.
* **Day 13-14:** UI/UX, âm thanh và đóng gói Build (Deploy thử nghiệm).

---

## 6. Ghi chú kỹ thuật (Technical Notes)
* **Serialization:** Sử dụng `NetSerializer` đi kèm LiteNetLib để tối ưu tốc độ đóng gói byte.
* **Time Step:** Sử dụng `System.Diagnostics.Stopwatch` trên Server để đảm bảo Tick Rate chính xác 16.67ms.
* **Coordinate System:** Thống nhất hệ tọa độ (ví dụ: Sân đấu rộng 20 đơn vị, cao 30 đơn vị) giữa Server và Client.