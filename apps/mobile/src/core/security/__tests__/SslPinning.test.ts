import {
  LCMS_SSL_PIN_CONFIG,
  validateTransportSecurity,
  SslSecurityException,
  generateAndroidNetworkSecurityConfigXml,
  generateIosPinnedDomainsDictionary,
} from '../SslPinning';

describe('SslPinning Security Suite', () => {
  it('contains primary and backup pins for cms.lsslogistics.vn', () => {
    expect(LCMS_SSL_PIN_CONFIG.domain).toBe('cms.lsslogistics.vn');
    expect(LCMS_SSL_PIN_CONFIG.pins.length).toBeGreaterThanOrEqual(2);

    const primary = LCMS_SSL_PIN_CONFIG.pins.find((p) => p.type === 'primary');
    const backup = LCMS_SSL_PIN_CONFIG.pins.find((p) => p.type === 'backup');

    expect(primary).toBeDefined();
    expect(backup).toBeDefined();
    expect(primary?.spkiBase64).toBeTruthy();
    expect(backup?.spkiBase64).toBeTruthy();
    expect(primary?.spkiBase64).not.toBe(backup?.spkiBase64);
  });

  it('rejects cleartext HTTP in production mode', () => {
    expect(() => {
      validateTransportSecurity('http://cms.lsslogistics.vn/api/costs', true);
    }).toThrow(SslSecurityException);
  });

  it('permits valid HTTPS request to pinned domain', () => {
    expect(() => {
      validateTransportSecurity('https://cms.lsslogistics.vn/api/costs', true);
    }).not.toThrow();
  });

  it('rejects rogue domain spoofing under lsslogistics.vn in production', () => {
    expect(() => {
      validateTransportSecurity('https://attacker.lsslogistics.vn/api/costs', true);
    }).toThrow(SslSecurityException);
  });

  it('generates valid Android network security config XML', () => {
    const xml = generateAndroidNetworkSecurityConfigXml();
    expect(xml).toContain('<domain includeSubdomains="true">cms.lsslogistics.vn</domain>');
    expect(xml).toContain('cleartextTrafficPermitted="false"');
    expect(xml).toContain(LCMS_SSL_PIN_CONFIG.pins[0].spkiBase64);
    expect(xml).toContain(LCMS_SSL_PIN_CONFIG.pins[1].spkiBase64);
  });

  it('generates valid iOS pinned domains dictionary', () => {
    const dict = generateIosPinnedDomainsDictionary();
    expect(dict['cms.lsslogistics.vn']).toBeDefined();
    expect(dict['cms.lsslogistics.vn'].NSPinnedLeafIdentities.length).toBeGreaterThanOrEqual(2);
  });
});
