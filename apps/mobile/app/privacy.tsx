import React from "react";
import {
  Linking,
  ScrollView,
  StyleSheet,
  Text,
  TouchableOpacity,
  View,
} from "react-native";
import { useRouter } from "expo-router";

export default function MobilePrivacyScreen() {
  const router = useRouter();

  const handleOpenWeb = () => {
    Linking.openURL("https://cms.lsslogistics.vn/privacy");
  };

  const handleEmailPrivacy = () => {
    Linking.openURL("mailto:privacy@lsslogistics.vn?subject=[CMS%20LSS]%20Yêu%20cầu%20về%20Quyền%20riêng%20tư%20dữ%20liệu");
  };

  return (
    <ScrollView style={styles.container} contentContainerStyle={styles.content}>
      {/* Top Bar */}
      <View style={styles.topBar}>
        <TouchableOpacity
          style={styles.backBtn}
          onPress={() => router.back()}
          accessibilityRole="button"
          accessibilityLabel="Quay lại"
        >
          <Text style={styles.backBtnText}>‹ Quay lại</Text>
        </TouchableOpacity>
        <TouchableOpacity
          style={styles.webBtn}
          onPress={handleOpenWeb}
          accessibilityRole="button"
          accessibilityLabel="Mở bản Web"
        >
          <Text style={styles.webBtnText}>🌐 Mở trên Web</Text>
        </TouchableOpacity>
      </View>

      {/* Header Badge */}
      <View style={styles.badgeRow}>
        <View style={styles.brandBadge}>
          <Text style={styles.brandBadgeText}>LSS</Text>
        </View>
        <Text style={styles.headerSubtitle}>CMS LOGISTICS — PHIÊN BẢN v1.0</Text>
      </View>

      <Text style={styles.title}>Chính sách về Quyền riêng tư</Text>
      <Text style={styles.effectiveDate}>
        Ngày có hiệu lực: 16/10/2026 • Áp dụng cho Hệ sinh thái Web CMS &amp; Ứng dụng Di động LSS
      </Text>

      {/* Quick Store Review Card */}
      <View style={styles.storeReviewCard}>
        <Text style={styles.storeReviewTitle}>
          🛡️ Tóm tắt Tuân thủ Google Play &amp; Apple App Store
        </Text>
        <Text style={styles.storeReviewDesc}>
          Ứng dụng LSS LCMS Mobile tuân thủ đầy đủ nguyên tắc bảo vệ dữ liệu cá nhân, PII và minh bạch quyền thiết bị:
        </Text>
        <View style={styles.bulletItem}>
          <Text style={styles.bulletDot}>•</Text>
          <Text style={styles.bulletText}>
            <Text style={styles.boldText}>Định vị GPS:</Text> Chỉ thu thập khi tài xế kích hoạt nhận chuyến (Start Leg) và kết thúc ngay khi giao xong hàng (Complete Leg). Tuyệt đối không theo dõi ngầm ngoài giờ làm việc.
          </Text>
        </View>
        <View style={styles.bulletItem}>
          <Text style={styles.bulletDot}>•</Text>
          <Text style={styles.bulletText}>
            <Text style={styles.boldText}>Quyền Camera:</Text> Dùng để quét mã vận đơn (HAWB/MAWB) và chụp ảnh chứng từ giao nhận (POD)/phiếu chi hiện trường.
          </Text>
        </View>
        <View style={styles.bulletItem}>
          <Text style={styles.bulletDot}>•</Text>
          <Text style={styles.bulletText}>
            <Text style={styles.boldText}>Sinh trắc học (Face ID/Vân tay):</Text> Xử lý hoàn toàn cục bộ trên phần cứng an toàn của điện thoại. Máy chủ LSS không bao giờ nhận hay lưu trữ dữ liệu sinh trắc học thô.
          </Text>
        </View>
        <View style={styles.bulletItem}>
          <Text style={styles.bulletDot}>•</Text>
          <Text style={styles.bulletText}>
            <Text style={styles.boldText}>Quyền xóa tài khoản:</Text> Người dùng có thể yêu cầu xóa tài khoản trực tiếp trong ứng dụng hoặc gửi email bảo mật, xử lý trong vòng 07 ngày làm việc.
          </Text>
        </View>
      </View>

      {/* Section 1 */}
      <View style={styles.sectionCard}>
        <Text style={styles.sectionHeading}>1. Giới thiệu &amp; Phạm vi áp dụng</Text>
        <Text style={styles.paragraph}>
          Công ty Cổ phần Dịch vụ Logistics LSS cam kết bảo vệ toàn diện quyền riêng tư và dữ liệu cá nhân của mọi cá nhân, tài xế, điều phối viên và đối tác sử dụng Hệ thống Quản lý Chi phí Logistics (CMS/LCMS).
        </Text>
        <Text style={styles.paragraph}>
          Chính sách này áp dụng thống nhất cho Nền tảng Web CMS (https://cms.lsslogistics.vn) và Ứng dụng Di động LSS LCMS Mobile phát hành trên Apple App Store và Google Play Store.
        </Text>
      </View>

      {/* Section 2 */}
      <View style={styles.sectionCard}>
        <Text style={styles.sectionHeading}>2. Loại thông tin thu thập</Text>
        <Text style={styles.paragraph}>
          <Text style={styles.boldText}>• Thông tin tài khoản (PII):</Text> Họ tên, email công việc, số điện thoại, vai trò nghiệp vụ (Role), đơn vị công tác.
        </Text>
        <Text style={styles.paragraph}>
          <Text style={styles.boldText}>• Dữ liệu chứng từ tài chính:</Text> Vận đơn (HAWB/MAWB), ảnh chụp biên bản bàn giao (POD), phiếu thu/chi tạm ứng, vé cầu đường.
        </Text>
        <Text style={styles.paragraph}>
          <Text style={styles.boldText}>• Định vị GPS lộ trình:</Text> Tọa độ di chuyển trong suốt hành trình chặng vận chuyển nhằm tính toán quãng đường thực tế, đối soát phụ phí BOT và nhiên liệu.
        </Text>
        <Text style={styles.paragraph}>
          <Text style={styles.boldText}>• Nhật ký kiểm toán (Audit Trail):</Text> Ghi nhận thời gian và hành động phê duyệt nhằm đảm bảo tính minh bạch tài chính.
        </Text>
      </View>

      {/* Section 3 */}
      <View style={styles.sectionCard}>
        <Text style={styles.sectionHeading}>3. Mục đích sử dụng thông tin</Text>
        <Text style={styles.paragraph}>
          • Xác thực danh tính và phân quyền theo vai trò (Role-based Data Scope).
        </Text>
        <Text style={styles.paragraph}>
          • Kiểm soát và đối soát tự động chi phí vận tải: Dự toán → Xác nhận → Thực tế → Quyết toán.
        </Text>
        <Text style={styles.paragraph}>
          • Xác thực mốc thời gian giao nhận và bằng chứng giao dịch thực tế phục vụ thanh toán.
        </Text>
        <Text style={styles.paragraph}>
          • Tuân thủ chế độ kế toán và nghĩa vụ thuế theo quy định pháp luật Việt Nam.
        </Text>
      </View>

      {/* Section 4 */}
      <View style={styles.sectionCard}>
        <Text style={styles.sectionHeading}>4. Phân quyền &amp; Cam kết bảo mật</Text>
        <Text style={styles.paragraph}>
          <Text style={styles.boldText}>Cam kết bất di bất dịch:</Text> LSS tuyệt đối không bán, cho thuê hoặc thương mại hóa dữ liệu cá nhân cho bất kỳ bên thứ ba nào vì mục đích tiếp thị.
        </Text>
        <Text style={styles.paragraph}>
          Hệ thống áp dụng nguyên tắc phân tách nhiệm vụ (Maker-Checker, SoD). Tài xế chỉ xem và cập nhật chặng vận chuyển của mình, không truy cập dữ liệu doanh thu hoặc biên lợi nhuận của đơn vị.
        </Text>
      </View>

      {/* Section 5 */}
      <View style={styles.sectionCard}>
        <Text style={styles.sectionHeading}>5. Bảo mật &amp; Lưu trữ dữ liệu</Text>
        <Text style={styles.paragraph}>
          • Toàn bộ kết nối di động được mã hóa an toàn qua HTTPS/TLS 1.2 &amp; TLS 1.3 và cơ chế khóa chứng chỉ SSL Pinning.
        </Text>
        <Text style={styles.paragraph}>
          • Dữ liệu cách ly tuyệt đối theo từng doanh nghiệp khách hàng (Tenant Isolation).
        </Text>
        <Text style={styles.paragraph}>
          • Token đăng nhập lưu trữ an toàn trong phân vùng phần cứng thiết bị (Keychain trên iOS, Encrypted Storage trên Android).
        </Text>
      </View>

      {/* Section 6 - Account Deletion */}
      <View style={[styles.sectionCard, styles.deletionCard]}>
        <Text style={[styles.sectionHeading, { color: "#C53030" }]}>
          6. Quyền của người dùng &amp; Xóa tài khoản
        </Text>
        <Text style={styles.paragraph}>
          Người dùng có đầy đủ quyền kiểm tra, đính chính và yêu cầu xóa tài khoản cùng dữ liệu cá nhân theo các hình thức sau:
        </Text>
        <View style={styles.actionBox}>
          <Text style={styles.boldText}>Cách 1: Trực tiếp trong ứng dụng</Text>
          <Text style={styles.subText}>
            Vào Mô-đun → Cài đặt tài khoản → Yêu cầu xóa tài khoản và nhập mật khẩu xác nhận.
          </Text>
        </View>
        <View style={[styles.actionBox, { marginTop: 8 }]}>
          <Text style={styles.boldText}>Cách 2: Gửi thư điện tử bảo mật</Text>
          <Text style={styles.subText}>
            Gửi email đến <Text style={{ color: "#2563EB" }}>privacy@lsslogistics.vn</Text> để tiếp nhận xử lý trong vòng 07 ngày làm việc.
          </Text>
        </View>
        <Text style={[styles.subText, { marginTop: 10, fontStyle: "italic" }]}>
          * Lưu ý: Các hóa đơn, chứng từ kế toán đã phát sinh hợp pháp sẽ được ẩn danh theo quy định bắt buộc của Luật Kế toán Việt Nam.
        </Text>
      </View>

      {/* Section 7 & 8 */}
      <View style={styles.sectionCard}>
        <Text style={styles.sectionHeading}>7. Thông tin liên hệ hỗ trợ</Text>
        <Text style={styles.paragraph}>
          <Text style={styles.boldText}>• Đơn vị chủ quản:</Text> Công ty Cổ phần Dịch vụ Logistics LSS
        </Text>
        <Text style={styles.paragraph}>
          <Text style={styles.boldText}>• Email bảo mật:</Text> privacy@lsslogistics.vn
        </Text>
        <Text style={styles.paragraph}>
          <Text style={styles.boldText}>• Email hỗ trợ:</Text> hotro@lsslogistics.vn
        </Text>
        <Text style={styles.paragraph}>
          <Text style={styles.boldText}>• Hotline:</Text> 1900 636 789
        </Text>
        <Text style={styles.paragraph}>
          <Text style={styles.boldText}>• Cổng trực tuyến:</Text> https://cms.lsslogistics.vn/privacy
        </Text>

        <TouchableOpacity
          style={styles.contactBtn}
          onPress={handleEmailPrivacy}
          accessibilityRole="button"
        >
          <Text style={styles.contactBtnText}>✉️ Gửi email cho Ban Bảo mật LSS</Text>
        </TouchableOpacity>
      </View>

      <View style={{ height: 40 }} />
    </ScrollView>
  );
}

const styles = StyleSheet.create({
  container: {
    flex: 1,
    backgroundColor: "#F8FAFC",
  },
  content: {
    padding: 16,
    paddingTop: 56,
  },
  topBar: {
    flexDirection: "row",
    justifyContent: "space-between",
    alignItems: "center",
    marginBottom: 16,
  },
  backBtn: {
    paddingVertical: 6,
    paddingHorizontal: 10,
    backgroundColor: "#E2E8F0",
    borderRadius: 8,
  },
  backBtnText: {
    fontSize: 13,
    fontWeight: "700",
    color: "#0F172A",
  },
  webBtn: {
    paddingVertical: 6,
    paddingHorizontal: 12,
    backgroundColor: "#EFF6FF",
    borderWidth: 1,
    borderColor: "#BFDBFE",
    borderRadius: 8,
  },
  webBtnText: {
    fontSize: 12,
    fontWeight: "700",
    color: "#2563EB",
  },
  badgeRow: {
    flexDirection: "row",
    alignItems: "center",
    gap: 8,
    marginBottom: 8,
  },
  brandBadge: {
    backgroundColor: "#0F172A",
    paddingHorizontal: 8,
    paddingVertical: 3,
    borderRadius: 6,
  },
  brandBadgeText: {
    color: "#FFFFFF",
    fontWeight: "800",
    fontSize: 12,
  },
  headerSubtitle: {
    fontSize: 11,
    fontWeight: "700",
    color: "#64748B",
  },
  title: {
    fontSize: 22,
    fontWeight: "800",
    color: "#0F172A",
    marginBottom: 6,
  },
  effectiveDate: {
    fontSize: 11,
    color: "#64748B",
    marginBottom: 16,
    lineHeight: 16,
  },
  storeReviewCard: {
    backgroundColor: "#EFF6FF",
    borderLeftWidth: 4,
    borderLeftColor: "#2563EB",
    borderRadius: 10,
    padding: 14,
    marginBottom: 16,
  },
  storeReviewTitle: {
    fontSize: 14,
    fontWeight: "800",
    color: "#1E40AF",
    marginBottom: 6,
  },
  storeReviewDesc: {
    fontSize: 12,
    color: "#334155",
    marginBottom: 8,
    lineHeight: 18,
  },
  bulletItem: {
    flexDirection: "row",
    alignItems: "flex-start",
    marginBottom: 6,
  },
  bulletDot: {
    fontSize: 14,
    color: "#2563EB",
    marginRight: 6,
    lineHeight: 18,
  },
  bulletText: {
    flex: 1,
    fontSize: 12,
    color: "#334155",
    lineHeight: 18,
  },
  boldText: {
    fontWeight: "700",
    color: "#0F172A",
  },
  sectionCard: {
    backgroundColor: "#FFFFFF",
    borderRadius: 12,
    borderWidth: 1,
    borderColor: "#E2E8F0",
    padding: 16,
    marginBottom: 14,
  },
  deletionCard: {
    borderLeftWidth: 4,
    borderLeftColor: "#F32945",
  },
  sectionHeading: {
    fontSize: 15,
    fontWeight: "800",
    color: "#0F172A",
    marginBottom: 10,
  },
  paragraph: {
    fontSize: 13,
    color: "#334155",
    lineHeight: 20,
    marginBottom: 8,
  },
  actionBox: {
    backgroundColor: "#F8FAFC",
    borderWidth: 1,
    borderColor: "#E2E8F0",
    borderRadius: 8,
    padding: 10,
  },
  subText: {
    fontSize: 12,
    color: "#64748B",
    lineHeight: 17,
  },
  contactBtn: {
    backgroundColor: "#0F172A",
    borderRadius: 8,
    paddingVertical: 11,
    alignItems: "center",
    marginTop: 10,
  },
  contactBtnText: {
    color: "#FFFFFF",
    fontSize: 13,
    fontWeight: "700",
  },
});
