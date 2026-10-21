import re
import sys
sys.stdout.reconfigure(encoding='utf-8')

with open('apps/web/components/CreateBillWorkspaceForm.tsx', 'r', encoding='utf-8') as f:
    text = f.read()

print("--- CreateBillWorkspaceForm.tsx ---")
for line in text.split('\n'):
    if any(k in line.lower() for k in ['order', 'shipment', 'cargo', 'payload', 'fetch', 'setorders']):
        print(line[:120])

