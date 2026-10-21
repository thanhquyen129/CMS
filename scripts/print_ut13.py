import sys
sys.stdout.reconfigure(encoding='utf-8')
with open('C:/Users/nguyenthanhbinh/.gemini/antigravity/brain/f43522e3-14cd-40ed-a550-be3aba2f0039/.system_generated/tasks/task-1508.log', 'r', encoding='utf-8', errors='ignore') as f:
    text = f.read()

pos = text.find('UT13_LegToMovement_DifferentPhysicalModes')
if pos != -1:
    chunk = text[pos:pos+1200]
    for line in chunk.splitlines()[:25]:
        print(line)

