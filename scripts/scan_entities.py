import os
import re

entities_dir = 'src/LCMS.Domain/Entities'
entity_files = [
    'Order.cs', 'Bill.cs', 'Shipment.cs', 'TransportLeg.cs', 'TransportMovement.cs',
    'OperationalCargo.cs', 'RateCard.cs', 'RateVersion.cs', 'PricingRule.cs', 
    'PricingRuleComponent.cs', 'RateBreak.cs', 'Surcharge.cs', 'Rating.cs', 'RatingDetail.cs',
    'Cost.cs', 'CostAdjustment.cs', 'CostAllocation.cs', 'Revenue.cs', 'RevenueAdjustment.cs',
    'RevenueMapping.cs', 'FinancialDocument.cs', 'FinancialDocumentLine.cs', 'DocumentMatch.cs',
    'DocumentMatchDetail.cs', 'PayableExposure.cs', 'ReceivableExposure.cs', 'AccountsPayable.cs',
    'AccountsReceivable.cs', 'Payment.cs', 'PaymentAllocation.cs', 'Collection.cs',
    'CollectionAllocation.cs', 'BankFeedLine.cs', 'Reconciliation.cs', 'FinancialClose.cs',
    'FinancialCloseSnapshot.cs', 'BusinessParty.cs', 'PartyRole.cs', 'PartyBankAccount.cs',
    'PartyContact.cs', 'Location.cs', 'RouteMaster.cs', 'CommodityType.cs', 'Currency.cs',
    'FxRate.cs', 'EconomicChargeType.cs', 'TenantSetting.cs'
]

results = {}
for fname in entity_files:
    p = os.path.join(entities_dir, fname)
    if not os.path.exists(p):
        print(f"MISSING: {fname}")
        continue
    with open(p, 'r', encoding='utf-8') as f:
        content = f.read()
    props = re.findall(r'public\s+([a-zA-Z0-9_<>?]+)\s+([a-zA-Z0-9_]+)\s*\{\s*get;\s*set;\s*\}', content)
    results[fname] = props
    print(f"{fname}: {len(props)} properties")

print("Done scanning entity files.")

