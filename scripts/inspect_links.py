import glob, os, sys
sys.stdout.reconfigure(encoding='utf-8')

for f in glob.glob('src/LCMS.Application/OperationalLinks/**/*.cs', recursive=True):
    print("=== File:", f)
    with open(f, 'r', encoding='utf-8') as fp:
        print(fp.read()[:1000])

