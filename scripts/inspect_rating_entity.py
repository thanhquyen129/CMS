import sys
sys.stdout.reconfigure(encoding='utf-8')

with open('src/LCMS.Domain/Entities/Rating.cs', 'r', encoding='utf-8') as f:
    print("=== Rating.cs ===")
    print(f.read())

