# -*- coding: utf-8 -*-
with open('scripts/generate_32_uat_tests.py', 'r', encoding='utf-8') as f:
    text = f.read()

# Fix 1: Candidates -> FieldCandidates
text = text.replace('prefill.Candidates["customerPartyId"]', 'prefill.FieldCandidates!["customerPartyId"]')

# Fix 2: Etd -> EtdAt
text = text.replace('Assert.Null(prefill.Etd);', 'Assert.Null(prefill.EtdAt);')
text = text.replace('Etd = etd1', 'Etd = etd1')
text = text.replace('Etd = etd2', 'Etd = etd2')

# Fix 3: CargoLineIds -> ExternalId starts with SHARED_CARGO_LINE
text = text.replace(
    'ExternalId = "ext-ut04-1",\n            CustomerPartyId = cust,\n            TransportMode = "air",\n            Context = new OperationalContextDocument { PackageCount = 10, GrossWeightKg = 100m, VolumeCbm = 1.0m, CargoLineIds = new List<string> { "SHARED_LINE_01" } },',
    'ExternalId = "SHARED_CARGO_LINE_01",\n            CustomerPartyId = cust,\n            TransportMode = "air",\n            Context = new OperationalContextDocument { PackageCount = 10, GrossWeightKg = 100m, VolumeCbm = 1.0m },'
)
text = text.replace(
    'ExternalId = "ext-ut04-2",\n            CustomerPartyId = cust,\n            TransportMode = "air",\n            Context = new OperationalContextDocument { PackageCount = 10, GrossWeightKg = 100m, VolumeCbm = 1.0m, CargoLineIds = new List<string> { "SHARED_LINE_01" } },',
    'ExternalId = "SHARED_CARGO_LINE_02",\n            CustomerPartyId = cust,\n            TransportMode = "air",\n            Context = new OperationalContextDocument { PackageCount = 10, GrossWeightKg = 100m, VolumeCbm = 1.0m },'
)

# Fix 4: NoticeMessage on ShipmentRollupPreviewDto -> RollupSummary
text = text.replace('Assert.Contains("đa phương thức", rollup.NoticeMessage);', 'Assert.Contains("đa phương thức", rollup.RollupSummary);')

with open('scripts/generate_32_uat_tests.py', 'w', encoding='utf-8') as f:
    f.write(text)

print("Updated generate_32_uat_tests.py successfully")
