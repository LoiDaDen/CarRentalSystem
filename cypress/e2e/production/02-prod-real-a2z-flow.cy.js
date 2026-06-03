const describeProd = Cypress.env('productionMode') === true ? describe : describe.skip;
const tc = (ids, depth = 'business-flow') => `${Cypress.env('productionMode') === true ? ids.map((id) => `[${id}]`).join('') : ''}[DEPTH:${depth}]`;

const tinyPng = 'iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAFgwJ/l6m2VwAAAABJRU5ErkJggg==';

const parseVndAmounts = (text) => [...String(text || '').matchAll(/([\d.,]+)\s*(?:d|đ|VND)/gi)]
  .map((match) => Number(match[1].replace(/[.,]/g, '')))
  .filter((amount) => Number.isFinite(amount) && amount > 0);

describeProd('Production A-Z real business flows', () => {
  beforeEach(() => {
    cy.requireProductionMode();
  });

  it(`${tc(['TC223', 'TC224', 'TC229', 'TC230'])} updates owner digital-signature evidence profile on the dedicated test owner`, () => {
    const validUntil = new Date();
    validUntil.setFullYear(validUntil.getFullYear() + 1);

    cy.loginAs('owner');
    cy.safeVisit('/OwnerCars/Contracts');
    cy.productionDataName('owner-signature-profile').then((profileName) => {
      cy.get('input[name="ProviderName"]').clear().type(`Cypress CA ${profileName}`);
      cy.get('input[name="CertificateSerialNumber"]').clear().type(profileName.slice(0, 60));
      cy.get('input[name="CertificateSubject"]').clear().type(`Cypress Owner ${profileName}`);
      cy.get('input[name="CertificateValidUntil"]').clear().type(validUntil.toISOString().slice(0, 10));
      cy.get('input[name="signatureImage"]').selectFile({
        contents: Cypress.Buffer.from(tinyPng, 'base64'),
        fileName: `${profileName}.png`,
        mimeType: 'image/png',
      }, { force: true });
      cy.get('form.owner-signature-form').submit();
      cy.assertNoFatalPageError();
      cy.registerProductionArtifact('owner', 'signature-profile-upsert', {
        providerName: `Cypress CA ${profileName}`,
        status: 'submitted',
      });
    });
  });

  it(`${tc(['TC106', 'TC107', 'TC109', 'TC110', 'TC148', 'TC149', 'TC151', 'TC152', 'TC169', 'TC170'], 'external-real')} creates a real booking hold, opens PayOS production payment, and waits for paid confirmation`, () => {
    const targetAmount = Number(Cypress.env('productionPaymentAmountVnd') || 10000);

    cy.loginAs('customer');
    cy.openProductionBookingCheckout().then((bookingDraft) => {
      cy.productionDataName('booking-note').then((note) => {
        cy.get('input[name="FullName"]').clear().type(`Cypress Customer ${Cypress.env('productionRunId')}`);
        cy.get('input[name="Phone"]').clear().type('0900000000');
        cy.get('textarea[name="Notes"]').clear().type(note);
        cy.get('input[name="SelfDriveAgreement"]').check({ force: true });
        cy.get('input[name="LegalAgreement"]').check({ force: true });
        cy.get('form#checkoutForm').submit();
        cy.assertNoFatalPageError();

        cy.location('pathname', { timeout: 30000 }).should('match', /\/Payment\/Hold|\/Payment\/Success/i);
        cy.location('search').then((search) => {
          const orderCode = new URLSearchParams(search).get('orderCode');
          expect(orderCode, 'PayOS order code').to.match(/^\d+$/);

          cy.get('body').invoke('text').then((text) => {
            const amounts = parseVndAmounts(text);
            expect(amounts, `pending page should contain target micro-payment ${targetAmount} VND`).to.include(targetAmount);
          });

          cy.get('a[href^="http"]').first().invoke('attr', 'href').then((checkoutUrl) => {
            cy.registerProductionArtifact('payment', 'payos-production-checkout-created', {
              orderCode,
              amountVnd: targetAmount,
              checkoutUrl,
              bookingDraft,
              status: 'waiting-for-manual-payment',
            });
            cy.log(`Pay the production micro-payment now, then Cypress will poll: ${checkoutUrl}`);
          });

          if (Cypress.env('productionWaitForPayment') === true) {
            cy.waitForProductionPayment(orderCode).then((paid) => {
              cy.registerProductionArtifact('payment', 'payos-production-paid', {
                orderCode,
                status: paid.status,
                location: paid.location,
              });
              cy.safeVisit(paid.location || `/Payment/Hold?orderCode=${orderCode}`);
              cy.assertNoFatalPageError();
              cy.get('body').then(($body) => {
                const detailsLink = $body.find('a[href*="/Booking/Details"]').first();
                if (!detailsLink.length) return;
                cy.wrap(detailsLink).invoke('attr', 'href').then((href) => {
                  cy.safeVisit(href);
                  cy.assertNoFatalPageError();
                });
              });
            });
          }
        });
      });
    });
  });

  it(`${tc(['TC127', 'TC128', 'TC130', 'TC131', 'TC274', 'TC275', 'TC277', 'TC278'])} exercises cart, customer self-service, support, and privacy requests`, () => {
    cy.loginAs('customer');
    cy.clearCart();
    cy.safeVisit('/Cart');
    cy.requestHealthy('/SelfService/Summary');

    cy.productionDataName('support-ticket').then((subject) => {
      cy.safeVisit('/Auth/Profile');
      cy.postForm('/Privacy/RequestExport', { reason: subject }).then((response) => {
        expect(response.status).to.be.oneOf([200, 302]);
      });
      cy.postForm('/Support/Create', {
        ticketType: 'Booking',
        priority: 'P2',
        subject,
        description: `Cypress production support request ${subject}`,
      }).then((response) => {
        expect(response.status).to.be.oneOf([200, 302]);
        cy.registerProductionArtifact('customer', 'support-and-privacy-created', {
          subject,
          privacyRequestStatus: response.status,
        });
      });
    });
  });

  it(`${tc(['TC379', 'TC380', 'TC400', 'TC401', 'TC421', 'TC422', 'TC442', 'TC443', 'TC505', 'TC506'])} verifies admin operational surfaces with the admin test account`, () => {
    cy.loginAs('admin');
    [
      '/Admin/Dashboard',
      '/Admin/Booking',
      '/Admin/User',
      '/Admin/Car/Manage',
      '/Admin/Promotions',
      '/Admin/AuditLog',
      '/Admin/Privacy',
    ].forEach((route) => {
      cy.safeVisit(route);
    });
    cy.registerProductionArtifact('admin', 'admin-surfaces-visited', {
      routes: ['Dashboard', 'Booking', 'User', 'Car', 'Promotions', 'AuditLog', 'Privacy'],
    });
  });

  it(`${tc(['TC199', 'TC200', 'TC238', 'TC239', 'TC301', 'TC302', 'TC313', 'TC314'])} covers long-term, membership, self-service, and tracking surfaces`, () => {
    cy.loginAs('customer');
    ['/LongTerm', '/LongTerm/RequestForm', '/Membership', '/Booking/MyBookings', '/Tracking/Map'].forEach((route) => {
      cy.safeVisit(route);
    });

    cy.loginAs('admin');
    cy.request({ url: '/Admin/Tracking/GetLiveLocations', failOnStatusCode: false }).then((response) => {
      expect(response.status).to.be.lessThan(500);
    });
  });
});
