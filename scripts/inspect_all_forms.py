import os
import re
import sys
import json

sys.stdout.reconfigure(encoding='utf-8')

def analyze_form(file_path):
    with open(file_path, 'r', encoding='utf-8') as f:
        content = f.read()

    # Find labels and associated inputs
    # Regex to find label text and following input/select/textarea
    elements = []
    
    # Simple regexes to find form controls with their name, id, or state binding
    # 1. Inputs: <input ... name="..." ... /> or value={...}
    # 2. Selects: <select ... name="..." ...>
    # 3. Textareas: <textarea ... name="..." ...>
    
    # Let's extract blocks: <label>...</label> followed by input/select or wrapping input/select
    label_pattern = re.compile(r'<label[^>]*>(.*?)</label>', re.DOTALL)
    for m in label_pattern.finditer(content):
        lbl_text = re.sub(r'<[^>]+>', ' ', m.group(1)).strip()
        lbl_text = ' '.join(lbl_text.split())
        if lbl_text:
            elements.append(('label', lbl_text))
            
    # Also find inputs with name="..." or id="..."
    names = re.findall(r'name=["\']([^"\']+)["\']', content)
    
    # Also find endpoints
    endpoints = re.findall(r'(?:fetch|axios|\$api)\([`\'"]([^`\'"]+)[`\'"]', content)
    
    return {
        'labels': elements,
        'names': list(set(names)),
        'endpoints': list(set(endpoints))
    }

form_groups = {
    'Operational': [
        'CreateOrderForm.tsx', 'CreateBillForm.tsx', 'CreateBillWorkspaceForm.tsx',
        'WaybillCaptureForm.tsx', 'BillPartyPolicyForm.tsx', 'CreateShipmentForm.tsx',
        'QuickCreateShipmentDialog.tsx', 'CreateLegForm.tsx', 'CreateMovementForm.tsx',
        'OperationalReferenceEditor.tsx', 'ManualReferenceForm.tsx', 'CreateCargoFields.tsx',
        'CreateCargoLineGrid.tsx'
    ],
    'Pricing_Rating': [
        'RateCardDeclarationForm.tsx', 'ComposeTariffForm.tsx', 'CreateRateVersionForm.tsx',
        'DraftRateVersionVatForm.tsx', 'AddPricingRuleForm.tsx', 'AddPricingRuleComponentForm.tsx',
        'TariffMatrix.tsx', 'SurchargeForm.tsx', 'RateBillForm.tsx', 'RateCalculatorForm.tsx',
        'CompareRatesForm.tsx'
    ],
    'Cost_Revenue_Doc': [
        'CreateCostForm.tsx', 'CreateSharedCostForm.tsx', 'AllocateSharedCostForm.tsx',
        'CreateRevenueForm.tsx', 'MapRevenueForm.tsx', 'ReceiveDocumentForm.tsx',
        'AddDocumentLineForm.tsx', 'EditDocumentLineForm.tsx', 'CorrectDocumentHeaderForm.tsx',
        'StartDocumentMatchForm.tsx', 'AddMatchDetailForm.tsx'
    ],
    'Debt_CashFlow': [
        'CreateExposureForm.tsx', 'RecognizeExposureForm.tsx', 'CreateCashTxnForm.tsx',
        'AllocateCashForm.tsx', 'CreateBankFeedLineForm.tsx', 'ImportBankFeedCsvForm.tsx',
        'StartReconciliationForm.tsx', 'AddReconciliationDetailForm.tsx', 'StartFinancialCloseForm.tsx'
    ],
    'MasterData_Config': [
        'CreateBusinessPartyForm.tsx', 'EditBusinessPartyForm.tsx', 'ImportPartiesForm.tsx',
        'UpsertLocationForm.tsx', 'UpsertRouteForm.tsx', 'UpsertCommodityForm.tsx',
        'CreateCurrencyForm.tsx', 'UpsertFxRateForm.tsx', 'EconomicChargeTypeForm.tsx',
        'UpsertCatalogItemForm.tsx', 'CreateOrganizationForm.tsx', 'CreateUserForm.tsx',
        'SettingsForm.tsx', 'TenantFinancialSettingsForm.tsx', 'CompanyProfileForm.tsx',
        'CompanyLogoForm.tsx', 'NotificationSettingsForm.tsx', 'LicenseModulesForm.tsx'
    ]
}

results = {}
for grp, fnames in form_groups.items():
    results[grp] = {}
    for fname in fnames:
        p = os.path.join('apps/web/components', fname)
        if os.path.exists(p):
            results[grp][fname] = analyze_form(p)
        else:
            results[grp][fname] = 'NOT_FOUND'

print(json.dumps(results, ensure_ascii=False, indent=2))

