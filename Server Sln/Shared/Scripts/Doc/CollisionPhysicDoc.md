# Air Hockey Physics & Collision Rules

Dưới đây là các quy tắc logic và công thức toán học để xử lý va chạm giữa Paddle và Puck trong game Air Hockey (C# / Unity).

## 1. Core Physics Formula
Khi va chạm xảy ra, vận tốc mới của Puck ($V_{puck\_new}$) được tính bằng sự kết hợp giữa vector phản xạ và lực truyền từ Paddle:

$$V_{puck\_new} = (V_{reflect} \times e) + (V_{paddle} \times f)$$

- **e (Elasticity):** Hệ số đàn hồi (nên để 0.7 - 0.9).
- **f (Influence):** Hệ số truyền lực từ Paddle (nên để 0.4 - 0.6).

## 2. Collision Logic Steps
Để Cursor AI viết code chính xác, hãy yêu cầu nó tuân thủ các bước:
1. **Calculate Normal Vector ($n$):** $$n = \text{normalize}(P_{puck} - P_{paddle})$$
2. **Relative Velocity ($V_{rel}$):** $$V_{rel} = V_{puck\_old} - V_{paddle}$$
3. **Reflection Vector:**
   Sử dụng hàm phản chiếu vật lý: `Vector2.Reflect(V_rel, n)`.

## 3. Implementation Constraints (Yêu cầu khi viết Code)
Khi yêu cầu Cursor generate code, hãy nhắc nó các ràng buộc sau:
- **Speed Clamping:** Giới hạn vận tốc tối đa ($V_{max}$) để tránh lỗi "tunnelling" (xuyên vật thể).
- **Minimum Speed:** Đảm bảo Puck không bao giờ dừng hẳn lại (duy trì một $V_{min}$ nhỏ).
- **Continuous Detection:** Sử dụng `CollisionDetectionMode2D.Continuous` cho Rigidbody của Puck.
- **Circular Paddle:** Tính toán va chạm dựa trên hình tròn (Circle Collider) để hướng nảy phụ thuộc vào điểm tiếp xúc trên đường cong của vợt.

## 4. Example Context for Cursor
"Hãy dựa vào file này để viết một class `PuckController.cs` xử lý va chạm trong `OnCollisionEnter2D`. Sử dụng `_maxSpeed` và `_bounciness` làm biến serialize."