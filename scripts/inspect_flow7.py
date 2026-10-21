import sys
sys.stdout.reconfigure(encoding='utf-8')

with open('apps/web/components/SurchargeForm.tsx', 'r', encoding='utf-8') as f:
    print("=== SurchargeForm.tsx ===")
    print(f.read()[:2000])

