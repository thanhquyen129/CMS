import os
import re
import sys
import glob

sys.stdout.reconfigure(encoding='utf-8')

def inspect_file(path):
    if not os.path.exists(path):
        return f"File {path} does not exist."
    with open(path, 'r', encoding='utf-8') as f:
        content = f.read()
    
    # Extract labels
    labels = re.findall(r'<label[^>]*>(.*?)</label>', content, re.DOTALL)
    clean_labels = [re.sub(r'<[^>]+>', '', l).strip() for l in labels if re.sub(r'<[^>]+>', '', l).strip()]
    
    # Extract state variables (useState)
    states = re.findall(r'const\s+\[([a-zA-Z0-9_]+),\s*set[a-zA-Z0-9_]+\]\s*=\s*useState', content)
    
    # Extract fetch endpoints
    fetch_calls = re.findall(r'fetch\((?:\"|\'|`)([^\"\'`]+)(?:\"|\'|`)', content)
    
    # Extract payload object structures (e.g. JSON.stringify({...}))
    stringify_matches = re.findall(r'JSON\.stringify\(\s*\{([^}]+)\}\s*\)', content, re.DOTALL)
    
    return {
        'labels': clean_labels,
        'states': states,
        'fetches': list(set(fetch_calls)),
        'stringify': [s.strip().replace('\n', ' ') for s in stringify_matches]
    }

operational_files = [
    'CreateOrderForm.tsx', 'CreateBillForm.tsx', 'CreateBillWorkspaceForm.tsx',
    'WaybillCaptureForm.tsx', 'BillPartyPolicyForm.tsx', 'CreateShipmentForm.tsx',
    'QuickCreateShipmentDialog.tsx', 'CreateLegForm.tsx', 'CreateMovementForm.tsx',
    'OperationalReferenceEditor.tsx', 'ManualReferenceForm.tsx', 'CreateCargoFields.tsx',
    'CreateCargoLineGrid.tsx'
]

print("=== OPERATIONAL FORMS ===")
for fname in operational_files:
    p = os.path.join('apps/web/components', fname)
    info = inspect_file(p)
    print(f"\n--- {fname} ---")
    print("Labels:", info.get('labels', []))
    print("States:", info.get('states', []))
    print("Fetches:", info.get('fetches', []))
    if info.get('stringify'):
        print("Payloads:", info.get('stringify')[:2])

