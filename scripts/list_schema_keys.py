import json

with open('scripts/db_schema_parsed.json', 'r', encoding='utf-8') as f:
    schema = json.load(f)

for k in sorted(schema.keys()):
    cols = [p['column_name'] for p in schema[k]['properties'].values()]
    table_name = schema[k]['entity'].split('.')[-1]
    print(f"{k} ({len(cols)} cols): {cols[:6]}...")

