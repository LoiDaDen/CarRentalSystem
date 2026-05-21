describe('07 Area operations roles', () => {
  let seed;

  before(() => {
    cy.resetTestData().then((body) => {
      seed = body;
    });
  });

  it('allows Manager to operate area dashboards, area bookings, staff, and stats', () => {
    cy.loginAs('manager');
    [
      '/Admin/AreaDashboard',
      '/Admin/AreaBooking',
      `/Admin/Area/Show/${seed.areaId}`,
      `/Admin/AreaStaff?areaId=${seed.areaId}`,
      '/Admin/Car/Manage',
      '/Admin/Maintenance',
      '/Admin/Enterprise',
    ].forEach((path) => {
      cy.safeVisit(path);
    });

    cy.requestHealthy('/Admin/Support');
    cy.requestHealthy(`/Admin/AreaStats/Stats?areaId=${seed.areaId}`).its('status').should('be.lessThan', 500);
    cy.requestHealthy('/admin/api/users?search=cypress').its('status').should('be.lessThan', 500);
  });

  it('allows Staff to work operational pages but blocks admin-only configuration', () => {
    cy.loginAs('staff');
    [
      '/Admin/AreaDashboard',
      '/Admin/AreaBooking',
      `/Admin/Area/Show/${seed.areaId}`,
      '/Admin/CarApproval',
      '/Admin/Maintenance',
    ].forEach((path) => {
      cy.safeVisit(path);
    });

    cy.requestHealthy('/Admin/Support');
    ['/Admin/User', '/Admin/Payment', '/Admin/Reports', '/Admin/HolidayPricing'].forEach((path) => {
      cy.request({ url: path, followRedirect: false, failOnStatusCode: false }).then((response) => {
        expect(response.status, path).to.be.oneOf([302, 403]);
      });
    });
  });

  it('keeps Viewer out of area admin controllers when no controller allows that app role', () => {
    cy.loginAs('viewer');
    ['/Admin/AreaDashboard', '/Admin/AreaBooking', `/Admin/Area/Show/${seed.areaId}`].forEach((path) => {
      cy.request({ url: path, followRedirect: false, failOnStatusCode: false }).then((response) => {
        expect(response.status, path).to.be.oneOf([302, 403]);
      });
    });
  });
});
