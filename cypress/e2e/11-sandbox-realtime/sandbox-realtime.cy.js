const tinyPng = 'iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAFgwJ/l6m2VwAAAABJRU5ErkJggg==';

const futureIso = (daysFromNow) => {
  const date = new Date();
  date.setDate(date.getDate() + daysFromNow);
  date.setHours(9, 0, 0, 0);
  return date.toISOString().slice(0, 16);
};

const carImageFiles = (count = 4) => Array.from({ length: count }, (_, index) => ({
  contents: Cypress.Buffer.from(tinyPng, 'base64'),
  fileName: `cypress-car-${index + 1}.png`,
  mimeType: 'image/png',
}));

describe('[MD26] Sandbox realtime delivery and vendor integrations', () => {
  before(() => {
    cy.resetTestData();
  });

  it('[TC526][MD26][P0] Sandbox vendor configuration preflight', () => {
    cy.assertSandboxConfigured({ payos: true, fpt: true, ready: true });
  });

  it('[TC527][MD26][P0] PayOS sandbox payment link creation', () => {
    cy.assertSandboxConfigured({ payos: true, ready: true });
    cy.seedScenario('payos-vendor-checkout').then((scenario) => {
      expect(scenario.orderCode).to.be.greaterThan(0);
      expect(scenario.bookingHoldId).to.be.greaterThan(0);
      expect(scenario.checkoutUrl).to.match(/^https?:\/\//);
      cy.request({ url: scenario.checkoutUrl, failOnStatusCode: false }).its('status').should('be.lessThan', 500);
    });
  });

  it('[TC528][MD26][P0] PayOS full vendor webhook converts paid hold', () => {
    cy.assertSandboxConfigured({ payos: true, ready: true });
    cy.seedScenario('payos-vendor-checkout').then((scenario) => {
      expect(scenario.orderCode).to.be.greaterThan(0);
      cy.log(`Complete PayOS sandbox checkout if the vendor does not auto-simulate payment: ${scenario.checkoutUrl}`);
      cy.waitForPayOSWebhook(scenario.orderCode, {
        timeoutSeconds: Number(Cypress.env('payosWebhookTimeoutSeconds')) || 180,
      }).then((paid) => {
        expect(paid.bookingID || paid.bookingId).to.be.greaterThan(0);
      });
    });
  });

  it('[TC529][MD26][P1] FPT eKYC sandbox OCR and face compare', () => {
    cy.assertSandboxConfigured({ payos: false, fpt: true, ready: true });
    cy.loginAs('customerUnverified');
    cy.safeVisit('/Ekyc');
    cy.get('#idFront').selectFile('cypress/fixtures/vendor/fpt/id-front.jpg', { force: true });
    cy.get('#idBack').selectFile('cypress/fixtures/vendor/fpt/id-back.jpg', { force: true });
    cy.get('#license').selectFile('cypress/fixtures/vendor/fpt/license-front.jpg', { force: true });
    cy.get('#licenseBack').selectFile('cypress/fixtures/vendor/fpt/license-back.jpg', { force: true });
    cy.get('#face').selectFile('cypress/fixtures/vendor/fpt/selfie.jpg', { force: true });
    cy.get('#EkycPrivacyConsent').check({ force: true });
    cy.get('#EkycBiometricConsent').check({ force: true });
    cy.get('#agreeTerms').check({ force: true });
    cy.get('#ekycForm').submit();
    cy.assertNoFatalPageError();
  });

  it('[TC530][MD26][P0] OwnerCars map address and coordinates required', () => {
    cy.loginAs('owner');
    cy.safeVisit('/OwnerCars/Create');
    cy.contains('.step', /Cho|thu/i).click();
    cy.get('#carAddressInput').should('be.visible');
    cy.get('#carAddressInput').clear();
    cy.get('#latInput').invoke('val', '');
    cy.get('#lngInput').invoke('val', '');
    cy.get('#btnNext').click();
    cy.assertNoFatalPageError();
    cy.get('body').invoke('text').should('match', /địa chỉ|dia chi|vị trí|vi tri|map|tọa độ|toa do/i);
  });

  it('[TC531][MD26][P0] OwnerCars image upload minimum validation', () => {
    cy.loginAs('owner');
    cy.safeVisit('/OwnerCars/Create');
    cy.get('#imageInput').selectFile(carImageFiles(3), { force: true });
    cy.get('#imageCount').should('contain.text', '3/4');
    cy.get('body').invoke('text').should('match', /4 ảnh|4 anh|tối thiểu|toi thieu/i);
  });

  it('[TC532][MD26][P1] OwnerCars image preview accepts 4 sandbox files', () => {
    cy.loginAs('owner');
    cy.safeVisit('/OwnerCars/Create');
    cy.get('#imageInput').selectFile(carImageFiles(4), { force: true });
    cy.get('#imageCount').should('contain.text', '4/4');
    cy.get('#imagePreviewGrid .upload-preview-card').should('have.length', 4);
  });

  it('[TC533][MD26][P0] Admin car amenities show existing selections', () => {
    cy.seedScenario('admin-amenities').then((scenario) => {
      cy.loginAs('admin');
      cy.safeVisit(`/Admin/Car/Edit/${scenario.carId}`);
      cy.get('body').should('contain.text', scenario.amenityName);
      cy.get(`input[name="AmenityIDs"][value="${scenario.amenityId}"]`).should('be.checked');
    });
  });

  it('[TC534][MD26][P0] Admin car amenities add a new feature', () => {
    const featureName = `Cypress Live Feature ${Date.now()}`;
    cy.seedScenario('admin-amenities').then((scenario) => {
      cy.loginAs('admin');
      cy.safeVisit(`/Admin/Car/Edit/${scenario.carId}`);
      cy.get('#btnAddFeature').click({ force: true });
      cy.get('input[name="NewAmenityNames"]').last().clear().type(featureName);
      cy.get('input[name="NewAmenityNames"]').last().closest('form').then(($form) => {
        const body = {};
        const formData = new FormData($form[0]);
        formData.forEach((value, key) => {
          if (Object.prototype.hasOwnProperty.call(body, key)) {
            body[key] = Array.isArray(body[key]) ? [...body[key], value] : [body[key], value];
          } else {
            body[key] = value;
          }
        });
        cy.request({
          method: 'POST',
          url: $form.attr('action') || `/Admin/Car/Edit/${scenario.carId}`,
          form: true,
          failOnStatusCode: false,
          body,
        }).its('status').should('be.lessThan', 500);
      });
      cy.assertNoFatalPageError();
      cy.safeVisit(`/Admin/Car/Details/${scenario.carId}`);
      cy.get('body').should('contain.text', featureName);
    });
  });

  it('[TC535][MD26][P0] Admin car amenities edit sync and details display', () => {
    cy.seedScenario('admin-amenities').then((scenario) => {
      cy.loginAs('admin');
      cy.safeVisit(`/Admin/Car/Details/${scenario.carId}`);
      cy.get('body').should('contain.text', scenario.amenityName);
      cy.safeVisit(`/Admin/Car/Edit/${scenario.carId}`);
      cy.get(`input[name="AmenityIDs"][value="${scenario.amenityId}"]`).should('exist');
    });
  });

  it('[TC536][MD26][P0] Delivery quote missing coordinates is blocked', () => {
    cy.seedScenario('delivery-tracking').then((scenario) => {
      cy.loginAs('customer');
      cy.request({
        url: `/Booking/Checkout?CarID=${scenario.carId}&StartDate=${encodeURIComponent(futureIso(210))}&EndDate=${encodeURIComponent(futureIso(212))}&PickupLocation=${encodeURIComponent('Cypress Customer Dropoff')}&pickupLocationType=owner`,
        failOnStatusCode: false,
      }).then((response) => {
        expect(response.status).to.be.lessThan(500);
        expect(`${response.body}`).to.match(/tọa độ|toa do|vị trí|vi tri|không thể|khong the|Checkout/i);
      });
    });
  });

  it('[TC537][MD26][P0] Delivery quote out of range is blocked', () => {
    cy.seedScenario('delivery-tracking').then((scenario) => {
      cy.request({
        url: `/Car/CalculateDelivery?carId=${scenario.carId}&customerLat=21.0285&customerLng=105.8542`,
        failOnStatusCode: false,
      }).then((response) => {
        expect(response.status).to.eq(200);
        expect(response.body.success).to.eq(false);
      });
    });
  });

  it('[TC538][MD26][P0] Delivery checkout fee is server recomputed', () => {
    cy.seedScenario('delivery-tracking').then((scenario) => {
      cy.loginAs('customer');
      cy.request({
        url: `/Booking/Checkout?CarID=${scenario.carId}&StartDate=${encodeURIComponent(futureIso(220))}&EndDate=${encodeURIComponent(futureIso(222))}&PickupLocation=${encodeURIComponent('Cypress Customer Dropoff')}&pickupLocationType=owner&deliveryLatitude=${scenario.deliveryLatitude}&deliveryLongitude=${scenario.deliveryLongitude}&DeliveryFee=999999`,
        failOnStatusCode: false,
      }).then((response) => {
        expect(response.status).to.eq(200);
        expect(`${response.body}`).to.match(/giao xe|delivery|phí|phi/i);
        expect(`${response.body}`).not.to.include('999,999');
      });
    });
  });

  it('[TC539][MD26][P0] Customer tracking map shows fallback or live state honestly', () => {
    cy.seedScenario('delivery-tracking').then((scenario) => {
      cy.loginAs('customer');
      cy.safeVisit(`/Tracking/Booking/${scenario.bookingId}`);
      cy.request(`/Tracking/Snapshot?bookingId=${scenario.bookingId}`).then((response) => {
        expect(response.status).to.eq(200);
        expect(response.body.trackingStatus).to.be.oneOf(['live', 'stale', 'fallback']);
        expect(response.body.current).to.have.property('lat');
        expect(response.body.destination).to.have.property('lat');
      });
    });
  });

  it('[TC540][MD26][P0] Mobile tracker posts GPS and snapshot updates', () => {
    cy.seedScenario('delivery-tracking').then((scenario) => {
      cy.loginAs('staff');
      cy.postMobileTracking(scenario.bookingId, { lat: 10.7775, lng: 106.7025, speed: 18 });
      cy.loginAs('customer');
      cy.request(`/Tracking/Snapshot?bookingId=${scenario.bookingId}`).then((response) => {
        expect(response.status).to.eq(200);
        expect(response.body.trackingStatus).to.eq('live');
        expect(response.body.current.lat).to.be.closeTo(10.7775, 0.0005);
      });
    });
  });

  it('[TC541][MD26][P0] Tracking stale GPS warning', () => {
    cy.seedScenario('tracking-stale').then((scenario) => {
      cy.loginAs('customer');
      cy.request(`/Tracking/Snapshot?bookingId=${scenario.bookingId}`).then((response) => {
        expect(response.status).to.eq(200);
        expect(response.body.trackingStatus).to.be.oneOf(['stale', 'fallback']);
      });
    });
  });

  it('[TC542][MD26][P0] Handover capture stores geolocation', () => {
    cy.seedScenario('delivery-tracking').then((scenario) => {
      cy.stubBrowserGeolocation(10.7757, 106.7019);
      cy.loginAs('customer');
      cy.safeVisit(`/Handover/Capture?bookingId=${scenario.bookingId}&type=Pickup`);
      cy.get('#geoLatitude', { timeout: 10000 }).should(($input) => {
        expect(Number($input.val())).to.be.closeTo(10.7757, 0.000001);
      });
      cy.get('#geoLongitude').should(($input) => {
        expect(Number($input.val())).to.be.closeTo(106.7019, 0.000001);
      });
    });
  });

  it('[TC543][MD26][P1] Traceability report includes Cypress addendum', () => {
    cy.fixture('testcase-addendum').then((addendum) => {
      expect(addendum.cases.map((testCase) => testCase.id)).to.include.members(['TC526', 'TC543']);
      expect(addendum.modules[0].code).to.eq('MD26');
      expect(addendum.cases).to.have.length(18);
    });
  });
});
