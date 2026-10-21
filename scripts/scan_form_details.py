import os
import re
import sys

sys.stdout.reconfigure(encoding='utf-8')

components_dir = 'apps/web/components'

target_components = [
    'CreateShipmentForm.tsx', 'QuickCreateShipmentDialog.tsx', 'CreateLegForm.tsx', 
    'CreateMovementForm.tsx', 'OperationalReferenceEditor.tsx', 'RateCardDeclarationForm.tsx', 
    'ComposeTariffForm.tsx', 'CreateRateVersionForm.tsx', 'DraftRateVersionVatForm.tsx', 
    'AddPricingRuleForm.tsx', 'AddPricingRuleComponentForm.tsx', 'SurchargeForm.tsx', 
    'RateBillForm.tsx', 'CreateCostForm.tsx', 'CreateSharedCostForm.tsx', 
    'AllocateSharedCostForm.tsx', 'CreateRevenueForm.tsx', 'MapRevenueForm.tsx', 
    'ReceiveDocumentForm.tsx', 'AddDocumentLineForm.tsx', 'CorrectDocumentHeaderForm.tsx', 
    'StartDocumentMatchForm.tsx', 'AddMatchDetailForm.tsx', 'CreateExposureForm.tsx', 
    'RecognizeExposureForm.tsx', 'CreateCashTxnForm.tsx', 'AllocateCashForm.tsx', 
    'CreateBankFeedLineForm.tsx', 'ImportBankFeedCsvForm.tsx', 'StartReconciliationForm.tsx', 
    'StartFinancialCloseForm.tsx', 'CreateBusinessPartyForm.tsx', 'EditBusinessPartyForm.tsx', 
    'UpsertLocationForm.tsx', 'UpsertRouteForm.tsx', 'UpsertCommodityForm.tsx', 
    'CreateCurrencyForm.tsx', 'UpsertFxRateForm.tsx', 'EconomicChargeTypeForm.tsx', 
    'UpsertCatalogItemForm.tsx', 'CreateOrganizationForm.tsx', 'CreateUserForm.tsx', 
    'TenantFinancialSettingsForm.tsx', 'SettingsForm.tsx'
]

for fname in target_components:
    p = os.path.join(components_dir, fname)
    if not os.path.exists(p):
        print(f"MISSING: {fname}")
        continue
    with open(p, 'r', encoding='utf-8') as f:
        content = f.read()
    
    # Extract labels
    labels = re.findall(r'<label[^>]*>(.*?)</label>', content, re.DOTALL)
    clean_labels = [re.sub(r'<[^>]+>', ' ', l).strip() for l in labels]
    clean_labels = [' '.join(l.split()) for l in clean_labels if l.strip()]
    
    # Extract inputs name="..."
    names = re.findall(r'name=["\']([^"\']+)["\']', content)
    
    print(f"\n=== {fname} ===")
    print(f"Labels ({len(clean_labels)}): {clean_labels}")
    print(f"Names ({len(names)}): {list(set(names))}")

