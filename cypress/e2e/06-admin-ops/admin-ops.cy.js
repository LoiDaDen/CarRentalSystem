describe('06 Admin operations surface', () => {
  let seed;

  before(() => {
    cy.resetTestData().then((body) => {
      seed = body;
    });
  });

  beforeEach(() => {
    cy.loginAs('admin');
  });

  it('loads core admin management pages without runtime failures', () => {
    [
      '/Admin/Dashboard',
      '/Admin/User',
      '/Admin/User/PartnerRequests',
      '/Admin/Booking',
      `/Admin/Booking/Details/${seed.bookingId}`,
      '/Admin/Car/Manage',
      `/Admin/Car/Details/${seed.firstAvailableCarId}`,
      '/Admin/Car/Create',
      '/Admin/CarApproval',
      '/Admin/Lookups',
      '/Admin/Payment',
      '/Admin/Promotions',
      '/Admin/Promotions/Create',
      '/Admin/Blog',
      '/Admin/Blog/Create',
      '/Admin/Reviews',
      '/Admin/Contacts',
      '/Admin/Membership',
      '/Admin/HolidayPricing',
      '/Admin/PhysicalContracts',
      '/Admin/Maintenance',
      '/Admin/Settlement',
      '/Admin/Alert',
    ].forEach((path) => {
      cy.safeVisit(path);
    });
  });

  it('runs privacy request workflow with auditable admin actions', () => {
    cy.safeVisit('/Admin/Privacy');
    cy.postForm('/Admin/Privacy/Approve', {
      id: seed.privacyRequestId,
      reason: 'Cypress privacy request approved',
    }).then((response) => {
      expect(response.status).to.be.oneOf([200, 302]);
    });

    cy.safeVisit('/Admin/Privacy');
    cy.postForm('/Admin/Privacy/Complete', {
      id: seed.privacyRequestId,
      reason: 'Cypress export package prepared in test environment',
    }).then((response) => {
      expect(response.status).to.be.oneOf([200, 302]);
    });

    cy.safeVisit('/Admin/Privacy');
    cy.get('body').invoke('text').should('match', /Completed|Hoàn tất|hoan tat|completed/i);
  });

  it('keeps read/report/export endpoints healthy', () => {
    [
      '/Admin/Reports',
      '/Admin/Reports/ExportExcel?period=month',
      '/Admin/AuditLog',
      '/Admin/AuditLog/VerifyChain',
      '/Admin/Privacy/RetentionPreview',
      '/Admin/Tracking/GetLiveLocations',
      '/Admin/Booking/GetRecentNotifications',
      '/Admin/Support',
    ].forEach((path) => {
      cy.requestHealthy(path);
    });
  });
});
