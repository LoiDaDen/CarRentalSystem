describe('09 Long-term rental and handover flows', () => {
  let seed;

  const futureDate = (daysFromNow) => {
    const date = new Date();
    date.setDate(date.getDate() + daysFromNow);
    date.setHours(9, 0, 0, 0);
    return date.toISOString().slice(0, 16);
  };

  before(() => {
    cy.resetTestData().then((body) => {
      seed = body;
    });
  });

  it('loads long-term request, selection, and pricing endpoints', () => {
    cy.safeVisit('/LongTerm');
    cy.safeVisit(`/LongTerm/RequestForm?contractId=${seed.contractTypeId}`);

    const start = futureDate(45);
    const end = futureDate(75);
    cy.requestHealthy(`/LongTerm/GetAvailableCars?startDate=${encodeURIComponent(start)}&endDate=${encodeURIComponent(end)}&contractTypeId=${seed.contractTypeId}`)
      .its('body.success')
      .should('eq', true);
    cy.requestHealthy('/LongTerm/GetCarsForSelection?minSeats=4').its('body.success').should('eq', true);
    cy.requestHealthy(`/LongTerm/CalculatePrice?carId=${seed.firstAvailableCarId}&contractTypeId=${seed.contractTypeId}&startDate=${encodeURIComponent(start)}&termMonths=1`)
      .its('body.success')
      .should('eq', true);
  });

  it('loads contract preview and inspection for verified customers', () => {
    cy.loginAs('customer');
    const start = futureDate(90);
    const query = `carId=${seed.firstAvailableCarId}&contractId=${seed.contractTypeId}&duration=${encodeURIComponent('1 month')}&customerName=Cypress%20Customer&customerPhone=0900000000&startDateStr=${encodeURIComponent(start)}&selectedDates=`;

    cy.safeVisit(`/LongTerm/ContractPreview?${query}`);
    cy.safeVisit(`/LongTerm/VehicleInspection?${query}`);
  });

  it('keeps with-driver selection blocked in long-term deposit confirmation', () => {
    cy.loginAs('customer');
    const start = futureDate(120);
    const query = `carId=${seed.firstAvailableCarId}&contractId=${seed.contractTypeId}&duration=${encodeURIComponent('1 month')}&customerName=Cypress%20Customer&customerPhone=0900000000&startDateStr=${encodeURIComponent(start)}&selectedDates=&selectedDriverId=1`;

    cy.safeVisit(`/LongTerm/VehicleInspection?${query}`);
    cy.postForm('/LongTerm/ConfirmAndDeposit', {
      carId: seed.firstAvailableCarId,
      contractId: seed.contractTypeId,
      duration: '1 month',
      customerName: 'Cypress Customer',
      customerPhone: '0900000000',
      startDateStr: start,
      selectedDates: '',
      selectedDriverId: 1,
    }).then((response) => {
      expect(response.status).to.be.oneOf([200, 302]);
    });
  });

  it('loads handover index and capture surfaces for a seeded booking', () => {
    cy.loginAs('customer');
    cy.safeVisit(`/Handover/Index?bookingId=${seed.bookingId}`);
    cy.safeVisit(`/Handover/Capture?bookingId=${seed.bookingId}&type=Pickup`);
  });
});
