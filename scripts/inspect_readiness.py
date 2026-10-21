import sys
sys.stdout.reconfigure(encoding='utf-8')

with open('src/LCMS.Application/Ratings/RatingReadiness.cs', 'r', encoding='utf-8') as f:
    text = f.read()

lines = text.split('\n')
for i in range(90, min(260, len(lines))):
    print(f"{i+1}: {lines[i]}")

