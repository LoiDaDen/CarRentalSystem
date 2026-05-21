describe('05 Owner cars and approval workflow', () => {
  let seed;

  before(() => {
    cy.resetTestData().then((body) => {
      seed = body;
    });
  });

  it('routes legacy partner/driver links into the OwnerCars module', () => {
    cy.loginAs('owner');
    cy.safeVisit('/OwnerCars');
    cy.safeVisit('/OwnerCars/Create');

    cy.safeVisit('/Partner/Dashboard');
    cy.url().should('include', '/OwnerCars');

    cy.safeVisit('/Driver/Dashboard');
    cy.url().should('include', '/OwnerCars');
  });

  it('validates owner car creation before accepting an approval submission', () => {
    cy.loginAs('owner');
    cy.safeVisit('/OwnerCars/Create');

    cy.get('select[name="BrandID"] option[value!=""]').first().then(($brand) => {
      const brandId = $brand.val();

      cy.postForm('/OwnerCars/Create', {
        LicensePlate: `CYP-${Date.now().toString().slice(-5)}`,
        BrandID: brandId,
        Seats: 5,
        PricePerDay: 650000,
        CarAddress: 'Cypress validation depot',
        CityOrProvince: 'Ho Chi Minh',
        Transmission: 'Automatic',
        FuelType: 'Gasoline',
        Description: 'Validation path without required image files.',
      }).then((response) => {
        expect(response.status).to.eq(200);
        expect(response.body).to.match(/upload|ảnh|image|ImageFiles/i);
      });
    });
  });

  it('lets admin approve a pending owner vehicle and keeps the owner view healthy', () => {
    cy.loginAs('admin');
    cy.safeVisit('/Admin/CarApproval');
    cy.get('body').invoke('text').should('match', /CYP-00001|Pending|duy/i);

    cy.postForm('/Admin/CarApproval/Approve', { id: seed.pendingOwnerCarId }).then((response) => {
      expect(response.status).to.be.oneOf([200, 302]);
    });

    cy.safeVisit('/Admin/CarApproval');
    cy.get('body').invoke('text').should('not.include', 'CYP-00001');

    cy.loginAs('owner');
    cy.safeVisit('/OwnerCars');
    cy.get('body').invoke('text').should('include', 'CYP-00001');
  });
});
