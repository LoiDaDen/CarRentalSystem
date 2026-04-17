describe('Admin Panel Flow', () => {

  beforeEach(() => {
    // Admin Role Login (Demo data: admin@test.com)
    cy.login('hloi78274@gmail.com', 'Admin@123');
  });

  it('1. Có thể truy cập Admin Dashboard toàn quyền', () => {
    cy.visit('/Admin/Dashboard');
    cy.url().should('include', '/Admin');
    
    // Validate thẻ doanh thu và top xe
    cy.get('.card').should('exist');
    cy.get('body').should('contain.text', 'Doanh thu')
      .or('contain.text', 'Revenue');
  });

  it('2. Phê duyệt hệ thống (System Lookups)', () => {
    cy.visit('/Admin/Lookups');
    cy.url().should('include', '/Lookups');

    cy.get('table').should('exist');
    // Expect the status lists
  });
  
  it('3. Quản lý Thuê xe (Bookings)', () => {
    cy.visit('/Admin/Booking');
    cy.get('table.table').should('have.length.greaterThan', 0);
  });
});
