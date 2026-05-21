describe('08 Enterprise, audit, reporting, and governance surfaces', () => {
  let seed;

  before(() => {
    cy.resetTestData().then((body) => {
      seed = body;
    });
  });

  it('loads admin enterprise command center and dependent governance pages', () => {
    cy.loginAs('admin');
    [
      '/Admin/Enterprise',
      '/Admin/Sre/Slo',
      '/Admin/Snapshot',
      '/Admin/AuditLog',
      '/Admin/Reports',
      '/Admin/Privacy',
      '/Admin/PhysicalContracts',
      '/Admin/Settlement',
      '/Admin/Maintenance',
    ].forEach((path) => {
      cy.safeVisit(path);
    });
  });

  it('keeps enterprise JSON/read APIs healthy and non-crashing', () => {
    cy.loginAs('admin');
    [
      '/Admin/Enterprise/Snapshot',
      '/Admin/Sre/Slo',
      '/Admin/FleetOps/Readiness',
      '/Admin/AuditLog/VerifyChain',
      '/Admin/Privacy/RetentionPreview',
      `/Admin/Dispute/Export?bookingId=${seed.bookingId}`,
    ].forEach((path) => {
      if (path.includes('/Admin/Dispute/Export')) {
        cy.safeVisit('/Auth/Profile');
        cy.postForm('/Admin/Dispute/Export', { bookingId: seed.bookingId, includeSensitive: false }).then((response) => {
          expect(response.status).to.be.lessThan(500);
        });
      } else {
        cy.requestHealthy(path);
      }
    });
  });

  it('allows Manager to read command center but blocks snapshot admin tooling', () => {
    cy.loginAs('manager');
    ['/Admin/Enterprise', '/Admin/Sre/Slo', '/Admin/Privacy', '/Admin/Settlement'].forEach((path) => {
      cy.safeVisit(path);
    });
    cy.requestHealthy('/Admin/FleetOps/Readiness');

    ['/Admin/Snapshot', '/Admin/Reports'].forEach((path) => {
      cy.request({ url: path, followRedirect: false, failOnStatusCode: false }).then((response) => {
        expect(response.status, path).to.be.oneOf([302, 403]);
      });
    });
  });
});
