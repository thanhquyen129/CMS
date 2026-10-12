import { BillListItemDto, CostItemDto } from "@lcms/shared";
import { CostFilterParams } from "../entities/Cost";
import { ICostRepository } from "../repositories/ICostRepository";

export interface GetCostsOutput {
  costs: CostItemDto[];
  bills: BillListItemDto[];
  totalCount: number;
}

export class GetCostsUseCase {
  constructor(private readonly costRepository: ICostRepository) {}

  /**
   * Truy vấn danh sách chi phí và vận đơn liên quan với bộ lọc nghiệp vụ.
   */
  public async execute(filter?: CostFilterParams): Promise<GetCostsOutput> {
    const [rawCosts, bills] = await Promise.all([
      this.costRepository.getCosts(filter),
      this.costRepository.getBills(),
    ]);

    let filtered = [...rawCosts];

    if (filter?.attributionType) {
      filtered = filtered.filter((c) => c.attributionType === filter.attributionType);
    }

    if (filter?.financialMaturity) {
      filtered = filtered.filter((c) => c.financialMaturity === filter.financialMaturity);
    }

    if (filter?.costTypeCode) {
      filtered = filtered.filter((c) => c.costTypeCode === filter.costTypeCode);
    }

    if (filter?.searchQuery && filter.searchQuery.trim() !== "") {
      const q = filter.searchQuery.toLowerCase().trim();
      filtered = filtered.filter(
        (c) =>
          (c.billNo && c.billNo.toLowerCase().includes(q)) ||
          (c.costTypeCode && c.costTypeCode.toLowerCase().includes(q)) ||
          (c.vendorName && c.vendorName.toLowerCase().includes(q))
      );
    }

    // Sắp xếp mặc định: ngày hiệu lực (effectiveDate) mới nhất trước
    filtered.sort((a, b) => {
      const timeA = new Date(a.effectiveDate || 0).getTime();
      const timeB = new Date(b.effectiveDate || 0).getTime();
      return timeB - timeA;
    });

    return {
      costs: filtered,
      bills,
      totalCount: filtered.length,
    };
  }
}
