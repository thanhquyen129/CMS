import sys
sys.stdout.reconfigure(encoding='utf-8')

with open('src/LCMS.Domain/Entities/FinancialClose.cs', 'r', encoding='utf-8') as f:
    print("=== FinancialClose.cs ===")
    print(f.read())

with open('src/LCMS.Domain/Entities/FinancialCloseSnapshot.cs', 'r', encoding='utf-8') as f:
    print("\n=== FinancialCloseSnapshot.cs ===")
    print(f.read())

