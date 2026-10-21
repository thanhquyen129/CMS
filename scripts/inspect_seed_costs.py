import sys
sys.stdout.reconfigure(encoding='utf-8')

with open('src/LCMS.Application/Costs/Commands/SeedExpectedCostsFromRatingCommand.cs', 'r', encoding='utf-8') as f:
    print("=== SeedExpectedCostsFromRatingCommand.cs ===")
    print(f.read()[:2000])

with open('src/LCMS.Application/Revenues/Commands/SeedExpectedRevenuesFromRatingCommand.cs', 'r', encoding='utf-8') as f:
    print("\n=== SeedExpectedRevenuesFromRatingCommand.cs ===")
    print(f.read()[:2000])

