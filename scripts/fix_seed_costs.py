with open('src/LCMS.Application/Costs/Commands/SeedExpectedCostsFromRatingCommand.cs', 'rb') as f:
    raw = f.read()

text = raw.decode('utf-8')
target = """        foreach (var detail in details)
        {
            if (existingBySource.TryGetValue(detail.Id, out var already))
            {
                costIds.Add(already.Id);
                continue;
            }

            var amount = decimal.Round(detail.Amount, 4, MidpointRounding.AwayFromZero);"""

replacement = """        foreach (var detail in details)
        {
            if (existingBySource.TryGetValue(detail.Id, out var already))
            {
                costIds.Add(already.Id);
                continue;
            }

            // UT-26 / DO-10 / GAP-04: Kiểm tra mapping khoản mục kinh tế trên master trước khi seed
            if (detail.EconomicChargeTypeId is Guid ectId)
            {
                var active = await _db.EconomicChargeTypes.AsNoTracking()
                    .AnyAsync(e => e.TenantId == tenantId && e.Id == ectId && e.IsActive, cancellationToken);
                if (!active)
                {
                    throw new ValidationAppException($"Khoản mục kinh tế '{ectId}' cho dòng '{detail.ComponentCode}' không tồn tại hoặc đã bị vô hiệu hóa trên master. Không thể seed chi phí.");
                }
            }

            var amount = decimal.Round(detail.Amount, 4, MidpointRounding.AwayFromZero);"""

if target not in text:
    target = target.replace('\n', '\r\n')
    replacement = replacement.replace('\n', '\r\n')

if target in text:
    text = text.replace(target, replacement)
    with open('src/LCMS.Application/Costs/Commands/SeedExpectedCostsFromRatingCommand.cs', 'wb') as f:
        f.write(text.encode('utf-8'))
    print("Successfully patched SeedExpectedCostsFromRatingCommand.cs")
else:
    print("Target not found!")

