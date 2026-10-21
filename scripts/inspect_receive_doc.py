import sys
sys.stdout.reconfigure(encoding='utf-8')

with open('apps/web/components/ReceiveDocumentForm.tsx', 'r', encoding='utf-8') as f:
    text = f.read()

lines = text.split('\n')
for i in range(40, min(150, len(lines))):
    print(lines[i])

