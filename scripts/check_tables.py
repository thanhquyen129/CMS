import json

with open('scripts/db_schema_parsed.json', 'r', encoding='utf-8') as f:
    schema = json.load(f)

print(f"Total tables in schema: {len(schema)}")
tables = ['orders', 'bills', 'shipments', 'transport_legs', 'transport_movements', 
          'operational_measurements', 'rate_cards', 'rate_versions', 'pricing_rules', 
          'pricing_rule_components', 'rate_breaks', 'container_rates', 'surcharges', 
          'surcharge_versions', 'surcharge_rules', 'ratings', 'rating_details', 'costs', 
          'cost_adjustments', 'cost_allocations', 'revenues', 'revenue_adjustments', 
          'revenue_mappings', 'financial_documents', 'financial_document_lines', 
          'document_matches', 'document_match_details', 'payable_exposures', 
          'receivable_exposures', 'accounts_payable', 'accounts_receivable', 'payments', 
          'payment_allocations', 'collections', 'collection_allocations', 'bank_feed_lines', 
          'reconciliations', 'financial_closes', 'business_parties', 'party_roles', 
          'party_bank_accounts', 'party_contacts', 'locations', 'routes', 'commodity_types', 
          'currencies', 'fx_rates', 'economic_charge_types', 'tenant_settings']

for t in tables:
    if t in schema:
        cols = [p['column_name'] for p in schema[t]['properties'].values()]
        print(f"{t}: {len(cols)} columns")
    else:
        print(f"MISSING: {t}")

