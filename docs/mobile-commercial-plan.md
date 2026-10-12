# Kế hoạch thương mại hóa LCMS Mobile (iOS & Android)

Ngày: 2026-10-07  
Phạm vi: đóng gói `apps/mobile` thành sản phẩm đưa lên App Store và Google Play, bám API đang chạy của LCMS.  
Sản phẩm: kiểm soát tài chính quanh Bill (chi phí, doanh thu, chứng từ, AP/AR, thanh toán, đối soát, chốt kỳ). Không phải TMS. Không bán hàng số trong app.

## Hiện trạng đã có trong repo

- Backend: ASP.NET Core modular monolith — `src/LCMS.Api`, `src/LCMS.Application`, `src/LCMS.Domain`, `src/LCMS.Infrastructure`, PostgreSQL (`LcmsDbContext`).
- Web: Next.js App Router `apps/web` (khoảng 95 `page.tsx`), BFF cookie dưới `apps/web/app/bff/**`, vỏ desktop `ShellChrome`.
- Mobile đã scaffold: Expo SDK 57, React Native 0.86, Expo Router, gói `lss.lcms.mobile`, scheme `lcms`.
- Xác thực API: JWT Bearer HMAC (`JwtTokenIssuer`) + refresh opaque xoay vòng (`AuthTokenService`, bảng `refresh_tokens`). ADR-0002.
- Bootstrap mobile: `GET /api/mobile/bootstrap` → `GetMobileBootstrapQueryHandler`.
- Push device: bảng `user_push_devices`, `POST/DELETE /api/notifications/devices`.
- Offline: SQLite `lcms_mobile_offline.db`, bảng `offline_outbox` trong `apps/mobile/src/offline/outbox.ts`.
- Sinh trắc học bước 2: `verifyBiometricForMoneyAction` trong `apps/mobile/src/security/biometrics.ts`.

Phần còn lại của kế hoạch là đưa các mảnh này lên chuẩn duyệt store và vận hành tiền an toàn. Không viết lại app bằng WebView.

---

## Cấu phần 1 — Đánh giá codebase và chọn phương án

### Phương án chọn

**App độc lập React Native (Expo) gọi thẳng `LCMS.Api` qua HTTPS + Bearer JWT.**

Tiếp tục `apps/mobile`. Không bọc `apps/web` trong WebView Flutter/React Native.

Lý do bám code:

- Web đăng nhập qua BFF (`apps/web/app/bff/auth/logout/route.ts` và các route cookie). Mobile đã gọi `POST /api/auth/login` và lưu token trong `expo-secure-store` (`saveSessionTokens`). WebView sẽ kéo session cookie, sidebar `ShellChrome`, và mất Face ID, camera, SQLite, push.
- Server đã tách hợp đồng mobile: `MobileAndAttachmentEndpoints.MapMobileAndAttachmentEndpoints`, DTO `MobileBootstrapDto`, tab theo persona `BuildRoleAdaptiveBottomTabs`.
- Quyền xem tiền tách khỏi tên vai trò: `MobileFinancialVisibilityDto` (`CanViewCost`, `CanViewRevenue`, `CanViewMargin`). `CanViewMargin` chỉ đúng khi có cả `PermissionCodes.CostRead` và `PermissionCodes.RevenueRead`. WebView không ép được luật này trên từng thẻ.
- Native đã cài: `expo-camera`, `expo-local-authentication`, `expo-secure-store`, `expo-sqlite`, `expo-notifications`, `expo-file-system`.

### API tái sử dụng ngay

Mobile gọi các nhóm này. Không tạo route “mobile-only” song song nếu verb và DTO đã đủ.

| Nhóm | Prefix | File endpoint | Việc mobile dùng |
| --- | --- | --- | --- |
| Auth | `/api/auth/login`, `/refresh`, `/logout` | `AuthEndpoints.cs` | Đăng nhập, tự đăng nhập, thu hồi |
| Bootstrap | `GET /api/mobile/bootstrap` | `MobileAndAttachmentEndpoints.cs` | Persona, tab, module, badge, thuật ngữ |
| Thuật ngữ | `GET /api/terminology` | `TerminologyEndpoints.cs` | Nhãn tiếng Việt khi bootstrap 404 |
| Bill / vận hành | `/api/bills`, `/api/orders`, `/api/shipments`, `/api/transport-legs`, `/api/transport-movements`, `/api/search` | `TenantBillEndpoints.cs`, `OperationalReferenceEndpoints.cs` | Danh sách, chi tiết, quét mã |
| Chi phí | `/api/costs`, `/api/cost-allocations` | `CostEndpoints.cs` | Expected / Confirmed / Actual, phân bổ |
| Doanh thu | `/api/revenues`, `/api/profitability/bills` | `RevenueEndpoints.cs` | DT và lãi gộp (chỉ persona được xem margin) |
| Chứng từ | `/api/financial-documents`, `/api/document-matches` | `FinancialDocumentEndpoints.cs` | Tiếp nhận, khớp N:N |
| Đính kèm | `/api/attachments` | `MobileAndAttachmentEndpoints.cs` | Ảnh/PDF hiện trường (form hoặc base64) |
| AP / AR | `/api/payable-exposures`, `/api/receivable-exposures`, `/api/accounts-payable`, `/api/accounts-receivable`, `/api/ap-ar` | `ExposureApArEndpoints.cs` | Exposure, sổ, xóa nợ, hoàn tác |
| Thanh toán | `/api/payments`, `/api/payment-allocations`, `/api/collections`, `/api/collection-allocations` | `SettlementEndpoints.cs` | Phiếu chi, phiếu thu, gạch nợ |
| Sao kê | `/api/bank-feed/lines` | `BankFeedEndpoints.cs` | Dòng sao kê (đọc + bỏ qua) |
| Kiểm soát | `/api/approvals`, `/api/exceptions`, `/api/variances`, `/api/reconciliations` | `FinancialControlEndpoints.cs` | Duyệt, ngoại lệ, chênh lệch |
| Chốt kỳ | `/api/financial-closes`, `/api/financial-close-snapshots` | `FinancialCloseEndpoints.cs` | Eligibility, snapshot, reopen |
| Báo cáo | `/api/dashboard`, `/api/aging`, `/api/queues`, `/api/reports` | `DashboardReportingEndpoints.cs` | KPI, tuổi nợ |
| Giá | `/api/rate-cards`, `/api/ratings`, `/api/fx-rates` | `RatePricingEndpoints.cs`, `MasterDataEndpoints.cs` | Tra giá, so sánh, tỷ giá |
| Danh mục | `/api/business-parties`, `/api/locations`, `/api/routes`, `/api/commodities` | `MasterDataEndpoints.cs`, `ReferenceMasterEndpoints.cs` | Tra cứu đối tác, điểm, tuyến |
| Thông báo | `/api/notifications`, `/api/notifications/devices`, inbox read | `TenantAdminEndpoints.cs`, `MobileAndAttachmentEndpoints.cs` | Hộp thư + đăng ký máy |
| Audit (chỉ Admin) | `/api/audit-events`, `/api/outbox` | `AuditIntegrationEndpoints.cs` | Màn `modules/settings.tsx` |

Header bắt buộc mọi mutation tiền: `Authorization`, `Idempotency-Key`, `If-Match` (row version). Client đã có trong `apiRequest` (`apps/mobile/src/api/client.ts`). Server đọc ở `CreatePaymentCommand`, `AllocatePaymentCommand`, `CostEndpoints`, `RevenueEndpoints`, `FinancialCloseEndpoints`.

### Backend / database cần sửa

Không thêm database thứ hai. Sửa trên schema hiện có.

1. **Thu hồi refresh khi logout.** `AuthContext.logout` gọi `POST /api/auth/logout` không body. `AuthEndpoints` nhận `RefreshRequest.RefreshToken`. `RevokeAsync` trả về sớm khi token rỗng. Gửi `refreshToken` đang lưu trong SecureStore. Giữ `AuthTokenService.RevokeAllForUserAsync` cho khóa tài khoản.
2. **Khóa login theo thuê bao.** `POST /api/auth/login` dùng `IgnoreQueryFilters()` và lấy user email khớp mật khẩu đầu tiên. Mobile gửi `tenantCodeOrId` nhưng `loginWithCredentials` không đưa vào body. Thêm resolve `Tenant.Code` → `TenantId` rồi chỉ verify user trong tenant đó.
3. **Bắt buộc phân trang cho client mobile.** `ListBillsQuery`, `ListRevenuesQuery`, `ListBusinessPartyDirectoryQuery`, `ListRateCardsQuery` trả full list khi thiếu `page`/`pageSize`. Mobile luôn gửi `page` + `pageSize=20`. Thêm DTO thẻ gọn dưới `GET /api/mobile/feed/{resource}` (id, mã, đối tác, số tiền, tiền tệ, trạng thái, `rowVersion`) cho Bill, Cost, Revenue, AP, AR, Document. Không trả ma trận giá `TariffMatrix` hay lưới cargo trên list.
4. **Upload chứng từ bằng multipart.** `attachments.MapPost("/")` đã nhận `form` (`objectType`, `objectId`, file). `uploadAttachmentBase64` đang gửi JSON base64 (~+33% băng thông). Scanner chuyển sang `FormData`.
5. **Worker gửi push.** `NotificationPublisher.PublishAsync` chỉ ghi `OutboxMessage.Topic = "notification.push"` kèm `UserPushDevice.DeviceToken`. Chưa có consumer gọi Expo Push API hoặc FCM HTTP v1. Thêm handler outbox, không gọi FCM trong request HTTP.
6. **Xóa tài khoản người vận hành.** Chưa có route. `User` kế thừa `EntityBase.SoftDelete`, có `IsActive`. Thêm `POST /api/auth/account-deletion` (xem cấu phần 5). Không xóa Bill, sổ, snapshot.
7. **Tắt bootstrap quyền tối đa khi user chưa có role trên production.** `GetMobileBootstrapQueryHandler` nếu `rawPermissions` rỗng và `HasPermissionAsync(BillRead)` thì gán `PermissionCodes.CoreActionCodes` + `DataScopes.All`. Chỉ giữ cho Dev. Production trả 403.
8. **Ẩn `/api/dev` và `AllowHeaderBootstrap` trên production.** `AuthServiceCollectionExtensions`: production mặc định `RequireJwt=true`, `AllowHeaderBootstrap=false`. Xác nhận env host không bật header bootstrap.

Không đụng snapshot đã đóng. Không suy phạm vi dữ liệu từ tên role.

---

## Cấu phần 2 — Kết nối và bảo mật thương mại

### Auth hiện tại áp sang mobile

Luồng đã có:

1. `POST /api/auth/login` → `AuthTokenService.IssuePairAsync`.
2. Access JWT: claim `sub` (user id), `tenant_id`, `jti`. Ký HMAC-SHA256. Hết hạn `Auth:Jwt:ExpiryMinutes` (mặc định 60, kẹp 1–1440). Issuer/audience `lcms-api`.
3. Refresh: 48 byte random, DB chỉ lưu SHA-256 hex (`RefreshToken.TokenHash`). Hạn `RefreshExpiryDays` (1–90). `RefreshAsync` thu hồi bản cũ, phát cặp mới, ghi `ReplacedByTokenHash`.
4. `TenantResolutionMiddleware` lấy tenant và actor từ JWT. Header `X-Tenant-Id` chỉ là fallback Dev (`AuthOptions.AllowHeaderBootstrap`).
5. Mọi API production nằm sau `FallbackPolicy` yêu cầu user đã xác thực.
6. `RateLimitingMiddleware` tách bucket `money` và `api`.

Auto-login thương mại:

- Cold start: `AuthProvider` đọc SecureStore (`lcms.auth.access_token`, `lcms.auth.refresh_token`, `lcms.auth.tenant_id`). Có session thì `GET /api/mobile/bootstrap`. 401 thì `tryRefreshAccessToken` gọi `/api/auth/refresh` (một promise, không refresh song song). Refresh hỏng thì `clearSessionTokens` và về `app/login.tsx`.
- Bổ sung khóa phiên: sau khi có token, lần mở app và mỗi lần app từ background > 5 phút gọi `LocalAuthentication.authenticateAsync` trước khi vào tab. Đây là khóa máy, không thay JWT.
- Access ngắn, refresh xoay vòng. Không gia hạn access bằng cách nhét JWT vào AsyncStorage.
- Logout: gửi refresh hiện tại, `DELETE /api/notifications/devices?deviceToken=`, rồi xóa SecureStore. Server `RevokeAsync`.
- Đổi mật khẩu / khóa user (`PUT /api/users/{id}`, `POST /api/users/{id}/password`) phải gọi `RevokeAllForUserAsync` (đã có trên `IRefreshTokenRevoker`).

Cookie web và JWT mobile song song. Không share cookie BFF sang app.

### Ba lớp bảo vệ máy

**Lưu token.** `expo-secure-store` map Keychain (iOS, `kSecAttrAccessibleWhenUnlockedThisDeviceOnly`) và EncryptedSharedPreferences/Keystore (Android). Key: `STORAGE_KEYS` trong `client.ts`. Cấm fallback `memoryStore` trên bản store (hiện dùng khi SecureStore lỗi — bản release phải fail đóng và bắt đăng nhập lại). Không log token. Không đưa refresh vào crash report.

**SSL pinning.** Pin SPKI (public key) của chứng chỉ `cms.lsslogistics.vn`, kèm một pin dự phòng. Làm ở native (config plugin Expo), không pin trong JavaScript. Xoay chứng chỉ: cài pin mới + pin cũ chồng 30 ngày, phát hành app trước ngày hết hạn cert. Phương án vỡ: bản vá EAS khẩn, không tắt pin bằng cờ remote không ký. URL mặc định đã là `https://cms.lsslogistics.vn` (`getApiBaseUrl`). Bản production tắt ô sửa API URL (`EXPO_PUBLIC_SHOW_DEMO_PERSONAS=false` đã có trong `eas.json` profile `production` — siết thêm: không cho `setApiBaseUrl` trên build store).

**Làm rối mã.** Hermes (RN 0.86) biên dịch JS thành bytecode. Android release: AAB + R8 (`enableProguardInReleaseBuilds`, shrink resources) trong profile `production` của `eas.json`. iOS release: strip symbol. Không nhét `Auth__Jwt__SigningKey`, mật khẩu demo, hay SMTP vào binary. `EXPO_PUBLIC_*` chỉ được URL công khai và cờ build.

Giới hạn thành thật: pinning và R8 nâng chi phí dịch ngược. Chúng không thay quyền server, `If-Match`, idempotency, và tách cost/revenue.

Sinh trắc học tiền giữ `verifyBiometricForMoneyAction` trước: duyệt/từ chối, actualize/adjust cost hoặc revenue, phân bổ phiếu chi/thu, write-off, reverse write-off, khóa/mở kỳ. Thiết bị không có vân tay thì fallback passcode thiết bị (`disableDeviceFallback: false`). Web preview bỏ qua — bản store không ship target web.

---

## Cấu phần 3 — UI/UX mobile

### Ánh xạ màn web → thay đổi bắt buộc

Web là sidebar + bảng (`ShellChrome`, `mobile-bar` hamburger dưới 900px, `BillListWorkspace`, `FilterBar`). Mobile là bottom tab tối đa 5 mục, do `BuildRoleAdaptiveBottomTabs` và `RoleAdaptiveTabsLayout`.

| Web | Mobile hiện có | Bắt buộc khi thương mại |
| --- | --- | --- |
| `/dashboard` | `(tabs)/dashboard.tsx` | 1 cột KPI. Không lưới 5 ô desktop. Skeleton, kéo để tải lại |
| `/bills`, `/orders`, `/shipments` | `(tabs)/bills.tsx`, `bills/[id].tsx` | Bỏ bảng. Thẻ `CompactTxnCard`. Infinite scroll `pageSize=20`. Nút chính 1 việc: mở Bill |
| `/costs`, `/costs/shared` | `(tabs)/costs.tsx` | Form 1 cột. Nhãn Expected / Confirmed / Actual tách, không gộp một status giả |
| `/revenues` | `(tabs)/revenues.tsx` | Ẩn hẳn khi `canViewRevenue=false`. Margin chỉ khi `canViewMargin` |
| `/ap-ar` | `(tabs)/ap.tsx`, `(tabs)/ar.tsx` | Tách tab theo persona. Không một màn vừa AP vừa AR |
| `/documents`, receive, match | `(tabs)/documents.tsx`, `AttachmentGallery` | Ảnh full-width, nút chụp ≥ 48dp. Khớp N:N là wizard từng bước |
| `/queues/approvals` | `(tabs)/approvals.tsx` | Before → After, rồi Face ID, rồi `POST /api/approvals/{id}/approve` |
| `/control`, queues exception/variance | `(tabs)/control.tsx` | Hàng đợi thẻ. Một quyết định mỗi thẻ |
| `/settlements`, payments, collections | `modules/settlements.tsx` | Không nằm bottom tab mặc định (đúng persona). Sticky “Ghi phiếu” phía trên bàn phím |
| `/financial-closes` | `modules/closes.tsx` | Read + khóa kỳ có biometric. Snapshot chỉ đọc |
| `/reports`, aging | `modules/reports.tsx` | Biểu đồ rút gọn + xuất để web. Không nhét bảng tuổi nợ 12 cột |
| `/rate-cards`, `TariffMatrix` | `modules/rates.tsx` | Tra cứu và so sánh. Sửa ma trận giá để trên web |
| Catalog hub, settings hub | `modules/master.tsx`, `modules/settings.tsx` | Tra cứu. Tạo user, backup, import CSV, sổ chính sách: ở web |
| Login web | `app/login.tsx` | Xóa mật khẩu mặc định `abc123` và danh sách `DEMO_ROLES` khỏi binary production |

Luật chạm và cuộn:

- Vùng bấm ≥ 44pt (iOS) / 48dp (Android). Tab bar đã `height: 56 + insets.bottom` — giữ. Nút header `paddingVertical: 6` trong `(tabs)/_layout.tsx` nâng lên ≥ 44pt hoặc bỏ, vì đã có tab Phân hệ.
- Cuộn dọc một trục. Không lồng `ScrollView` ngang kiểu bảng web. List dùng `FlatList` (windowing), không map toàn bộ vào `ScrollView`.
- Bottom nav: đúng 5 tab từ server. Persona `executive_control`: Tổng quan, Phê duyệt, Kiểm soát, Bill, Phân hệ. `cost_accountant` không có tab Doanh thu/AR. `field_ops`: Vận hành, Quét, CP dự kiến, Đồng bộ, Phân hệ.
- Một hành động chính mỗi màn, dính đáy, trên safe area. Hành động hủy/xóa là secondary, có xác nhận.
- Trạng thái: loading, rỗng, lỗi tiếng Việt (`LcmsApiException.parsed.messageVi`), thành công. Không màn trắng.
- Tiền: `apps/web/lib/money.ts` / shared formatter. Hiển thị `dd/MM/yyyy` và `Asia/Ho_Chi_Minh` từ `MobileTenantDto`. Số lưu UTC trên server.

Màn chỉ dành cho web (không đưa form đầy đủ lên store v1): import đối tác/vận hành/bảng giá, backup tenant, sửa ma trận phụ phí, ma trận quyền `permission-matrix`, cấu hình SMTP.

### Native thương mại

**Push.** Xin quyền sau khi user vào được home, không ngay màn login. `registerPushTokenBestEffort` đã `POST /api/notifications/devices` với `deviceToken`, `platform`, `deviceName`, `appVersion`. Token là Expo push token (FCM/APNs nằm dưới Expo). Server phải có consumer outbox gọi Expo Push API `https://exp.host/--/api/v2/push/send` bằng token đã lưu. Payload chỉ có `title`, `body`, `objectType`, `objectId`. App mở thông báo thì refetch API (nguồn sự thật), không tin số trên notification. Thu hồi device khi logout và khi Expo trả `DeviceNotRegistered`.

**Face ID / vân tay.** Hai mức:

- Mở khóa phiên: mọi persona, lúc cold start và khi quay lại app.
- Step-up tiền: `verifyBiometricForMoneyAction` ngay trước mutation. `NSFaceIDUsageDescription` đã có trong `app.json`. Android: `USE_BIOMETRIC`. Không gửi template sinh trắc lên server. Server vẫn authorize bằng JWT + permission + data scope.

**Mất mạng.**

- Cho vào outbox: tạo CP dự kiến, ảnh chứng từ, ghi chú hiện trường. `enqueueOfflineMutation` đã sinh `idempotencyKey` và giữ `ifMatch`.
- Cấm offline: approve, write-off, allocate payment/collection, recognize exposure, financial close, reverse. Các lệnh này cần online + biometric + `If-Match`.
- UI tab Đồng bộ (`(tabs)/offline.tsx`): pending / syncing / conflict / failed. 409 hoặc row version lệch → `conflict`, user chọn tải lại rồi ghi đè có chủ đích.
- Banner “Đang ngoại tuyến” khi `fetch` fail network. Không nuốt lỗi tiền thành “đã lưu nháp”.

---

## Cấu phần 4 — Lộ trình 8 tuần

Mỗi tuần xong khi tiêu chí nghiệm thu đạt trên build EAS `preview` trỏ `https://cms.lsslogistics.vn`. Tiền và auth có test server.

### Tuần 1 — Khóa phiên và bỏ demo

- Backend: logout nhận refresh và revoke; login theo `tenantCode`; production không nâng quyền bootstrap khi user không có role.
- Mobile: `logout` gửi body `{ refreshToken }`; khóa Face ID lúc mở app; xóa `DEMO_ROLES` và mật khẩu `abc123` khi `EXPO_PUBLIC_SHOW_DEMO_PERSONAS=false`; cấm sửa API URL trên production.
- File: `src/LCMS.Api/Endpoints/AuthEndpoints.cs`, `src/LCMS.Api/Auth/AuthTokenService.cs`, `src/LCMS.Application/Mobile/GetMobileBootstrapQuery.cs`, `apps/mobile/src/auth/AuthContext.tsx`, `apps/mobile/src/api/client.ts`, `apps/mobile/app/login.tsx`, `apps/mobile/eas.json`.
- Nghiệm thu: logout xong gọi lại `/api/auth/refresh` với token cũ → 401. User tenant A không đăng nhập được bằng mật khẩu nếu email thuộc tenant B. Binary production không chứa chuỗi `abc123`. Cold start có token vẫn vào home sau Face ID.

### Tuần 2 — Danh sách gọn, tiết kiệm băng thông

- Backend: `GET /api/mobile/feed/bills|costs|revenues|payables|receivables|documents` trả trang 20 field thẻ + `rowVersion`. Giữ endpoint đầy đủ cho màn chi tiết.
- Mobile: mọi list gọi feed, `FlatList`, kéo tải thêm. Upload ảnh bằng multipart tới `/api/attachments`.
- File: `src/LCMS.Api/Endpoints/MobileAndAttachmentEndpoints.cs`, query mới cạnh `GetMobileBootstrapQuery.cs`, `apps/mobile/app/(tabs)/bills.tsx`, `costs.tsx`, `revenues.tsx`, `ap.tsx`, `ar.tsx`, `documents.tsx`, `apps/mobile/src/api/client.ts`, `apps/mobile/src/components/CompactTxnCard.tsx`.
- Nghiệm thu: list 20 Bill trên mạng 3G < 80KB JSON (đo response). Ảnh 1MB không đi qua base64. Persona cost không thấy field doanh thu trong payload feed.

### Tuần 3 — Push thật

- Backend: consumer outbox topic `notification.push` → Expo Push API. Lưu ticket. Gỡ `UserPushDevice.IsActive=false` khi token chết. Không fan-out 200 user nếu event có danh sách người nhận cụ thể (sửa `NotificationPublisher` đang `Take(200)` mọi user active).
- Mobile: xin quyền notification sau home; tap notification điều hướng `Href`/`objectId`; mở app thì `GET` inbox `/api/notifications`.
- File: `src/LCMS.Infrastructure/Notifications/NotificationPublisher.cs`, worker outbox hiện có dưới Infrastructure, `src/LCMS.Application/Notifications/PushDeviceCommandsAndQueries.cs`, `apps/mobile/src/auth/AuthContext.tsx`, `apps/mobile/app.json` (plugin `expo-notifications`).
- Nghiệm thu: tạo approval trên web → máy đã đăng ký nhận push < 30s → tap mở đúng hàng đợi. Logout → push sau đó không tới máy đó.

### Tuần 4 — UX chạm và SoD trên UI

- Mobile: nút ≥ 44/48; một CTA; empty/error/skeleton; ẩn module trái `financialVisibility` và `enabledModules` (license `TenantLicenseModules`).
- Không đưa form import/backup/ma trận giá vào luồng chính.
- File: `apps/mobile/app/(tabs)/_layout.tsx`, các màn `(tabs)/*`, `app/modules/*.tsx`, `apps/mobile/src/components/*`.
- Nghiệm thu: tài khoản `cost@` không mở được route doanh thu/AR/lợi nhuận (deep link bị chặn). Tài khoản `revenue@` không thấy chi phí. VoiceOver/TalkBack đọc được nút chính. Không control nào bấm mà không gọi API hoặc không báo “chỉ trên web”.

### Tuần 5 — Offline đúng biên

- Mobile: whitelist endpoint được `enqueueOfflineMutation`. Lệnh tiền từ chối khi offline kèm câu tiếng Việt. Màn đồng bộ xử lý `conflict`.
- Backend: giữ idempotency hiện có; test gửi hai lần cùng `Idempotency-Key` không nhân đôi phiếu chi.
- File: `apps/mobile/src/offline/outbox.ts`, `(tabs)/offline.tsx`, `(tabs)/scanner.tsx`, `SettlementEndpoints.cs` (chỉ test, không đổi hợp đồng nếu đã idempotent).
- Nghiệm thu: máy bay tắt mạng, chụp chứng từ, bật mạng, một attachment. Tắt mạng bấm Duyệt → không vào outbox, không đổi trạng thái approval. Double-tap Ghi phiếu online → một `Payment`.

### Tuần 6 — Pinning, R8, quyền máy

- Mobile: config plugin SSL pin SPKI + pin dự phòng. `eas.json` production `app-bundle`, R8. Gỡ `RECORD_AUDIO` / `android.permission.RECORD_AUDIO` khỏi `app.json` (camera chỉ chụp ảnh và quét mã). Rà `NSCameraUsageDescription`, `NSFaceIDUsageDescription` — đã có, giữ câu tiếng Việt cụ thể.
- File: `apps/mobile/app.json`, `apps/mobile/eas.json`, plugin mới `apps/mobile/plugins/`, `apps/mobile/scripts/generate-assets.ps1` (icon 1024 đã có asset).
- Nghiệm thu: request tới host khác cert bị chặn. Proxy bắt TLS user-trust không đọc được body API. AAB không xin micro. Release build không còn sourcemap công khai.

### Tuần 7 — Xóa tài khoản, privacy, thanh toán nghiệp vụ

- Backend: `POST /api/auth/account-deletion` với mật khẩu hiện tại. Set `User.IsActive=false`, `SoftDelete`, `RevokeAllForUserAsync`, tắt mọi `UserPushDevice`. Ghi `AuditEvent`. Không xóa sổ, chứng từ, snapshot. Trang public `/privacy` trên web (không login).
- Mobile: mục trong `modules/settings.tsx`: Xóa tài khoản (xác nhận + mật khẩu + biometric), link Chính sách bảo mật, mục quyền Camera/Face ID. Màn phiếu chi/thu gọi `/api/payments` và `/api/collections` sau biometric. Không StoreKit.
- File: `AuthEndpoints.cs`, `User.cs` (dùng `SoftDelete` sẵn), `apps/mobile/app/modules/settings.tsx`, `apps/web` trang privacy tĩnh, `app.json` `extra.privacyPolicyUrl`.
- Nghiệm thu: sau xóa tài khoản, login → 401, refresh cũ → 401, Bill cũ còn trên web tenant. Privacy URL mở không cần đăng nhập, HTTPS.

### Tuần 8 — Store

- EAS `production`: `autoIncrement`, `EXPO_PUBLIC_SHOW_DEMO_PERSONAS=false`, API production. TestFlight + Internal testing (20 tài khoản thật: controller, cost, revenue, ops).
- Điền App Privacy (Apple) và Data safety (Google) khớp dữ liệu thật: email, tên, mã thuê bao, dữ liệu tài chính doanh nghiệp, ảnh chứng từ, định danh thiết bị cho push.
- File: `apps/mobile/eas.json`, `apps/mobile/app.json`, metadata store (ngoài git hoặc `apps/mobile/store/`).
- Nghiệm thu: crash-free buổi UAT nội bộ. Không demo login. Reviewer dùng tài khoản do mình cấp (không public signup). Checklist cấu phần 5 đạt.

---

## Cấu phần 5 — Checklist store và thanh toán

### Apple

- Tài khoản xóa được trong app (tuần 7), không chỉ “gửi email cho admin”. Guideline 5.1.1(v). Giải thích trên màn hình: khóa đăng nhập và dữ liệu cá nhân phiên; chứng từ kế toán giữ theo luật, vì `EntityBase.SoftDelete` không đụng sổ.
- Privacy Policy URL trong App Store Connect và trong app, cùng URL.
- Mục đích quyền đúng chữ trong `Info.plist`: Camera (quét HAWB/MAWB, chụp chứng từ), Face ID (xác nhận thao tác tài chính). Không xin danh bạ, vị trí, micro, ảnh thư viện nếu chưa dùng.
- `PrivacyInfo.xcprivacy`: UserDefaults / file timestamp chỉ nếu API mà SDK chạm yêu cầu. Expo 57 khai báo qua plugin.
- Encryption: JWT app dùng HTTPS, không tự viết crypto ngoài OS → thường khai báo exempt (ITSAppUsesNonExemptEncryption=false) sau khi pháp chế xác nhận. Không thêm thuật toán riêng.
- Đăng nhập demo công khai là lý do reject. Reviewer nhận tài khoản qua ghi chú review, tenant riêng, `IsActive`, hết hạn sau review.
- Sign in with Apple: không bắt buộc vì app không đưa login Google/Facebook.
- Tài khoản do admin tạo (`POST /api/users`), app không có màn đăng ký. Ghi rõ trong review notes.

### Google Play

- Data safety form trùng privacy policy.
- Target API level đúng yêu cầu Play tại thời điểm nộp (Expo 57 / RN 0.86 chọn qua EAS image mới).
- Quyền nhạy cảm: chỉ `CAMERA`, `USE_BIOMETRIC`, `POST_NOTIFICATIONS`, `INTERNET`, `VIBRATE`. Xóa `RECORD_AUDIO` đang khai trong `app.json`.
- Foreground service: không cần nếu không tracking nền.
- Tài khoản xóa: URL hoặc in-app, cùng luồng tuần 7.
- AAB (không APK production). Play App Signing bật.
- Internal testing track trước production. Staged rollout 10% → 50% → 100%.

### Thanh toán — không IAP

LCMS không bán hàng số cho người dùng cuối. Tiền trong app là nghiệp vụ doanh nghiệp:

- Chi tiền nhà cung cấp: `CreatePaymentCommand` qua `POST /api/payments`, phân bổ `AllocatePaymentCommand` → `POST /api/payments/{id}/allocations`, chốt `FinalizePaymentAllocationCommand`.
- Thu tiền khách: `POST /api/collections` và `collection-allocations`.
- Công nợ: exposure recognize, write-off, reverse trên `/api/accounts-payable` và `/api/accounts-receivable`.
- License thuê bao: `/api/tenant-license` (hợp đồng công ty), không phải consumable StoreKit.

App Store và Play tính IAP cho hàng số / dịch vụ số tiêu trong app. Phiếu chi, phiếu thu, gạch nợ AP/AR là nghĩa vụ ngoài app (ngân hàng, hóa đơn). Đưa các lệnh đó qua In-App Purchase sẽ sai sổ (Expected/Confirmed/Actual, Received khác Settled) và bị tính phí store trên tiền hàng logistics.

Cách nối đúng:

- Mobile chỉ là client của `SettlementEndpoints`.
- Trước khi ghi: biometric + permission server + `Idempotency-Key` + `If-Match`.
- Số tiền user gõ không phải receipt Apple/Google.
- Nếu sau này bán gói phần mềm tự phục vụ trên store: sản phẩm IAP riêng “quyền dùng LCMS”, webhook chỉ cập nhật `TenantLicense` / `TenantLicenseModules`. Webhook đó không tạo `Payment` và không đụng Bill. Việc đó là sản phẩm khác, không nằm trong phát hành v1.

Phân phối thêm cho nhân viên nội bộ: Apple Business Manager (Custom app) và Managed Google Play, cùng binary, cùng API. Store công khai vẫn cần xóa tài khoản và privacy vì người dùng có thể cài từ store.

---

## Rủi ro còn lại

- Consumer push chưa tồn tại. Tuần 3 là đường critical.
- Login xuyên tenant là lỗ hổng trước khi lên store. Tuần 1 chặn.
- Fallback quyền `DataScopes.All` khi user chưa gán role không được phép trên production.
- Pinning làm gãy app nếu xoay cert không có pin dự phòng và không phát hành bản mới trước.
- Ảnh chứng từ là dữ liệu nhạy cảm. Privacy form phải khai “ảnh”, không khai “không thu thập”.
