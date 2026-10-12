/**
 * Enterprise SSL Pinning & Transport Security Configuration for LCMS Mobile
 * Target Domain: cms.lsslogistics.vn
 * 
 * Implements SPKI (Subject Public Key Info) SHA-256 pinning with 1 active Primary Pin
 * and 1 pre-configured Disaster Recovery Backup Pin for seamless zero-downtime certificate rotation.
 */

export interface SslPinConfig {
  domain: string;
  includeSubdomains: boolean;
  expirationDate: string;
  pins: {
    type: 'primary' | 'backup';
    digestAlgorithm: 'SHA-256';
    spkiBase64: string;
    description: string;
  }[];
}

export class SslSecurityException extends Error {
  public readonly code: string;

  constructor(message: string, code: string = 'SSL_SECURITY_VIOLATION') {
    super(message);
    this.name = 'SslSecurityException';
    this.code = code;
  }
}

/**
 * Standard SPKI SHA-256 Public Key Pins for cms.lsslogistics.vn
 */
export const LCMS_SSL_PIN_CONFIG: SslPinConfig = {
  domain: 'cms.lsslogistics.vn',
  includeSubdomains: true,
  expirationDate: '2027-12-31',
  pins: [
    {
      type: 'primary',
      digestAlgorithm: 'SHA-256',
      spkiBase64: '8BxW0v+5M3BMDDba/TBK2BoM+iddfYrAs/fknGgwS98=',
      description: 'Primary Production Leaf SPKI Pin (cms.lsslogistics.vn)',
    },
    {
      type: 'backup',
      digestAlgorithm: 'SHA-256',
      spkiBase64: 'brzvtCELCIZUo4sD/qPX0ccRtPsd3DY6RfmxpOU9oB4=',
      description: 'Intermediate CA SPKI Pin (Let\'s Encrypt YE1)',
    },
    {
      type: 'backup',
      digestAlgorithm: 'SHA-256',
      spkiBase64: 'sCkq5UWXjg+7mKu9lMhhYF5bGLsy7VI/UNW3tccdR7w=',
      description: 'Root CA SPKI Pin (ISRG Root YE)',
    },
    {
      type: 'backup',
      digestAlgorithm: 'SHA-256',
      spkiBase64: 'C5+lpZ7tcVwmwQIMcRtPbsQtWLABXhQzejna0wHFr8M=',
      description: 'Disaster Recovery Root SPKI Pin (ISRG Root X1)',
    },
  ],
};

/**
 * Validates request URL against enterprise SSL transport policies:
 * 1. Forbids unencrypted HTTP cleartext traffic in production.
 * 2. Enforces pinned domain rules for all financial endpoints.
 */
export function validateTransportSecurity(url: string, isProduction: boolean = true): void {
  const trimmed = url.trim();

  if (isProduction && trimmed.startsWith('http://')) {
    throw new SslSecurityException(
      'Giao thức HTTP không mã hóa bị cấm trong môi trường Production thương mại. Bắt buộc sử dụng HTTPS.',
      'CLEARTEXT_TRAFFIC_FORBIDDEN'
    );
  }

  try {
    const parsed = new URL(trimmed);
    const hostname = parsed.hostname.toLowerCase();

    if (hostname.endsWith('lsslogistics.vn')) {
      if (hostname !== LCMS_SSL_PIN_CONFIG.domain && !hostname.endsWith(`.${LCMS_SSL_PIN_CONFIG.domain}`)) {
        throw new SslSecurityException(
          `Tên miền ${hostname} không nằm trong danh sách SSL Pinning được chứng thực (${LCMS_SSL_PIN_CONFIG.domain}).`,
          'DOMAIN_PIN_MISMATCH'
        );
      }
    }
  } catch (err) {
    if (err instanceof SslSecurityException) throw err;
    throw new SslSecurityException(`Định dạng URL không hợp lệ: ${url}`, 'MALFORMED_URL');
  }
}

/**
 * Returns formatted native Network Security Config XML for Android
 */
export function generateAndroidNetworkSecurityConfigXml(): string {
  const pinXml = LCMS_SSL_PIN_CONFIG.pins
    .map((p) => `            <!-- ${p.description} -->\n            <pin digest="SHA-256">${p.spkiBase64}</pin>`)
    .join('\n');

  return `<?xml version="1.0" encoding="utf-8"?>
<!-- Enterprise Network Security Config for LCMS Mobile -->
<network-security-config>
    <domain-config>
        <domain includeSubdomains="${LCMS_SSL_PIN_CONFIG.includeSubdomains}">${LCMS_SSL_PIN_CONFIG.domain}</domain>
        <pin-set expiration="${LCMS_SSL_PIN_CONFIG.expirationDate}">
${pinXml}
        </pin-set>
    </domain-config>
    <!-- Cấm hoàn toàn Cleartext HTTP traffic trong môi trường thương mại -->
    <base-config cleartextTrafficPermitted="false" />
</network-security-config>
`;
}

/**
 * Returns formatted NSPinnedDomains configuration dictionary for iOS Info.plist
 */
export function generateIosPinnedDomainsDictionary(): Record<string, any> {
  return {
    [LCMS_SSL_PIN_CONFIG.domain]: {
      NSIncludesSubdomains: LCMS_SSL_PIN_CONFIG.includeSubdomains,
      NSPinnedLeafIdentities: LCMS_SSL_PIN_CONFIG.pins.map((p) => ({
        'SPKI-SHA256-BASE64': p.spkiBase64,
      })),
    },
  };
}
