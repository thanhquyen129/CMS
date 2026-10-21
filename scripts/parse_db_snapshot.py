import re
import json

with open('src/LCMS.Infrastructure/Persistence/Migrations/LcmsDbContextModelSnapshot.cs', 'r', encoding='utf-8') as f:
    text = f.read()

# Parse entities: modelBuilder.Entity("...", b => { ... });
entity_blocks = re.split(r'modelBuilder\.Entity\(', text)

schema = {}

for block in entity_blocks[1:]:
    entity_name_match = re.match(r'\"([^\"]+)\"', block)
    if not entity_name_match:
        continue
    entity_name = entity_name_match.group(1)
    
    table_match = re.search(r'b\.ToTable\(\"([^\"]+)\"\)', block)
    table_name = table_match.group(1) if table_match else entity_name.split('.')[-1].lower()
    
    properties = {}
    prop_matches = re.finditer(r'b\.Property<([^>]+)>\(\"([^\"]+)\"\)(.*?)(?=b\.Property|b\.HasKey|b\.ToTable|\Z)', block, re.DOTALL)
    for pm in prop_matches:
        clr_type = pm.group(1).strip()
        prop_name = pm.group(2).strip()
        prop_body = pm.group(3)
        
        col_match = re.search(r'HasColumnName\(\"([^\"]+)\"\)', prop_body)
        col_name = col_match.group(1) if col_match else prop_name
        
        type_match = re.search(r'HasColumnType\(\"([^\"]+)\"\)', prop_body)
        col_type = type_match.group(1) if type_match else clr_type
        
        is_nullable = 'IsRequired()' not in prop_body and ('?' in clr_type or 'IsRequired(false)' in prop_body)
        
        properties[prop_name] = {
            'clr_type': clr_type,
            'column_name': col_name,
            'column_type': col_type,
            'nullable': is_nullable
        }
    
    schema[table_name] = {
        'entity': entity_name,
        'properties': properties
    }

print(f"Parsed {len(schema)} tables.")
with open('scripts/db_schema_parsed.json', 'w', encoding='utf-8') as f:
    json.dump(schema, f, indent=2, ensure_ascii=False)
print("Saved to scripts/db_schema_parsed.json")

