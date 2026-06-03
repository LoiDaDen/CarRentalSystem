const describeProd = Cypress.env('productionMode') === true ? describe : describe.skip;
const tc = (ids, depth = 'business-flow') => `${Cypress.env('productionMode') === true ? ids.map((id) => `[${id}]`).join('') : ''}[DEPTH:${depth}]`;

describeProd('Production preflight and account readiness', () => {
  beforeEach(() => {
    cy.requireProductionMode();
  });

  it(`${tc(['TC004', 'TC005'], 'ui-real')} loads the real production app without local reset hooks`, () => {
    cy.request({ url: '/live', failOnStatusCode: false }).its('status').should('be.lessThan', 500);
    cy.request({ url: '/health', failOnStatusCode: false }).its('status').should('be.lessThan', 500);
    cy.safeVisit('/');
    cy.assertNoFatalPageError();
    cy.registerProductionArtifact('preflight', 'production-base-url', {
      baseUrl: Cypress.config('baseUrl'),
    });
  });

  it(`${tc(['TC022', 'TC023', 'TC025', 'TC026', 'TC028', 'TC031', 'TC034', 'TC037'], 'ui-real')} logs in every dedicated production test role`, () => {
    const roles = ['admin', 'manager', 'staff', 'viewer', 'customer', 'customerUnverified', 'owner'];
    cy.wrap(roles).each((roleKey) => {
      cy.loginAs(roleKey);
      cy.getCookie('CarRental.Auth').should('exist');
      cy.registerProductionArtifact('account', `login-${roleKey}`, { roleKey });
      cy.logout();
    });
  });

  it(`${tc(['TC463', 'TC464', 'TC466', 'TC467'], 'ui-real')} blocks customer access to admin-only surfaces`, () => {
    cy.loginAs('customer');
    ['/Admin/Dashboard', '/Admin/Booking', '/Admin/User', '/Admin/Car/Manage'].forEach((route) => {
      cy.request({ url: route, followRedirect: false, failOnStatusCode: false }).then((response) => {
        expect(response.status, route).to.be.oneOf([302, 401, 403, 404]);
      });
    });
  });
});
