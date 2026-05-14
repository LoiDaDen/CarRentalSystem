describe('Enterprise UI polish', () => {
  const unverifiedCustomer = {
    email: 'john@example.com',
    password: 'Customer@123',
    name: 'John Customer',
    phone: '0901111111',
  };

  const futureIso = (daysFromNow = 7) => {
    const date = new Date();
    date.setDate(date.getDate() + daysFromNow);
    date.setHours(10, 0, 0, 0);
    return date.toISOString().slice(0, 16);
  };

  it('shows the eKYC popup instead of a toast when cart checkout is blocked', () => {
    cy.login(unverifiedCustomer.email, unverifiedCustomer.password);

    const startDate = futureIso(7);
    const endDate = futureIso(9);

    cy.request({
      method: 'POST',
      url: '/Cart/AddToCart',
      form: true,
      body: {
        carId: 1,
        startDate,
        endDate,
        withDriver: false,
        estimatedPrice: 0,
      },
      failOnStatusCode: false,
    });

    cy.visit('/Cart');
    cy.get('body').should('not.contain.text', 'Identity verification is required before renting.');
    cy.get('form[action*="CheckoutAll"]').first().find('button[type="submit"]').click({ force: true });
    cy.get('#ekycQuickModal', { timeout: 10000 })
      .should('be.visible')
      .and('contain.text', 'Cần xác thực eKYC trước khi thuê xe');
    cy.url().should('include', '/Cart');
  });

  it('shows the eKYC popup and preserves the long-term inspection screen', () => {
    cy.login(unverifiedCustomer.email, unverifiedCustomer.password);
    cy.visit('/LongTerm');

    cy.get('a[href*="/LongTerm/RequestForm?contractId="]').first().invoke('attr', 'href').then((href) => {
      const contractId = new URL(href, Cypress.config('baseUrl')).searchParams.get('contractId');
      const startDate = futureIso(10);

      cy.visit(`/LongTerm/VehicleInspection?carId=1&contractId=${contractId}&duration=1&customerName=${encodeURIComponent(unverifiedCustomer.name)}&customerPhone=${unverifiedCustomer.phone}&startDateStr=${encodeURIComponent(startDate)}`);
      cy.get('#confirmForm').should('exist');
      cy.get('#chkExterior').check({ force: true });
      cy.get('#chkTechnical').check({ force: true });
      cy.get('#chkAgree').check({ force: true });
      cy.get('#btnConfirmProceed').should('not.be.disabled').click({ force: true });

      cy.url().should('include', '/LongTerm/VehicleInspection');
      cy.url().should('include', `contractId=${contractId}`);
      cy.get('#ekycQuickModal', { timeout: 10000 })
        .should('be.visible')
        .and('contain.text', 'Cần xác thực eKYC trước khi thuê xe');
    });
  });

  it('toggles admin light/dark theme and persists the choice', () => {
    cy.login('hloi78274@gmail.com', 'Admin@123');
    cy.visit('/Admin/Dashboard');

    cy.get('.admin-theme-toggle').first().click();
    cy.window().its('localStorage.admin-theme').should('match', /light|dark/);
    cy.reload();
    cy.get('.admin-theme-toggle').should('exist');
    cy.window().then((win) => {
      expect(win.document.documentElement.getAttribute('data-bs-theme')).to.eq(win.localStorage.getItem('admin-theme'));
    });
  });

  it('keeps primary pages usable across desktop, tablet, and mobile widths', () => {
    const pages = ['/', '/Cart', '/LongTerm', '/Admin/Dashboard', '/Partner/Dashboard'];
    const widths = [1366, 768, 390];

    cy.login('hloi78274@gmail.com', 'Admin@123');
    widths.forEach((width) => {
      cy.viewport(width, 820);
      pages.forEach((page) => {
        cy.visit(page, { failOnStatusCode: false });
        cy.get('body').should('be.visible');
        cy.get('body').invoke('text').should('not.match', /Khong|Vui long|Thong bao|Thanh toan|Dang xac thuc/);
      });
    });
  });
});
