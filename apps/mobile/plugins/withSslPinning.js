const { withDangerousMod, withInfoPlist, createRunOncePlugin } = require('@expo/config-plugins');
const fs = require('fs');
const path = require('path');

const PINNED_DOMAIN = 'cms.lsslogistics.vn';
const PINS = [
  {
    spki: '8BxW0v+5M3BMDDba/TBK2BoM+iddfYrAs/fknGgwS98=',
    comment: 'Primary Leaf SPKI Pin (cms.lsslogistics.vn)',
  },
  {
    spki: 'brzvtCELCIZUo4sD/qPX0ccRtPsd3DY6RfmxpOU9oB4=',
    comment: 'Intermediate CA SPKI Pin (Let\'s Encrypt YE1)',
  },
  {
    spki: 'sCkq5UWXjg+7mKu9lMhhYF5bGLsy7VI/UNW3tccdR7w=',
    comment: 'Root CA SPKI Pin (ISRG Root YE)',
  },
  {
    spki: 'C5+lpZ7tcVwmwQIMcRtPbsQtWLABXhQzejna0wHFr8M=',
    comment: 'Disaster Recovery Root SPKI Pin (ISRG Root X1)',
  },
];

/**
 * Expo Config Plugin to inject Enterprise SSL Pinning into Android & iOS Native build files.
 */
function withAndroidSslPinning(config) {
  if (process.env.EXPO_PUBLIC_DISABLE_SSL_PINNING === 'true') {
    return config;
  }
  return withDangerousMod(config, [
    'android',
    async (modConfig) => {
      const resXmlDir = path.join(
        modConfig.modRequest.platformProjectRoot,
        'app',
        'src',
        'main',
        'res',
        'xml'
      );

      if (!fs.existsSync(resXmlDir)) {
        fs.mkdirSync(resXmlDir, { recursive: true });
      }

      const xmlPath = path.join(resXmlDir, 'network_security_config.xml');
      const pinItems = PINS.map(
        (p) => `            <!-- ${p.comment} -->\n            <pin digest="SHA-256">${p.spki}</pin>`
      ).join('\n');

      const xmlContent = `<?xml version="1.0" encoding="utf-8"?>
<network-security-config>
    <domain-config>
        <domain includeSubdomains="true">${PINNED_DOMAIN}</domain>
        <pin-set expiration="2027-12-31">
${pinItems}
        </pin-set>
    </domain-config>
    <base-config cleartextTrafficPermitted="false" />
</network-security-config>
`;

      fs.writeFileSync(xmlPath, xmlContent, 'utf-8');

      // Update AndroidManifest.xml networkSecurityConfig attribute
      const manifestPath = path.join(
        modConfig.modRequest.platformProjectRoot,
        'app',
        'src',
        'main',
        'AndroidManifest.xml'
      );

      if (fs.existsSync(manifestPath)) {
        let manifest = fs.readFileSync(manifestPath, 'utf-8');
        if (!manifest.includes('android:networkSecurityConfig')) {
          manifest = manifest.replace(
            '<application',
            '<application\n        android:networkSecurityConfig="@xml/network_security_config"'
          );
          fs.writeFileSync(manifestPath, manifest, 'utf-8');
        }
      }

      return modConfig;
    },
  ]);
}

function withIosSslPinning(config) {
  if (process.env.EXPO_PUBLIC_DISABLE_SSL_PINNING === 'true') {
    return config;
  }
  return withInfoPlist(config, (modConfig) => {
    modConfig.modResults.NSPinnedDomains = {
      [PINNED_DOMAIN]: {
        NSIncludesSubdomains: true,
        NSPinnedLeafIdentities: PINS.map((p) => ({
          'SPKI-SHA256-BASE64': p.spki,
        })),
      },
    };
    return modConfig;
  });
}

function withSslPinning(config) {
  return withIosSslPinning(withAndroidSslPinning(config));
}

module.exports = createRunOncePlugin(withSslPinning, 'withSslPinning', '1.0.0');
