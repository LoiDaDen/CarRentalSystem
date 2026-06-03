const describeProd = Cypress.env('productionMode') === true ? describe : describe.skip;
const tc = (ids, depth = 'business-flow') => `${Cypress.env('productionMode') === true ? ids.map((id) => `[${id}]`).join('') : ''}[DEPTH:${depth}]`;

describeProd('Production cleanup and evidence scan', () => {
  beforeEach(() => {
    cy.requireProductionMode();
  });

  it(`${tc(['TC154', 'TC155', 'TC157', 'TC158', 'TC442', 'TC443'])} scans customer/admin pages for run-scoped artifacts only`, () => {
    const runId = Cypress.env('productionRunId');

    cy.loginAs('customer');
    cy.safeVisit('/Booking/MyBookings');
    cy.get('body').invoke('text').then((text) => {
      cy.registerProductionArtifact('cleanup', 'customer-bookings-scan', {
        status: 'scanned',
        matchedRunId: text.includes(runId),
        notes: 'Only runId-tagged records are eligible for cleanup; no non-runId data was touched.',
      });
    });

    cy.loginAs('admin');
    ['/Admin/Booking', '/Admin/AuditLog', '/Admin/Payment'].forEach((route) => {
      cy.request({ url: route, failOnStatusCode: false }).then((response) => {
        expect(response.status, route).to.be.lessThan(500);
      });
    });
    cy.registerProductionArtifact('cleanup', 'admin-cleanup-scan', {
      status: 'scanned',
      runId,
      notes: 'Review production-run-summary for payment/refund artifacts that require manual reconciliation.',
    });
  });
});
