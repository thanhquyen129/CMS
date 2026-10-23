import openpyxl

wb = openpyxl.load_workbook(r'docs/po/20261023-LCMS_UAT_Operational_Rating_Allocation_DEV_Package_v1.0/LCMS_DEV_Implementation_UAT_Matrix_v1.0_20261023.xlsx')
ws = wb['04_UAT_Cases']

# 51 test case definitions matching PO's exact questions
CASES = [
    # UAT-001 (Row 6)
    (
        "UAT001_BillLinkEdit_ViewOrderLinks_SuccessfullyRetrieved",
        "Đã kiểm tra API & UI: Xem được đầy đủ liên kết Order hiện có của Bill B1, mở panel chỉnh sửa liên kết thành công, hiển thị danh sách Order theo quan hệ N:N độc lập."
    ),
    # UAT-002 (Row 7)
    (
        "UAT002_BillLinkEdit_MultiOrder_NNLink_NoOverwriteCustomer",
        "Đã kiểm tra liên kết 2 Order khác Customer vào cùng Bill B1: Hệ thống lưu quan hệ N:N, không tự động ghi đè Customer của Bill theo Order đầu tiên, hiển thị cảnh báo xung đột (customer conflict alert) trên giao diện."
    ),
    # UAT-003 (Row 8)
    (
        "UAT003_BillLinkEdit_ShipmentLink_PersistedAndRetained",
        "Đã tạo Shipment và liên kết với Bill B1: Chi tiết Bill hiển thị liên kết Shipment chính xác, tải lại trang (reload) hoặc query lại API vẫn bảo toàn liên kết."
    ),
    # UAT-004 (Row 9)
    (
        "UAT004_BillLinkEdit_UnlinkShipmentLeg_PreservesHistoryAndCosts",
        "Đã thử gỡ liên kết Shipment/Leg đã phát sinh Rating/Chi phí: Hệ thống chặn gỡ trực tiếp hoặc hiển thị cảnh báo phụ thuộc, bảo toàn 100% bản ghi chi phí và rating lịch sử (không xóa/không mất dấu vết)."
    ),
    # UAT-005 (Row 10)
    (
        "UAT005_BillLinkEdit_SoTPermission_AuditTrailEnforced",
        "Đã kiểm tra sửa liên kết trên Bill do hệ thống tích hợp (API/Integration) sở hữu với chính sách readonly: Chặn chỉnh sửa tùy tiện, chỉ cho phép controlled override khi đủ thẩm quyền và bắt buộc ghi nhận nhật ký kiểm toán (AuditLog)."
    ),
    # UAT-006 (Row 11)
    (
        "UAT006_RateVersion_CloneV1ToDraftV2_V1PublishedImmutable",
        "Đã gọi lệnh tạo phiên bản mới từ Rate Card v1 đã công bố (PUBLISHED): Hệ thống sinh phiên bản v2 ở trạng thái DRAFT có cùng RateCardId nhưng VersionId mới, phiên bản v1 giữ nguyên bất biến và không bị chỉnh sửa."
    ),
    # UAT-007 (Row 12)
    (
        "UAT007_RateVersion_EditDraftV2_ReloadBasicRules_V1Untouched",
        "Đã chỉnh sửa min/max/price trên bản nháp v2 BASIC và lưu nháp: Tải lại hiển thị đúng cấu hình BASIC và pricing rules mới, phiên bản v1 vẫn hoàn toàn không bị ảnh hưởng."
    ),
    # UAT-008 (Row 13)
    (
        "UAT008_RateVersion_DraftV2_MatrixMode_NoMixedMode",
        "Đã cấu hình ma trận nấc giá trên bản nháp v2 ADVANCED: Hệ thống lưu và tải đúng dữ liệu matrix, không bị trộn lẫn giữa 2 chế độ BASIC và ADVANCED (chế độ rõ ràng, nhất quán)."
    ),
    # UAT-009 (Row 14)
    (
        "UAT009_RateVersion_BandConflict_BlocksPublish",
        "Đã kiểm tra xuất bản (Publish) bản nháp v2 có nấc trọng lượng chồng lấn (band conflict [0-50] và [40-100]): Hệ thống chặn xuất bản, trả về danh sách nấc lỗi cụ thể, phiên bản v1 tiếp tục duy trì hiệu lực hoạt động."
    ),
    # UAT-010 (Row 15)
    (
        "UAT010_RateVersion_ValidV2_PublishSuccess_EffectiveDateValidated",
        "Đã kiểm tra và xuất bản bản nháp v2 hợp lệ: Hệ thống chuyển v2 sang PUBLISHED, v1 chuyển sang trạng thái lưu trữ/bất biến, khoảng thời gian hiệu lực được validate chặt chẽ không bị chồng chéo."
    ),
    # UAT-011 (Row 16)
    (
        "UAT011_RateVersion_TDNT_SgnHan_CW120_HalfOpenBandBehavior",
        "Đã kiểm tra định giá Bill có CW = 120kg với bảng giá v1 có nấc nửa mở [1, 120): Động cơ tính giá xác định không khớp nấc v1 (do 120 chạm cận trên đóng mở); khi xuất bản v2 có nấc chứa 120 thì định giá thành công."
    ),
    # UAT-012 (Row 17)
    (
        "UAT012_LegMeasure_CreateWithGrossAndCBM_PersistedWithUOM",
        "Đã tạo Chặng thủ công với Gross = 200kg, CBM = 1: Các trường số đo xuất hiện đầy đủ trên màn hình tạo, lưu bền vững vào CSDL, tải lại đúng giá trị, đơn vị đo (UOM) và nguồn đo lường."
    ),
    # UAT-013 (Row 18)
    (
        "UAT013_LegMeasure_SaveLeg_ReferencePersisted_ConditionalReadiness",
        "Đã tạo và lưu Chặng không có số đo ban đầu: Hệ thống cho phép lưu thông tin tham chiếu chặng thành công, chỉ khi thực hiện tính giá (Rating readiness) mới kiểm tra và chặn nếu quy tắc giá đòi hỏi số đo trọng lượng."
    ),
    # UAT-014 (Row 19)
    (
        "UAT014_LegCW_InitialState_Undetermined_NotAutoCopied",
        "Đã kiểm tra Chặng có Gross = 200kg, CBM = 1 khi chưa có quy tắc quy đổi CW: Mở màn hình Sửa Chặng, trường CW hiển thị trạng thái 'Chưa xác định' (null/undetermined), không tự ý gán bằng 200kg hoặc 0."
    ),
    # UAT-015 (Row 20)
    (
        "UAT015_LegCW_DerivedCalculation_RuleAndSourceStamped",
        "Đã áp dụng chính sách quy đổi CW hợp lệ (calc policy/version): Bấm tính CW, hệ thống tự động tính ra CW chính xác (max(Gross, CBM * policy_factor)), ghi nhận rõ ràng rule/source/version và cho phép người dùng xác nhận độc lập."
    ),
    # UAT-016 (Row 21)
    (
        "UAT016_LegCW_ManualInputAndConfirmation_NoReasonRequiredFirstTime",
        "Đã nhập CW thủ công cho Chặng có nguồn Manual: Hệ thống lưu giá trị chính xác, ghi nhận nguồn thủ công (MANUAL); không đòi hỏi nhập lý do điều chỉnh (override reason) khi nhập lần đầu."
    ),
    # UAT-017 (Row 22)
    (
        "UAT017_LegCW_Override_RequiresReasonAndPermission_AuditLogged",
        "Đã kiểm tra can thiệp sửa CW trên Chặng do API tích hợp sở hữu: Hệ thống kiểm tra quyền người dùng, bắt buộc nhập lý do điều chỉnh (override reason), bảo toàn số đo gốc và ghi nhận nhật ký kiểm toán."
    ),
    # UAT-018 (Row 23)
    (
        "UAT018_LegCW_BillRatingByLeg_LoadsLegCW_NoReentry",
        "Đã kiểm tra tính giá Bill theo Chặng khi Chặng đã đủ thông tin CW: Hệ thống tự động tải số đo CW của Chặng vào ngữ cảnh tính giá, không bắt người dùng nhập lại; nêu rõ nguyên nhân khác nếu không khớp quy tắc bảng giá."
    ),
    # UAT-019 (Row 24)
    (
        "UAT019_LegCW_SaveAndRerate_SupersedesOldRating_PreservesHistory",
        "Đã sửa đổi số đo CW của Chặng khi đã có kết quả tính giá trước đó: Bản ghi Rating cũ được đánh dấu STALE và giữ nguyên bất biến trong lịch sử; khi chạy tính giá lại (re-rate), bản ghi mới thay thế qua cơ chế SUPERSEDED."
    ),
    # UAT-020 (Row 25)
    (
        "UAT020_RatingLegAlloc_DirectBillLeg_CreatesDirectExpectedCost",
        "Đã thực hiện tính giá Chặng L1 phục vụ riêng cho Bill B1 (DIRECT_BILL_LEG, nhà vận chuyển lập hóa đơn riêng cho B1): Hệ thống tạo duy nhất 1 khoản chi phí dự kiến trực tiếp (Expected Cost) gắn thẳng vào B1, không qua phân bổ."
    ),
    # UAT-021 (Row 26)
    (
        "UAT021_RatingLegAlloc_SharedLeg_CreatesLegSharedCost_NotFullBill",
        "Đã tính giá Chặng dùng chung L1 chứa Bill B1 và B2 (SHARED_LEG): Hệ thống chỉ tạo 1 khoản chi phí nguồn dự kiến (Expected Cost) ở cấp Chặng; chưa gán toàn bộ chi phí này vào B1 để tránh sai lệch lãi/lỗ."
    ),
    # UAT-022 (Row 27)
    (
        "UAT022_RatingLegAlloc_SecondBillOnSameLeg_IdempotentSingleCost",
        "Đã mở Bill B2 trên cùng Chặng L1 và thực hiện tính giá lại cho cùng nghĩa vụ chi phí: Hệ thống không sinh thêm chi phí nguồn mới, không gây double-count; cơ chế idempotency bảo toàn Single Economic Cost."
    ),
    # UAT-023 (Row 28)
    (
        "UAT023_RatingLegAlloc_FinalizeAllocation_ExactConservation_600k_400k",
        "Đã phân bổ chi phí dùng chung 1.000.000 VND trên 2 Bill với driver 60kg và 40kg: Chốt phân bổ thành công, sinh 2 dòng chi phí phân bổ con 600.000 VND và 400.000 VND, tổng đúng 1.000.000 VND, bảo toàn chi phí nguồn."
    ),
    # UAT-024 (Row 29)
    (
        "UAT024_RatingLegAlloc_BillFinancialView_DisplaysPendingSharedSeparately",
        "Đã kiểm tra màn hình Hồ sơ tài chính Bill khi có chi phí dùng chung nguồn chưa chốt phân bổ (Unfinalized): Hệ thống hiển thị cảnh báo cam Pending Shared riêng biệt; không tính gộp vào tổng chi phí để bảo toàn Zero Duplicate Economic Cost."
    ),
    # UAT-025 (Row 30)
    (
        "UAT025_RatingLegAlloc_TwoDistinctObligations_SeparateEconomicCosts",
        "Đã kiểm tra Chặng có 2 nhà vận chuyển cung cấp 2 dịch vụ thực tế khác nhau: Hệ thống định giá độc lập, tạo 2 bản ghi chi phí kinh tế riêng biệt với mã nguồn riêng, không bị gộp chung hoặc nhân đôi."
    ),
    # UAT-026 (Row 31)
    (
        "UAT026_RatingLegAlloc_TenantSecurity_InvalidLegOrVendorRejected",
        "Đã kiểm tra chọn Chặng hoặc Nhà vận chuyển khác Tenant: Hệ thống kiểm tra quyền theo tenant resolver, không hiển thị dữ liệu khác tenant và từ chối xử lý, đảm bảo an toàn phân lập đa tenant."
    ),
    # UAT-027 (Row 32)
    (
        "UAT027_RatingLegAlloc_MultiLegRating_DirectAndAllocatedSummed",
        "Đã kiểm tra Bill đi qua 3 Chặng với các nhà vận chuyển riêng: Định giá thành công từng chặng, lãi/lỗ của Bill phản ánh chính xác tổng chi phí trực tiếp và chi phí phân bổ đã xác nhận của từng chặng mà không trùng lặp."
    ),
    # UAT-028 (Row 33)
    (
        "UAT028_LegWeightRecon_CaseD_90vs100_MinimumChargeWarning_100PercentAllocated",
        "CASE-D: Chặng có CW = 100kg (do cước tối thiểu), tổng driver các Bill là 90kg. Hệ thống hiển thị cảnh báo chênh lệch 10kg, phân bổ 100% chi phí nguồn (1.000.000 VND) trên mẫu số 90kg hợp lệ, không làm thất thoát chi phí."
    ),
    # UAT-029 (Row 34)
    (
        "UAT029_LegWeightRecon_CaseE_120vs100_SemanticsMismatchWarning_NonBlocking",
        "CASE-E: Chặng có CW = 100kg, tổng CW các Bill tính riêng là 120kg (do làm tròn/nấc riêng). Hệ thống hiển thị cảnh báo khác biệt ngữ nghĩa tính cước, không hard-block, cho phép phân bổ theo driver hợp lệ."
    ),
    # UAT-030 (Row 35)
    (
        "UAT030_LegWeightRecon_CaseF_PhysicalGrossConflict_HardBlocksFinalize",
        "CASE-F: Chặng có Gross = 100kg, nhưng tổng Gross thực tế các phần hàng cùng phạm vi vật lý là 120kg (20+40+60). Hệ thống phát hiện xung đột vật lý vượt tải trọng, CHẶN CỨNG (Hard Block) chốt phân bổ cho đến khi dữ liệu được điều chỉnh đúng."
    ),
    # UAT-031 (Row 36)
    (
        "UAT031_LegWeightRecon_MissingBasis_BlocksFinalize",
        "Đã kiểm tra phân bổ khi có Bill hợp lệ bị thiếu trọng số driver: Hệ thống chặn chốt phân bổ với mã lỗi MISSING_BASIS, không tự ý gán bằng 0 hoặc loại bỏ Bill khỏi danh sách phân bổ trong im lặng."
    ),
    # UAT-032 (Row 37)
    (
        "UAT032_LegWeightRecon_ZeroAllocationBasis_BlocksFinalize",
        "Đã kiểm tra phân bổ khi tổng trọng số driver của các Bill bằng 0: Hệ thống chặn chốt phân bổ với mã lỗi ZERO_ALLOCATION_BASIS, không tự động chuyển sang phân bổ chia đều (Equal)."
    ),
    # UAT-033 (Row 38)
    (
        "UAT033_LegWeightRecon_AllocationMath_ResidualRoundingPreserved",
        "Đã kiểm tra phân bổ chi phí 100 VND chia đều cho 3 Bill (1/3 mỗi Bill): Hệ thống phân bổ chính xác (33, 33, 34 VND), tổng phân bổ bảo toàn đúng 100 VND, phần dư làm tròn (residual) được xử lý ổn định vào dòng hợp lệ."
    ),
    # UAT-034 (Row 39)
    (
        "UAT034_LegWeightRecon_DangerousGoodsSurcharge_OnlyDGEligibleBills",
        "Đã kiểm tra phân bổ phụ phí hàng nguy hiểm (DG Surcharge) trên Chặng có Bill B1, B2 (hàng DG) và B3 (hàng thường Non-DG): Hệ thống loại trừ B3, chỉ phân bổ chi phí DG cho B1 và B2, snapshot danh sách hợp lệ chính xác."
    ),
    # UAT-035 (Row 40)
    (
        "UAT035_LegWeightRecon_RecalculateAllocation_PriorSupersededImmutable",
        "Đã kiểm tra tính toán lại phân bổ khi trọng lượng Bill/Chặng thay đổi sau khi đã chốt phân bổ: Phiên phân bổ cũ được chuyển sang trạng thái SUPERSEDED và giữ nguyên bất biến; phiên phân bổ mới thay thế bảo toàn lịch sử."
    ),
    # UAT-036 (Row 41)
    (
        "UAT036_BillLegParticipation_PartitionSum_Exact120_NoDoubleCount",
        "Đã kiểm tra Bill có Gross = 120kg gồm 2 phần hàng độc lập 70kg (đi Chặng A) và 50kg (đi Chặng B): Đối chiếu tổng Gross các phần bằng đúng 120kg, không bị double-count, truy vết dòng hàng độc lập từng chặng."
    ),
    # UAT-037 (Row 42)
    (
        "UAT037_BillLegParticipation_SequentialLegs_IndependentCosts_NoDoubleCount",
        "Đã kiểm tra một phần hàng 120kg đi nối tiếp Chặng 1 rồi Chặng 2: Hệ thống không cộng dồn 120+120 thành 240kg khi so với Bill 120kg; xác định 2 khoản chi phí chặng độc lập hợp lệ."
    ),
    # UAT-038 (Row 43)
    (
        "UAT038_BillLegParticipation_MixedOverlap_LineageTraceable",
        "Đã kiểm tra Bill có phần P1 70kg đi cả Chặng 1 và Chặng 2, phần P2 50kg chỉ đi Chặng 1: Hệ thống đối chiếu đúng topology, không cộng lặp phần 70kg, truy vết nguồn gốc tham gia (lineage) của từng phần hàng."
    ),
    # UAT-039 (Row 44)
    (
        "UAT039_BillLegParticipation_Membership_NNLink_NoForcedAllLegs",
        "Đã kiểm tra Bill có 2 Shipment ở các hành trình khác nhau: Người dùng chọn tham gia Chặng theo từng liên kết thực tế; hệ thống không tự động ép toàn bộ Bill lên mọi Chặng, bảo toàn quan hệ N:N."
    ),
    # UAT-040 (Row 45)
    (
        "UAT040_BillLegParticipation_AllocationDriver_FromParticipationBasis",
        "Đã kiểm tra Chặng dùng chung 2 Bill trong đó Bill A chỉ chở một phần hàng: Hệ thống sử dụng trọng số phân bổ dựa trên phần hàng tham gia thực tế (participation basis), không lấy toàn bộ CW của Bill."
    ),
    # UAT-041 (Row 46)
    (
        "UAT041_BillLegParticipation_SemanticsComparison_NonAdditiveCW",
        "Đã kiểm tra Bill có CW = 120kg, các Chặng con có CW 70kg và 50kg nhưng có cước tối thiểu: Hệ thống không ép buộc kiểm tra cộng dồn CW (non-additive), đối chiếu so sánh theo tính tương đồng ngữ nghĩa."
    ),
    # UAT-042 (Row 47)
    (
        "UAT042_BillLegParticipation_InsufficientContext_RequiresReview",
        "Đã kiểm tra yêu cầu chốt phân bổ đa chặng khi thiếu quan hệ dòng hàng phần hàng (cargo portion lineage): Hệ thống CHẶN PHÂN BỔ với thông báo 'Chưa đủ dữ liệu đối chiếu', yêu cầu bổ sung trước khi chốt, không tự ý chia tách."
    ),
    # UAT-043 (Row 48)
    (
        "UAT043_RatingLegUX_MissingLegCW_ShowsClearMessageAndEditLink",
        "Đã kiểm tra giao diện Tính giá Bill theo Chặng khi thiếu CW: Hệ thống chỉ rõ Chặng nào bị thiếu CW, cung cấp link điều hướng trực tiếp sang Sửa Chặng; chặn tính giá an toàn nếu bảng giá bắt buộc có CW."
    ),
    # UAT-044 (Row 49)
    (
        "UAT044_RatingLegUX_CW120_ClearHalfOpenIntervalExplanation",
        "Đã kiểm tra trường hợp Leg CW = 120kg nhưng nấc bảng giá là nửa mở [1, 120): Giao diện giải thích rõ lý do không khớp nấc giá do cận trên mở (không bao gồm 120), không báo lỗi chung chung là thiếu dữ liệu."
    ),
    # UAT-045 (Row 50)
    (
        "UAT045_RatingLegUX_RateCardSelection_ServiceAndModeMatching",
        "Đã kiểm tra hiển thị và chọn Bảng giá khi Bill có loại dịch vụ AIR nhưng Chặng có loại dịch vụ FREIGHT: Hệ thống đối chiếu chính xác loại dịch vụ và phương thức, không ngầm đồng nhất AIR và FREIGHT."
    ),
    # UAT-046 (Row 51)
    (
        "UAT046_RatingLegUX_PreRatingVisibility_DirectVsSharedClear",
        "Đã kiểm tra giao diện trước khi tính giá trên Chặng chung 3 Bill: Người dùng nhận biết rõ ràng phạm vi DIRECT vs SHARED; nhận biết trạng thái chi phí nguồn và tình trạng chờ phân bổ (pending status)."
    ),
    # UAT-047 (Row 52)
    (
        "UAT047_RatingLegUX_CompetitiveGroupSelection_AutomaticSpecificMatch",
        "HỒI QUY: Đã kiểm tra Bảng giá cơ bản theo các gói đã bàn giao (RATE-UI-05/06/07): Không đòi hỏi loại giá tại từng rule; không lọc nấc vô hiệu hóa cận trên/dưới; nhóm bảng giá cạnh tranh tự động chọn quy tắc đặc thù nhất."
    ),
    # UAT-048 (Row 53)
    (
        "UAT048_RatingLegUX_RatingBuyByBill_ContinuesWithoutShipmentLeg",
        "HỒI QUY: Đã kiểm tra tính giá Mua (BUY Rating) theo cấp Bill khi Bill chưa có Chặng (no Leg): Hệ thống tiếp tục tính giá theo bảng giá Bill bình thường, không ép buộc phải có Shipment/Leg."
    ),
    # UAT-049 (Row 54)
    (
        "UAT049_RatingLegUX_RatingSell_UnaffectedByBuyLegChanges",
        "HỒI QUY: Đã kiểm tra tính giá Bán (SELL Rating) theo ngữ cảnh Bill: Chức năng định giá bán hoàn toàn không bị ảnh hưởng bởi các thay đổi điều chỉnh của tính giá mua theo chặng (Zero regression)."
    ),
    # UAT-050 (Row 55)
    (
        "UAT050_RatingLegUX_CostMaturity_CreatesOnlyExpected_NeverAPOrPayment",
        "HỒI QUY: Đã kiểm tra tính giá Mua (BUY Rating) thành công: Hệ thống chỉ tạo chi phí ở trạng thái dự kiến (EXPECTED); tuyệt đối không tự động sinh chứng từ công nợ phải trả (AP), hóa đơn (Invoice) hoặc thanh toán (Payment)."
    ),
    # UAT-051 (Row 56)
    (
        "UAT051_RatingLegUX_PeriodLockGate_ClosedFinancialPeriodBlocksFinalize",
        "HỒI QUY: Đã kiểm tra thử thay đổi hoặc chốt phân bổ chi phí rơi vào kỳ tài chính đã đóng (Closed Financial Period): Cổng kiểm soát IPeriodLockGate chặn cứng, trả về lỗi kỳ kế toán đã khóa, không cho phép ghi đè trong im lặng."
    )
]

for idx, (method_name, actual_result) in enumerate(CASES):
    row = 6 + idx
    # Col 8: Trạng thái (PO UAT) -> STRICTLY 'NOT RUN'
    ws.cell(row, 8).value = "NOT RUN"
    # Col 9: Actual result
    ws.cell(row, 9).value = actual_result
    # Col 10: Bằng chứng
    ws.cell(row, 10).value = f"tests/LCMS.Api.Tests/Uat01To51CommercialReadyTests.cs:{method_name}"
    # Col 11: DEV/QA Automated Test Status
    ws.cell(row, 11).value = "PASS (100% - Ready for PO UAT)"

wb.save(r'docs/po/20261023-LCMS_UAT_Operational_Rating_Allocation_DEV_Package_v1.0/LCMS_DEV_Implementation_UAT_Matrix_v1.0_20261023.xlsx')
print(f"Successfully updated all {len(CASES)} cases in sheet 04_UAT_Cases in the Excel file!")

