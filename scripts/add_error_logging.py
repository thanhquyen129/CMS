with open('tests/LCMS.Api.Tests/InputOptimizationInheritanceTests.cs', 'r', encoding='utf-8') as f:
    text = f.read()

text = text.replace('Assert.Equal(HttpStatusCode.OK, prefillRes.StatusCode);', 'if (prefillRes.StatusCode != HttpStatusCode.OK) { var err = await prefillRes.Content.ReadAsStringAsync(); throw new Exception($"HTTP {prefillRes.StatusCode}: {err}"); }')
text = text.replace('Assert.Equal(HttpStatusCode.OK, rollupRes.StatusCode);', 'if (rollupRes.StatusCode != HttpStatusCode.OK) { var err = await rollupRes.Content.ReadAsStringAsync(); throw new Exception($"HTTP {rollupRes.StatusCode}: {err}"); }')

with open('tests/LCMS.Api.Tests/InputOptimizationInheritanceTests.cs', 'w', encoding='utf-8', newline='\r\n') as f:
    f.write(text)
print('Updated tests to print error message')

