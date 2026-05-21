describe('00 Preflight and seeded Cypress DB', () => {
  let seed;

  before(() => {
    cy.resetTestData().then((body) => {
      seed = body;
    });
  });

  it('resets and seeds an isolated Cypress database', () => {
    expect(seed.firstAvailableCarId).to.be.greaterThan(0);
    expect(seed.pendingOwnerCarId).to.be.greaterThan(0);
    expect(seed.bookingId).to.be.greaterThan(0);
    expect(seed.areaId).to.be.greaterThan(0);
    expect(seed.users.admin.email).to.eq('admin@admin.com');
    expect(seed.users.customer.email).to.eq('customer@cypress.local');
  });

  it('reports Cypress sandbox configuration explicitly', () => {
    cy.assertSandboxConfigured({
      payos: Cypress.env('usePayOSSandbox') === true,
      fpt: Cypress.env('useFptSandbox') === true,
      ready: Cypress.env('usePayOSSandbox') === true || Cypress.env('useFptSandbox') === true,
    });
  });

  it('serves liveness and database health', () => {
    cy.request('/live').its('status').should('eq', 200);
    cy.request('/health').its('status').should('eq', 200);
  });

  it('loads public routes without 500/runtime failures', () => {
    [
      '/',
      '/Car',
      '/Home/Privacy',
      '/Home/Terms',
      '/LongTerm',
      '/Membership',
      '/Blog',
      '/Guide?role=customer',
      '/Guide?role=admin',
    ].forEach((path) => {
      cy.request({ url: path, failOnStatusCode: false }).then((response) => {
        expect(response.status, path).to.be.lessThan(500);
        expect(response.body, path).not.to.match(/Invalid column name|Invalid object name|An unhandled exception occurred/i);
      });
    });
  });
});
