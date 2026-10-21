import glob
import re
import os

endpoints = glob.glob('src/LCMS.Api/Endpoints/*.cs')
print(f"Total endpoint files: {len(endpoints)}")

for ep in sorted(endpoints):
    fname = os.path.basename(ep)
    with open(ep, 'r', encoding='utf-8') as f:
        content = f.read()
    
    # Map routes
    routes = re.findall(r'(\w+)\.Map(Get|Post|Put|Patch|Delete)\s*\(\s*["\']([^"\']*)["\']', content)
    # Also find top-level group routes
    groups = re.findall(r'app\.MapGroup\s*\(\s*["\']([^"\']*)["\']', content)
    
    print(f"\n=== {fname} ===")
    print("Groups:", groups)
    for g_var, verb, path in routes:
        print(f"  {verb} {path} (on {g_var})")

