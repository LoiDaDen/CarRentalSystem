describe('02 RBAC matrix across primary roles', () => {
  before(() => {
    cy.resetTestData();
  });

  it('redirects anonymous users away from authenticated surfaces', () => {
    ['/Cart', '/Booking/MyBookings', '/Ekyc', '/OwnerCars', '/Admin/Dashboard'].forEach((path) => {
      cy.request({ url: path, followRedirect: false, failOnStatusCode: false }).then((response) => {
        expect(response.status, path).to.be.oneOf([302, 401, 403]);
      });
    });
  });

  it('allows Admin to reach the full admin shell', () => {
    cy.loginAs('admin');
    [
      '/Admin/Dashboard',
      '/Admin/User',
      '/Admin/Booking',
      '/Admin/Payment',
      '/Admin/Reports',
      '/Admin/Privacy',
      '/Admin/Tracking',
      '/Admin/CarApproval',
      '/Admin/Enterprise',
    ].forEach((path) => {
      cy.safeVisit(path);
    });
  });

  it('allows Manager and Staff to reach area operations but not admin-only payment/user management', () => {
    ['manager', 'staff'].forEach((roleKey) => {
      cy.loginAs(roleKey);
      ['/Admin/AreaDashboard', '/Admin/AreaBooking', '/Admin/CarApproval'].forEach((path) => {
        cy.safeVisit(path);
      });

      ['/Admin/User', '/Admin/Payment'].forEach((path) => {
        cy.request({ url: path, followRedirect: false, failOnStatusCode: false }).then((response) => {
          expect(response.status, `${roleKey} ${path}`).to.be.oneOf([302, 403]);
        });
      });
    });
  });

  it('keeps Customer and Viewer out of admin pages', () => {
    ['customer', 'viewer'].forEach((roleKey) => {
      cy.loginAs(roleKey);
      ['/Admin/Dashboard', '/Admin/User', '/Admin/Reports', '/Admin/Payment'].forEach((path) => {
        cy.request({ url: path, followRedirect: false, failOnStatusCode: false }).then((response) => {
          expect(response.status, `${roleKey} ${path}`).to.be.oneOf([302, 403]);
        });
      });
    });
  });

  it('redirects legacy Partner and Driver routes to active modules', () => {
    cy.loginAs('owner');
    cy.safeVisit('/Partner/Dashboard');
    cy.url().should('include', '/OwnerCars');
    cy.safeVisit('/Driver/Dashboard');
    cy.url().should('include', '/OwnerCars');
  });
});
