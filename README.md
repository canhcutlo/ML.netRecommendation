# Movies Recommendation

Hệ thống gợi ý phim sử dụng Machine Learning (ML.NET) kết hợp với dữ liệu phim thực từ TMDB API.

---

## Nhóm thực hiện

| Họ và tên | Vai trò | Phụ trách | Chi tiết công việc |
|---|---|---|---|
| Phạm Văn Minh | Integration Engineer | Thu thập & chuyển hóa dữ liệu | Xây dựng và chuẩn bị file `movie_ratings.csv` (UserId, MovieId, Rating); thiết kế class `MovieRating`, `MovieRatingPrediction`; định nghĩa pipeline tiền xử lý `MapValueToKey` |
| Lê Trung Hiếu | Backend Architect | Kiến trúc API & Swagger | Thiết kế Minimal API với ASP.NET Core 9; xây dựng endpoint `POST /api/predict` (dự đoán rating) và `GET /api/external` (proxy TMDB); tích hợp Dependency Injection cho `PredictionEngine` |
| Nguyễn Trung Thành | DevOps Engineer | DevOps & Live Demo | Cấu hình môi trường chạy, quản lý build/run dự án; trình diễn live demo gọi API qua Postman; xử lý lỗi kết nối và kiểm thử end-to-end |

---

## Công nghệ sử dụng

| Công nghệ | Phiên bản | Mục đích |
|---|---|---|
| .NET / ASP.NET Core | 9.0 | Nền tảng backend, Minimal API |
| ML.NET | 5.0.0 | Thư viện Machine Learning |
| ML.NET Recommender | 0.23.0 | Thuật toán Matrix Factorization |
| TMDB API | v3 | Lấy danh sách phim thực tế |

---

## Kiến trúc hệ thống

```
Client (Postman)
      │
      ▼
ASP.NET Core Minimal API (localhost)
      ├── POST /api/predict  ──► ML.NET Model (Matrix Factorization) ──► Dự đoán rating
      └── GET  /api/external ──► TMDB API (api.themoviedb.org)        ──► Danh sách phim
```

---

## Cách hoạt động

### 1. Huấn luyện Model (khởi động server)

Khi server khởi động, hệ thống tự động:
1. Đọc dữ liệu từ `movie_ratings.csv` (cột: `UserId`, `MovieId`, `Rating`)
2. Mã hóa `UserId` và `MovieId` thành dạng key số (`MapValueToKey`)
3. Huấn luyện model **Matrix Factorization** (50 vòng lặp, rank 50)
4. Đăng ký `PredictionEngine` vào DI container — sẵn sàng phục vụ request

### 2. API Endpoints

#### `POST /api/predict` — Dự đoán điểm phim
Nhận `UserId` và `MovieId`, trả về điểm rating dự đoán (thang 1–5).

**Request body:**
```json
{
  "userId": 1,
  "movieId": 10
}
```

**Response:**
```json
{
  "userId": 1,
  "movieId": 10,
  "predictedRating": 4.25
}
```

#### `GET /api/external` — Phim phổ biến từ TMDB
Gọi TMDB API lấy danh sách phim đang thịnh hành, trả về dạng JSON tiếng Việt (`language=vi-VN`).

---

## Cách chạy dự án

```bash
# Tại thư mục AiRecommender/
dotnet run
```

Server mặc định chạy tại `http://localhost:5000` (hoặc cổng được cấu hình).

---

## Cấu trúc dữ liệu

File `movie_ratings.csv`:
```
UserId,MovieId,Rating
1,10,4.5
1,20,3.0
2,10,5.0
...
```
