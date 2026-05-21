const seriousConsoleError = /ReferenceError|TypeError|is not defined|Cannot read properties|Unhandled|Invalid column name|Invalid object name/i;

const visitWithConsoleGuard = (path) => {
  cy.visit(path, {
    failOnStatusCode: false,
    onBeforeLoad(win) {
      win.__seriousConsoleErrors = [];
      const originalError = win.console.error;
      win.console.error = (...args) => {
        const message = args.map((arg) => String(arg)).join(' ');
        if (seriousConsoleError.test(message)) {
          win.__seriousConsoleErrors.push(message);
        }
        originalError.apply(win.console, args);
      };
    },
  });
  cy.assertNoFatalPageError();
  cy.window().then((win) => {
    expect(win.__seriousConsoleErrors || [], path).to.deep.eq([]);
  });
};

describe('10 Non-functional smoke guards', () => {
  before(() => {
    cy.resetTestData();
  });

  it('keeps key public pages responsive on phone, tablet, and desktop widths', () => {
    const viewports = [
      ['iphone-6', null],
      ['ipad-2', null],
      [1366, 768],
    ];

    viewports.forEach(([width, height]) => {
      if (height == null) {
        cy.viewport(width);
      } else {
        cy.viewport(width, height);
      }

      ['/', '/Car', '/Home/Privacy', '/Home/Terms'].forEach((path) => {
        cy.safeVisit(path);
        cy.document().then((doc) => {
          const overflow = doc.documentElement.scrollWidth - doc.documentElement.clientWidth;
          expect(overflow, `${path} horizontal overflow`).to.be.lessThan(96);
        });
      });
    });
  });

  it('toggles the admin dark theme without breaking the dashboard shell', () => {
    cy.loginAs('admin');
    cy.safeVisit('/Admin/Dashboard');
    cy.window().then((win) => win.localStorage.setItem('admin-theme', 'dark'));
    cy.reload();
    cy.document().its('documentElement').should('have.attr', 'data-bs-theme', 'dark');
    cy.window().then((win) => win.localStorage.setItem('admin-theme', 'light'));
    cy.reload();
    cy.document().its('documentElement').should('have.attr', 'data-bs-theme', 'light');
  });

  it('does not emit serious console errors on primary public/admin pages', () => {
    visitWithConsoleGuard('/');
    visitWithConsoleGuard('/Car');
    cy.loginAs('admin');
    visitWithConsoleGuard('/Admin/Dashboard');
    visitWithConsoleGuard('/Admin/Enterprise');
  });

  it('scans a bounded set of internal links for server errors', () => {
    const scanLinks = (path) => {
      cy.safeVisit(path);
      cy.get('a[href]').then(($links) => {
        const urls = [...$links]
          .map((link) => link.getAttribute('href'))
          .filter(Boolean)
          .filter((href) => href.startsWith('/') || href.startsWith(Cypress.config('baseUrl')))
          .filter((href) => !href.includes('/Auth/Logout'))
          .filter((href) => !href.includes('/Payment/'))
          .slice(0, 25);

        cy.wrap([...new Set(urls)]).each((href) => {
          const url = href.startsWith(Cypress.config('baseUrl'))
            ? href.replace(Cypress.config('baseUrl'), '')
            : href;
          cy.requestHealthy(url);
        });
      });
    };

    scanLinks('/');
    cy.loginAs('admin');
    scanLinks('/Admin/Dashboard');
  });

  it('reports external provider sandbox readiness consistently', () => {
    cy.assertSandboxConfigured({
      payos: Cypress.env('usePayOSSandbox') === true,
      fpt: Cypress.env('useFptSandbox') === true,
      ready: Cypress.env('usePayOSSandbox') === true || Cypress.env('useFptSandbox') === true,
    });

    cy.request({ url: '/ready', failOnStatusCode: false }).then((response) => {
      expect(response.status).to.be.oneOf([200, 503]);
      expect(response.body.checks).to.be.an('array');
    });
  });
});
