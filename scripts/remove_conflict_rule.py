import sqlite3

con = sqlite3.connect('src/LCMS.Api/lcms_dev.db')
cur = con.cursor()
cur.execute("DELETE FROM pricing_rule_components WHERE pricing_rule_id IN (SELECT id FROM pricing_rules WHERE code = 'QT0002')")
cur.execute("DELETE FROM pricing_rules WHERE code = 'QT0002'")
con.commit()
con.close()
print('Successfully removed conflicting rule QT0002')

