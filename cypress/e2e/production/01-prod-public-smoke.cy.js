const describeProd = Cypress.env('productionMode') === true ? describe : describe.skip;
const tc = (ids, depth = 'ui-real') => `${Cypress.env('productionMode') === true ? ids.map((id) => `[${id}]`).join('') : ''}[DEPTH:${depth}]`;

const publicRoutes = [
  '/',
  '/Car',
  '/Home/Privacy',
  '/Home/Terms',
  '/LongTerm',
  '/Membership',
  '/Blog',
  '/Guide?role=customer',
  '/Guide?role=owner',
  '/Faq',
];

describeProd('Production public smoke, catalog, content, and responsive checks', () => {
  beforeEach(() => {
    cy.requireProductionMode();
  });

  it(`${tc(['TC004', 'TC005', 'TC064', 'TC085', 'TC262', 'TC274', 'TC505', 'TC487'])} loads public routes as real pages`, () => {
    cy.wrap(publicRoutes).each((route) => {
      cy.requestHealthy(route);
      cy.safeVisit(route);
      cy.assertNoFatalPageError();
    });
  });

  it(`${tc(['TC094', 'TC095', 'TC097', 'TC098', 'TC100', 'TC101'])} browses catalog and opens a real car detail page`, () => {
    cy.safeVisit('/Car');
    cy.get('a[href*="/Car/Details"]', { timeout: 15000 }).first().should('exist').invoke('attr', 'href').then((href) => {
      cy.safeVisit(href);
      cy.get('form#bookingForm').should('exist');
      cy.get('input[name="CarID"]').invoke('val').should('match', /^\d+$/);
    });
  });

  it(`${tc(['TC487', 'TC488', 'TC496', 'TC497'])} keeps core production pages responsive without fatal console errors`, () => {
    ['/', '/Car', '/Home/Privacy'].forEach((route) => {
      cy.assertResponsiveBaseline(route);
      cy.assertNoSeriousConsoleErrors(route);
    });
  });

  it(`${tc(['TC316', 'TC317'], 'external-real')} calls production AI/API surfaces without provider mocks`, () => {
    cy.request({
      method: 'POST',
      url: '/api/ai/chat',
      failOnStatusCode: false,
      body: { message: `Cypress production smoke ${Cypress.env('productionRunId')}` },
    }).then((response) => {
      expect(response.status).to.be.lessThan(500);
      cy.registerProductionArtifact('external', 'ai-chat-smoke', {
        status: response.status,
      });
    });

    cy.request({ url: '/api/blogs/latest', failOnStatusCode: false }).then((response) => {
      expect(response.status).to.be.lessThan(500);
    });
  });
});
