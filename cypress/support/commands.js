// Custom Cypress commands for the CarRentalSystem A-Z E2E suite.

let browserGeolocationStub = null;

const fatalPageText = /An unhandled exception occurred|Invalid column name|Invalid object name|SqlException|NullReferenceException|RuntimeBinderException|ERR_TOO_MANY_REDIRECTS|Oops!\s*Đã xảy ra lỗi|Error - CarRental/i;

const camelizeKeys = (value) => {
  if (Array.isArray(value)) return value.map(camelizeKeys);
  if (!value || typeof value !== 'object') return value;

  return Object.entries(value).reduce((result, [key, item]) => {
    const camelKey = key.charAt(0).toLowerCase() + key.slice(1);
    result[camelKey] = camelizeKeys(item);
    return result;
  }, {});
};

const cypressSecretHeaders = () => ({
  'X-Cypress-Reset-Secret': Cypress.env('resetSecret') || 'local-cypress-reset',
});

Cypress.on('window:before:load', (win) => {
  if (!browserGeolocationStub) return;
  const position = {
    coords: {
      latitude: browserGeolocationStub.lat,
      longitude: browserGeolocationStub.lng,
      accuracy: browserGeolocationStub.accuracy,
      speed: browserGeolocationStub.speed,
    },
    timestamp: Date.now(),
  };

  Object.defineProperty(win.navigator, 'geolocation', {
    configurable: true,
    value: {
      getCurrentPosition(success) {
        setTimeout(() => success(position), 0);
      },
      watchPosition(success) {
        setTimeout(() => success(position), 0);
        return 1;
      },
      clearWatch() {},
    },
  });
});

Cypress.Commands.add('resetTestData', () => {
  return cy.request({
    method: 'POST',
    url: '/__test/reset',
    timeout: 120000,
    failOnStatusCode: false,
    headers: cypressSecretHeaders(),
  }).then((response) => {
    expect(response.status, JSON.stringify(response.body)).to.eq(200);
    const seed = camelizeKeys(response.body);
    Cypress.env('seed', seed);
    return seed;
  });
});

Cypress.Commands.add('assertSandboxConfigured', (options = {}) => {
  const requirePayOS = options.payos ?? (Cypress.env('usePayOSSandbox') === true);
  const requireFpt = options.fpt ?? (Cypress.env('useFptSandbox') === true);
  const requireReady = options.ready ?? (requirePayOS || requireFpt);

  return cy.request({
    method: 'GET',
    url: '/__test/sandbox/status',
    failOnStatusCode: false,
    headers: cypressSecretHeaders(),
  }).then((response) => {
    expect(response.status, JSON.stringify(response.body)).to.eq(200);
    const status = camelizeKeys(response.body);
    expect(status.isCypressDatabase, 'Cypress database guard').to.eq(true);
    if (requirePayOS) {
      expect(status.usePayOSSandbox, 'Cypress:UsePayOSSandbox').to.eq(true);
      expect(status.payOS.configured, 'PayOS sandbox credentials').to.eq(true);
    }
    if (requireFpt) {
      expect(status.useFptSandbox, 'Cypress:UseFptSandbox').to.eq(true);
      expect(status.fptAi.configured, 'FPT sandbox API key').to.eq(true);
    }
    if (requireReady) {
      const relevantMissing = (status.missing || []).filter((item) => {
        if (/^PayOS__|^App__PublicBaseUrl/i.test(item)) return requirePayOS;
        if (/^FptAi__/i.test(item)) return requireFpt;
        return true;
      });
      expect(relevantMissing, `Missing sandbox config: ${relevantMissing.join(', ')}`).to.deep.eq([]);
      if (requirePayOS && requireFpt) {
        expect(status.ready).to.eq(true);
      }
    }
    return status;
  });
});

Cypress.Commands.add('seedScenario', (name) => {
  return cy.request({
    method: 'POST',
    url: '/__test/seed/scenario',
    failOnStatusCode: false,
    headers: cypressSecretHeaders(),
    body: { name },
  }).then((response) => {
    expect(response.status, JSON.stringify(response.body)).to.eq(200);
    const scenario = camelizeKeys(response.body);
    Cypress.env('scenario', scenario);
    return scenario;
  });
});

Cypress.Commands.add('waitForPayOSWebhook', (orderCode, options = {}) => {
  const timeoutSeconds = options.timeoutSeconds || 120;
  return cy.request({
    method: 'GET',
    url: `/__test/payments/wait-paid?orderCode=${encodeURIComponent(orderCode)}&timeoutSeconds=${timeoutSeconds}`,
    timeout: (timeoutSeconds + 15) * 1000,
    failOnStatusCode: false,
    headers: cypressSecretHeaders(),
  }).then((response) => {
    expect(response.status, JSON.stringify(response.body)).to.eq(200);
    const body = camelizeKeys(response.body);
    expect(body.paid, JSON.stringify(body)).to.eq(true);
    return body;
  });
});

Cypress.Commands.add('postMobileTracking', (bookingId, coords = {}) => {
  return cy.request({
    method: 'POST',
    url: '/Tracking/MobileUpdate',
    failOnStatusCode: false,
    body: {
      bookingId,
      latitude: coords.lat ?? coords.latitude,
      longitude: coords.lng ?? coords.longitude,
      speed: coords.speed ?? 0,
    },
  }).then((response) => {
    expect(response.status, JSON.stringify(response.body)).to.eq(200);
    return camelizeKeys(response.body);
  });
});

Cypress.Commands.add('stubBrowserGeolocation', (lat, lng, options = {}) => {
  browserGeolocationStub = {
    lat,
    lng,
    accuracy: options.accuracy || 12,
    speed: options.speed || 0,
  };
});

Cypress.Commands.add('assertNoFatalPageError', () => {
  cy.get('body').should('be.visible');
  cy.get('body').invoke('text').should((text) => {
    expect(text).not.to.match(fatalPageText);
  });
});

Cypress.Commands.add('safeVisit', (url, options = {}) => {
  cy.visit(url, { failOnStatusCode: false, ...options });
  cy.assertNoFatalPageError();
});

Cypress.Commands.add('requestHealthy', (url, options = {}) => {
  return cy.request({ url, failOnStatusCode: false, ...options }).then((response) => {
    const expectedNonCrashStatus = response.status < 500
      || (/^\/ready(?:$|\?)/i.test(String(url)) && response.status === 503);
    expect(expectedNonCrashStatus, `${url} returned ${response.status}`).to.eq(true);
    if (typeof response.body === 'string') {
      expect(response.body, url).not.to.match(fatalPageText);
    }
    return response;
  });
});

Cypress.Commands.add('getAntiForgeryToken', () => {
  return cy.get('body').then(($body) => {
    const existingToken = $body.find('input[name="__RequestVerificationToken"]').first().val();
    if (existingToken) return existingToken;

    return cy.visit('/Auth/Profile', { failOnStatusCode: false }).then(() => {
      return cy.get('input[name="__RequestVerificationToken"]').first().invoke('val');
    });
  });
});

Cypress.Commands.add('login', (email, password) => {
  cy.clearCookies();
  cy.clearLocalStorage();
  cy.visit('/Auth/Login');
  cy.get('input[name="email"]').clear().type(email);
  cy.get('input[name="password"]').clear().type(password, { log: false });
  cy.get('input[name="email"]').closest('form').submit();
  cy.url().should('not.include', '/Auth/Login');
  cy.getCookie('CarRental.Auth').should('exist');
});

Cypress.Commands.add('loginAs', (roleKey) => {
  cy.fixture('users').then((users) => {
    const user = users[roleKey];
    expect(user, `fixture user ${roleKey}`).to.exist;
    cy.login(user.email, user.password);
  });
});

Cypress.Commands.add('logout', () => {
  cy.visit('/Auth/Logout', { failOnStatusCode: false });
  cy.getCookie('CarRental.Auth').should('be.null');
});

Cypress.Commands.add('clearCart', () => {
  cy.safeVisit('/Cart');
  cy.get('body').then(($body) => {
    const ids = [...$body.find('[id^="car-card-"]')]
      .map((el) => el.id.replace('car-card-', ''))
      .filter(Boolean);

    if (!ids.length) return;

    cy.getAntiForgeryToken().then((token) => {
      cy.wrap(ids).each((id) => {
        cy.request({
          method: 'POST',
          url: '/Cart/RemoveFromCart',
          form: true,
          headers: {
            RequestVerificationToken: token,
          },
          body: {
            id,
            __RequestVerificationToken: token,
          },
        }).its('body.success').should('eq', true);
      });
    });
  });
});

Cypress.Commands.add('visitFirstListedCarDetails', (startDate, endDate) => {
  cy.safeVisit('/Car');
  cy.get('a[href*="/Car/Details"]').first().invoke('attr', 'href').then((href) => {
    const url = new URL(href, Cypress.config('baseUrl'));
    if (startDate) url.searchParams.set('startDate', startDate);
    if (endDate) url.searchParams.set('endDate', endDate);
    cy.safeVisit(`${url.pathname}${url.search}`);
  });
});

Cypress.Commands.add('createBookingFixture', () => {
  const seed = Cypress.env('seed');
  if (seed?.bookingId) {
    return cy.wrap(seed.bookingId);
  }

  return cy.resetTestData().then((freshSeed) => freshSeed.bookingId);
});

Cypress.Commands.add('postForm', (url, formBody = {}) => {
  return cy.getAntiForgeryToken().then((token) => {
    return cy.request({
      method: 'POST',
      url,
      form: true,
      failOnStatusCode: false,
      headers: {
        RequestVerificationToken: token,
      },
      body: {
        ...formBody,
        __RequestVerificationToken: token,
      },
    });
  });
});

const workbookMojibakeText = /Ã|Â|Ä|Æ|�|â€|áº|á»/;
const seriousConsoleError = /ReferenceError|TypeError|is not defined|Cannot read properties|Unhandled|Invalid column name|Invalid object name|SqlException|NullReferenceException|RuntimeBinderException/i;
const legalDeadLinkText = /terms|privacy|điều khoản|dieu khoan|chính sách|chinh sach|bảo mật|bao mat/i;
const pseudoRouteText = /^\/(?:options|ui|payment|routes|terms|apis?|screens?|pages?|flow|flows?)$/i;
const nonHtmlRouteText = /\.webmanifest(?:$|\?)|\/manifest\.webmanifest|service-worker|\.json(?:$|\?)|\.xml(?:$|\?)|\.css(?:$|\?)|\.js(?:$|\?)/i;

const roleKeyMap = {
  admin: 'admin',
  manager: 'manager',
  staff: 'staff',
  viewer: 'viewer',
  customer: 'customer',
  owner: 'owner',
  partner: 'owner',
  driver: 'owner',
};

const moduleRoutes = {
  MD01: ['/', '/Car', '/Home/Privacy'],
  MD02: ['/Auth/Login', '/Auth/Register', '/Auth/ForgotPassword'],
  MD03: ['/Auth/Profile', '/Ekyc', '/SecureFile/GetSecureImage?folder=eKyc&fileName=../../appsettings.json'],
  MD04: ['/', '/Car', '/CarCatalog/Models?brandId=1'],
  MD05: ['/Car/Details/{firstAvailableCarId}', '/Car'],
  MD06: ['/Booking/MyBookings', '/Booking/Details/{bookingId}'],
  MD07: ['/Cart', '/Cart/GetCartCount'],
  MD08: ['/Cart', '/Admin/Payment'],
  MD09: ['/PhysicalContracts', '/Handover/Index?bookingId={bookingId}'],
  MD10: ['/LongTerm', '/LongTerm/RequestForm?contractId={contractTypeId}'],
  MD11: ['/OwnerCars', '/Partner/Dashboard', '/Driver/Dashboard'],
  MD12: ['/Membership', '/SelfService/Summary'],
  MD13: ['/Blog', '/BlogApi/Latest'],
  MD14: ['/Auth/Profile', '/Support/Mine', '/Admin/Privacy'],
  MD15: ['/Tracking/Map', '/Admin/Tracking/GetLiveLocations'],
  MD16: ['/Ai/Chat'],
  MD17: ['/Admin/Dashboard', '/Admin/Sre/Slo', '/Admin/Enterprise'],
  MD18: ['/Admin/AreaDashboard', '/Admin/AreaBooking', '/Admin/Area/Show/{areaId}'],
  MD19: ['/Admin/Booking', '/Admin/Booking/Details/{bookingId}', '/Admin/PhysicalContracts'],
  MD20: ['/Admin/User', '/Admin/User/PartnerRequests'],
  MD21: ['/Admin/Car/Manage', '/Admin/Lookups', '/Admin/FleetOps/Readiness'],
  MD22: ['/Admin/Promotions', '/Admin/AuditLog', '/Admin/Snapshot'],
  MD23: ['/Admin/Dashboard', '/Admin/AuditLog', '/SecureFile/GetSecureImage?folder=eKyc&fileName=../../appsettings.json'],
  MD24: ['/', '/Car', '/Admin/Dashboard'],
  MD25: ['/Home/Privacy', '/Home/Terms', '/Admin/Privacy/RetentionPreview'],
  MD26: ['/__test/sandbox/status', '/Tracking/Booking/{bookingId}', '/OwnerCars/Create'],
};

const routeReplacements = (seed = {}) => ({
  '{firstAvailableCarId}': seed.firstAvailableCarId || 1,
  '{pendingOwnerCarId}': seed.pendingOwnerCarId || 1,
  '{bookingId}': seed.bookingId || 1,
  '{paymentId}': seed.paymentId || 1,
  '{privacyRequestId}': seed.privacyRequestId || 1,
  '{areaId}': seed.areaId || 1,
  '{contractTypeId}': seed.contractTypeId || 1,
});

const normalizeRoute = (route, seed) => {
  let normalized = String(route || '').trim();
  if (!normalized) return null;
  normalized = normalized.replace(/[),.;]+$/g, '');
  if (!normalized.startsWith('/')) normalized = `/${normalized}`;
  for (const [token, value] of Object.entries(routeReplacements(seed))) {
    normalized = normalized.replaceAll(token, value);
  }
  normalized = normalized
    .replace(/:id\b/g, seed.bookingId || 1)
    .replace(/\{id\}/g, seed.bookingId || 1)
    .replace(/ID=1/g, `ID=${seed.bookingId || 1}`);
  return normalized;
};

const isUsableRoute = (route) => Boolean(route) && !pseudoRouteText.test(route);

const routeCandidates = (testCase, seed) => {
  const explicit = String(testCase.routeApi || '')
    .split(/[\s,;]+/)
    .filter((token) => token.startsWith('/'))
    .map((route) => normalizeRoute(route, seed))
    .filter(isUsableRoute);

  const fallback = (moduleRoutes[testCase.moduleCode] || ['/'])
    .map((route) => normalizeRoute(route, seed))
    .filter(isUsableRoute);

  return [...new Set([...explicit, ...fallback])]
    .filter((route) => !route.includes('Wallet/UI'))
    .slice(0, 3);
};

const isHtmlRoute = (route) => {
  if (!route) return false;
  if (nonHtmlRouteText.test(route)) return false;
  return !/\/api\/|\/Get|\/Check|\/Calculate|\/Snapshot$|\/VerifyChain|\/RetentionPreview|\/Readiness|\/Stats|\/Mine|\/Chat|\/Export|\/GetLiveLocations|\/GetRecentNotifications|\/Models|\/RequestDelete/i.test(route);
};

const isHtmlResponse = (response) => (
  String(response.headers?.['content-type'] || '').toLowerCase().includes('text/html')
);

const primaryHtmlRoute = (routes, testCase, seed) => {
  const route = routes.find(isHtmlRoute);
  if (route) return route;

  return (moduleRoutes[testCase.moduleCode] || ['/'])
    .map((candidate) => normalizeRoute(candidate, seed))
    .filter(isUsableRoute)
    .find(isHtmlRoute) || '/';
};

const fallbackVisualRoute = (testCase, seed) => (
  (moduleRoutes[testCase.moduleCode] || ['/'])
    .map((candidate) => normalizeRoute(candidate, seed))
    .filter(isUsableRoute)
    .find(isHtmlRoute) || '/'
);

const visualRouteFromResponse = (route, fallbackRoute = '/') => {
  const normalizedFallback = fallbackRoute || '/';
  return cy.requestHealthy(route).then((response) => {
    if (isHtmlRoute(route) && isHtmlResponse(response) && response.status < 400) {
      return route;
    }

    if (normalizedFallback === route) {
      return '/';
    }

    return cy.requestHealthy(normalizedFallback).then((fallbackResponse) => {
      if (isHtmlRoute(normalizedFallback) && isHtmlResponse(fallbackResponse) && fallbackResponse.status < 400) {
        return normalizedFallback;
      }

      return '/';
    });
  });
};

const legalAssertionRoute = (routes) => (
  routes.find((route) => /^\/Home\/(?:Privacy|Terms)(?:$|\?)/i.test(route)) || '/Home/Privacy'
);

const chooseRoleKey = (testCase) => {
  const roleText = `${testCase.role || ''} ${testCase.title || ''} ${testCase.moduleName || ''}`.toLowerCase();
  if (roleText.includes('guest') && !roleText.includes('customer') && !testCase.moduleCode?.startsWith('MD1') && testCase.moduleCode !== 'MD24') {
    return null;
  }
  if (['MD17', 'MD19', 'MD20', 'MD21', 'MD22', 'MD23'].includes(testCase.moduleCode)) return 'admin';
  if (testCase.moduleCode === 'MD18') {
    if (roleText.includes('staff')) return 'staff';
    if (roleText.includes('viewer')) return 'viewer';
    return 'manager';
  }
  if (roleText.includes('owner') || roleText.includes('partner') || roleText.includes('driver')) return 'owner';
  for (const [needle, roleKey] of Object.entries(roleKeyMap)) {
    if (roleText.includes(needle)) return roleKey;
  }
  return null;
};

const runRouteCheck = (route) => {
  cy.requestHealthy(route).then((response) => {
    if (isHtmlRoute(route) && isHtmlResponse(response) && response.status < 400) {
      cy.safeVisit(route);
    }
  });
};

Cypress.Commands.add('ensureLoggedInAs', (roleKey) => {
  cy.fixture('users').then((users) => {
    const user = users[roleKey];
    expect(user, `fixture user ${roleKey}`).to.exist;
    cy.session(`workbook:${roleKey}`, () => {
      cy.visit('/Auth/Login');
      cy.get('input[name="email"]').clear().type(user.email);
      cy.get('input[name="password"]').clear().type(user.password, { log: false });
      cy.get('input[name="email"]').closest('form').submit();
      cy.url().should('not.include', '/Auth/Login');
      cy.getCookie('CarRental.Auth').should('exist');
    }, {
      validate() {
        cy.getCookie('CarRental.Auth').should('exist');
      },
    });
  });
});

Cypress.Commands.add('tc', (testCase) => {
  expect(testCase.id, 'workbook testcase id').to.match(/^TC\d{3}$/);
  cy.log(`${testCase.id} ${testCase.moduleCode} ${testCase.priority}: ${testCase.title}`);
});

Cypress.Commands.add('mockExternalProviders', () => {
  cy.intercept(/payos|fpt|groq|gemini|googleapis|nominatim|osrm|smtp/i, {
    statusCode: 200,
    body: { success: true, mocked: true, provider: 'cypress' },
  });
});

Cypress.Commands.add('assertNoMojibake', () => {
  cy.get('body').invoke('text').should((text) => {
    expect(text).not.to.match(workbookMojibakeText);
  });
});

Cypress.Commands.add('assertNoDeadLegalLinks', () => {
  cy.get('a[href="#"]').then(($links) => {
    const offenders = [...$links]
      .map((link) => link.textContent || '')
      .filter((text) => legalDeadLinkText.test(text));
    expect(offenders, 'dead legal links').to.deep.eq([]);
  });
});

Cypress.Commands.add('assertDenied', (route, roleKey = 'customer') => {
  cy.ensureLoggedInAs(roleKey);
  cy.request({ url: route, followRedirect: false, failOnStatusCode: false }).then((response) => {
    expect(response.status, `${roleKey} denied ${route}`).to.be.oneOf([302, 401, 403, 404]);
  });
});

Cypress.Commands.add('assertRouteAccess', (route, options = {}) => {
  if (options.roleKey) cy.ensureLoggedInAs(options.roleKey);
  if (options.denied) {
    cy.assertDenied(route, options.roleKey || 'customer');
  } else {
    runRouteCheck(route);
  }
});

Cypress.Commands.add('assertNoSeriousConsoleErrors', (route = '/') => {
  cy.visit(route, {
    failOnStatusCode: false,
    onBeforeLoad(win) {
      win.__workbookConsoleErrors = [];
      const originalError = win.console.error;
      win.console.error = (...args) => {
        const message = args.map((arg) => String(arg)).join(' ');
        if (seriousConsoleError.test(message)) {
          win.__workbookConsoleErrors.push(message);
        }
        originalError.apply(win.console, args);
      };
    },
  });
  cy.assertNoFatalPageError();
  cy.window().then((win) => {
    expect(win.__workbookConsoleErrors || [], route).to.deep.eq([]);
  });
});

Cypress.Commands.add('assertResponsiveBaseline', (route = '/') => {
  const viewports = [
    [1366, 768],
    [768, 1024],
    [375, 667],
  ];

  cy.wrap(viewports).each(([width, height]) => {
    cy.viewport(width, height);
    cy.safeVisit(route);
    cy.document().then((doc) => {
      const overflow = doc.documentElement.scrollWidth - doc.documentElement.clientWidth;
      expect(overflow, `${route} horizontal overflow ${width}x${height}`).to.be.lessThan(120);
    });
  });
});

Cypress.Commands.add('assertKeyboardBaseline', () => {
  cy.get('body').then(($body) => {
    const focusable = $body.find('a[href], button, input, select, textarea, [tabindex]:not([tabindex="-1"])')
      .filter(':visible')
      .first();
    if (!focusable.length) return;
    cy.wrap(focusable).focus().should('be.focused');
  });
});

Cypress.Commands.add('assertNoXssExecution', (route = '/') => {
  const separator = route.includes('?') ? '&' : '?';
  const payload = encodeURIComponent('<script>window.__workbookXssExecuted=true</script>');
  cy.visit(`${route}${separator}q=${payload}`, {
    failOnStatusCode: false,
    onBeforeLoad(win) {
      win.__workbookXssExecuted = false;
    },
  });
  cy.window().its('__workbookXssExecuted').should('eq', false);
  cy.assertNoFatalPageError();
});

Cypress.Commands.add('runWorkbookTestCase', (testCase) => {
  cy.tc(testCase);
  cy.mockExternalProviders();

  cy.then(() => {
    const seed = Cypress.env('seed') || {};
    const roleKey = chooseRoleKey(testCase);
    const routes = routeCandidates(testCase, seed);
    const title = `${testCase.title || ''} ${testCase.expected || ''}`.toLowerCase();
    const primaryRoute = primaryHtmlRoute(routes, testCase, seed);
    const fallbackRoute = fallbackVisualRoute(testCase, seed);

    if (roleKey) {
      cy.ensureLoggedInAs(roleKey);
    } else {
      cy.clearCookies();
    }

    if (testCase.moduleCode === 'MD16') {
      cy.request({
        method: 'POST',
        url: '/Ai/Chat',
        failOnStatusCode: false,
        body: { message: 'Cypress workbook AI safety smoke test' },
      }).then((response) => {
        expect(response.status).to.be.lessThan(500);
      });
      return;
    }

    if (/pwa|manifest|service worker|offline/.test(title)) {
      routes.forEach((route) => cy.requestHealthy(route));
      if (/layout|responsive|accessibility|console/.test(title)) {
        cy.assertResponsiveBaseline('/');
        cy.assertNoSeriousConsoleErrors('/');
        cy.assertKeyboardBaseline();
      }
      return;
    }

    if (/layout|responsive|accessibility|console/.test(title)) {
      visualRouteFromResponse(primaryRoute, fallbackRoute).then((route) => {
        cy.assertResponsiveBaseline(route);
        cy.assertNoSeriousConsoleErrors(route);
        cy.assertKeyboardBaseline();
      });
      return;
    }

    if (/validation|edge|csrf|idor|rbac|phân quyền|phan quyen|không đủ quyền|khong du quyen|admin-only|security|secrets/.test(title)) {
      routes.slice(0, 2).forEach((route) => runRouteCheck(route));
      cy.requestHealthy(`${primaryRoute}${primaryRoute.includes('?') ? '&' : '?'}id=999999`);
      if (!['MD17', 'MD18', 'MD19', 'MD20', 'MD21', 'MD22'].includes(testCase.moduleCode)) {
        cy.assertDenied('/Admin/Dashboard', 'customer');
      }
      return;
    }

    if (/xss|html\/script|script giả|script gia/.test(title)) {
      visualRouteFromResponse(primaryRoute, fallbackRoute).then((route) => {
        cy.assertNoXssExecution(route);
      });
      return;
    }

    routes.slice(0, 2).forEach((route) => runRouteCheck(route));

    if (/footer|legal|privacy|terms|pháp lý|phap ly|bảo mật|bao mat|điều khoản|dieu khoan|mojibake|placeholder/.test(title) || testCase.moduleCode === 'MD25') {
      const route = legalAssertionRoute(routes);
      cy.safeVisit(route);
      cy.assertNoDeadLegalLinks();
      if (['/Home/Privacy', '/Home/Terms'].includes(route)) {
        cy.assertNoMojibake();
      }
    }
  });
});
