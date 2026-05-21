describe('01 Auth, sessions, and account entrypoints', () => {
  before(() => {
    cy.resetTestData();
  });

  it('rejects invalid credentials without creating a session', () => {
    cy.visit('/Auth/Login');
    cy.get('input[name="email"]').type('wrong@cypress.local');
    cy.get('input[name="password"]').type('wrong-password', { log: false });
    cy.get('input[name="email"]').closest('form').submit();
    cy.url().should('include', '/Auth/Login');
    cy.getCookie('CarRental.Auth').should('be.null');
    cy.assertNoFatalPageError();
  });

  ['admin', 'manager', 'staff', 'viewer', 'customer', 'customerUnverified', 'owner'].forEach((roleKey) => {
    it(`logs in and out as ${roleKey}`, () => {
      cy.loginAs(roleKey);
      cy.logout();
    });
  });

  it('loads registration, email verification, MFA, and password reset pages', () => {
    [
      '/Auth/Register',
      '/Auth/VerifyEmailStep1',
      '/Auth/VerifyEmailStep2?email=customer%40cypress.local',
      '/Auth/ForgotPassword',
      '/Auth/ResetPassword?email=customer%40cypress.local',
      '/Auth/AccessDenied',
    ].forEach((path) => {
      cy.safeVisit(path);
    });
  });

  it('keeps auth cookies http-only and scoped to the app session', () => {
    cy.loginAs('customer');
    cy.getCookie('CarRental.Auth').should((cookie) => {
      expect(cookie).to.exist;
      expect(cookie.httpOnly).to.eq(true);
    });
  });
});
