const mojibakeText = /Ã|Â|áº|á»|Ä|Æ|ðŸ|â€/;
const legalPlaceholders = /Use this page to detail your site's privacy policy|FPT\.AI .*Response/i;
const legalMain = (html) => {
  const match = html.match(/<main[^>]*legal-page[^>]*>[\s\S]*?<\/main>/i);
  return match ? match[0] : html;
};

describe('04 eKYC, legal consent, and secure file guards', () => {
  let seed;

  before(() => {
    cy.resetTestData().then((body) => {
      seed = body;
    });
  });

  it('keeps public legal pages readable and free of placeholders/dead legal links', () => {
    ['/Home/Privacy', '/Home/Terms'].forEach((path) => {
      cy.requestHealthy(path).then((response) => {
        const body = legalMain(response.body);
        expect(body, path).not.to.match(legalPlaceholders);
        expect(body, path).not.to.match(mojibakeText);
      });
    });
  });

  it('renders eKYC consent controls and legal links for unverified customers', () => {
    cy.loginAs('customerUnverified');
    cy.safeVisit('/Ekyc');
    cy.get('input[name="EkycPrivacyConsent"]').should('exist').and('have.attr', 'required');
    cy.get('input[name="EkycBiometricConsent"]').should('exist').and('have.attr', 'required');
    cy.get('a[href*="/Home/Privacy"]').should('exist');
  });

  it('rejects eKYC provider calls when consent is missing', () => {
    cy.loginAs('customerUnverified');
    cy.safeVisit('/Ekyc');

    cy.getAntiForgeryToken().then((token) => {
      [
        { url: '/Ekyc/ScanIdCard', body: { EkycPrivacyConsent: false, EkycBiometricConsent: false } },
        { url: '/Ekyc/ScanLicense', body: { EkycPrivacyConsent: false } },
        { url: '/Ekyc/VerifyFace', body: { EkycPrivacyConsent: false, EkycBiometricConsent: false } },
      ].forEach((request) => {
        cy.request({
          method: 'POST',
          url: request.url,
          form: true,
          failOnStatusCode: false,
          headers: { RequestVerificationToken: token },
          body: { ...request.body, __RequestVerificationToken: token },
        }).then((response) => {
          expect(response.status, request.url).to.eq(200);
          expect(response.body.success, request.url).to.eq(false);
          expect(response.body.message, request.url).to.match(/đồng ý|consent|dữ liệu/i);
        });
      });
    });
  });

  it('does not expose secure file paths through path traversal or cross-user guesses', () => {
    cy.loginAs('customer');
    [
      '/SecureFile/GetSecureImage?folder=eKyc&fileName=../../appsettings.json',
      `/SecureFile/GetSecureImage?folder=eKyc&fileName=${seed.users.customerUnverified.userId}/id_front.jpg`,
    ].forEach((path) => {
      cy.request({ url: path, followRedirect: false, failOnStatusCode: false }).then((response) => {
        expect(response.status, path).to.be.oneOf([302, 400, 401, 403, 404]);
        expect(response.headers['content-type'] || '', path).not.to.match(/^image\//);
      });
    });
  });

  it('keeps checkout legal links real on car detail and cart surfaces', () => {
    cy.loginAs('customer');
    cy.safeVisit(`/Car/Details/${seed.firstAvailableCarId}`);
    cy.get('a').contains(/Điều khoản|Terms/i).should('not.have.attr', 'href', '#');
    cy.get('a').contains(/Chính sách|Thông báo|Privacy/i).should('not.have.attr', 'href', '#');

    cy.safeVisit('/Cart');
  });
});
