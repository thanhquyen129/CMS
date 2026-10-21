import sys
sys.stdout.reconfigure(encoding='utf-8')

with open('src/LCMS.Application/Ratings/RatingEngine.cs', 'r', encoding='utf-8') as f:
    text = f.read()

print("Length of RatingEngine.cs:", len(text))
lines = text.split('\n')
for i in range(min(150, len(lines))):
    print(lines[i])

