import sys
sys.stdout.reconfigure(encoding='utf-8')

with open('src/LCMS.Application/DocumentMatches/Commands/CreateExposuresFromMatchCommand.cs', 'r', encoding='utf-8') as f:
    print("=== CreateExposuresFromMatchCommand.cs ===")
    print(f.read()[:2000])

with open('apps/web/components/ReceiveDocumentForm.tsx', 'r', encoding='utf-8') as f:
    print("\n=== ReceiveDocumentForm.tsx ===")
    print(f.read()[:2000])

