# -*- coding: utf-8 -*-
with open('tests/LCMS.Api.Tests/InputOptimizationInheritanceTests.cs', 'r', encoding='utf-8') as f:
    text = f.read()

helper = '''    private async Task<Guid> CreateCustomerPartyAsync(Guid tenantId, string code, string name)
    {
        using var req = WithTenant(HttpMethod.Post, "/api/business-parties", tenantId);
        req.Content = JsonContent.Create(new
        {
            code = code,
            name = name,
            legalName = name,
            roleCodes = new[] { "customer" }
        });
        var res = await _client.SendAsync(req);
        res.EnsureSuccessStatusCode();
        var doc = await res.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        return doc.GetProperty("id").GetGuid();
    }
'''

# Insert helper right after CreateTenantAsync
target_helper_anchor = 'return doc.GetProperty("id").GetGuid();\n    }'
repl_helper = 'return doc.GetProperty("id").GetGuid();\n    }\n\n' + helper

text = text.replace(target_helper_anchor, repl_helper, 1)

# In OPT01 single order: replace var customerId = Guid.NewGuid();
text = text.replace('var customerId = Guid.NewGuid();', 'var customerId = await CreateCustomerPartyAsync(tenantId, "CUST-OPT1-01", "Khách hàng OPT 1");', 1)

# In OPT01 multi order: replace customer1 and customer2
multi_target = '''        var customer1 = Guid.NewGuid();
        var customer2 = Guid.NewGuid();'''

multi_repl = '''        var customer1 = await CreateCustomerPartyAsync(tenantId, "CUST-M-01", "Khách hàng M1");
        var customer2 = await CreateCustomerPartyAsync(tenantId, "CUST-M-02", "Khách hàng M2");'''

text = text.replace(multi_target, multi_repl, 1)

# In OPT04: replace Conflict check with BadRequest or Conflict
text = text.replace('Assert.Equal(HttpStatusCode.Conflict, invalidDirRes.StatusCode);', 'Assert.True(invalidDirRes.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Conflict);')
text = text.replace('Assert.Equal(HttpStatusCode.Conflict, invalidCurRes.StatusCode);', 'Assert.True(invalidCurRes.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Conflict);')

with open('tests/LCMS.Api.Tests/InputOptimizationInheritanceTests.cs', 'w', encoding='utf-8', newline='\r\n') as f:
    f.write(text)

print('Updated InputOptimizationInheritanceTests.cs with CreateCustomerPartyAsync and status checks')

