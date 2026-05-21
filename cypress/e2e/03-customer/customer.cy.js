describe('03 Customer booking, privacy, support, and reviews', () => {
  let seed;

  const futureIso = (daysFromNow) => {
    const date = new Date();
    date.setDate(date.getDate() + daysFromNow);
    date.setHours(10, 0, 0, 0);
    return date.toISOString().slice(0, 16);
  };

  before(() => {
    cy.resetTestData().then((body) => {
      seed = body;
    });
  });

  it('browses cars, filters/searches, and opens a car detail page', () => {
    cy.safeVisit('/');
    cy.safeVisit('/Car?search=Vios');
    cy.get('a[href*="/Car/Details"]').first().should('exist');
    cy.safeVisit(`/Car/Details/${seed.firstAvailableCarId}?startDate=${futureIso(60)}&endDate=${futureIso(62)}`);
    cy.get('body').invoke('text').should('match', /Vios|Toyota|xe|Car/i);
  });

  it('adds a verified customer cart item and exercises legal checkout guard', () => {
    cy.loginAs('customer');
    cy.clearCart();
    cy.visitFirstListedCarDetails(futureIso(90), futureIso(92));

    cy.intercept('POST', '/Cart/AddToCart').as('addToCart');
    cy.get('#addToCartBtn').click({ force: true });
    cy.wait('@addToCart').its('response.body.success').should('eq', true);

    cy.safeVisit('/Cart');
    cy.get('input[name="SelfDriveAgreement"]').check({ force: true });
    cy.get('input[name="LegalAgreement"]').check({ force: true });
    cy.get('form[action*="CheckoutAll"]').first().find('button[type="submit"]').click({ force: true });
    cy.assertNoFatalPageError();
    cy.url().should('match', /Cart|Booking|Payment|payos/i);
  });

  it('blocks checkout for an unverified customer with the eKYC modal', () => {
    cy.loginAs('customerUnverified');
    cy.clearCart();
    cy.visitFirstListedCarDetails(futureIso(110), futureIso(112));

    cy.intercept('POST', '/Cart/AddToCart').as('addToCart');
    cy.get('#addToCartBtn').click({ force: true });
    cy.wait('@addToCart').its('response.body.success').should('eq', true);

    cy.safeVisit('/Cart');
    cy.get('input[name="SelfDriveAgreement"]').check({ force: true });
    cy.get('input[name="LegalAgreement"]').check({ force: true });
    cy.get('form[action*="CheckoutAll"]').first().find('button[type="submit"]').click({ force: true });
    cy.get('#ekycQuickModal', { timeout: 10000 }).should('be.visible').and('contain.text', 'eKYC');
  });

  it('creates privacy and support requests from authenticated customer context', () => {
    cy.loginAs('customer');
    cy.safeVisit('/Auth/Profile');
    cy.postForm('/Privacy/RequestExport', { reason: 'Cypress export request' }).then((response) => {
      expect(response.status).to.be.oneOf([200, 302]);
    });

    cy.safeVisit('/Auth/Profile');
    cy.postForm('/Support/Create', {
      ticketType: 'Booking',
      priority: 'P2',
      subject: 'Cypress support request',
      description: 'A-Z Cypress support ticket',
      bookingId: seed.bookingId,
    }).then((response) => {
      expect(response.status).to.eq(200);
      expect(response.body.success).to.eq(true);
    });

    cy.request('/Support/Mine').then((response) => {
      expect(response.status).to.eq(200);
      expect(response.body.length).to.be.greaterThan(0);
    });
  });

  it('loads customer self-service and booking history pages', () => {
    cy.loginAs('customer');
    ['/Booking/MyBookings', `/Booking/Details/${seed.bookingId}`].forEach((path) => {
      cy.safeVisit(path);
    });
    cy.requestHealthy('/SelfService/Summary').its('body').should('have.property', 'success', true);
  });
});
