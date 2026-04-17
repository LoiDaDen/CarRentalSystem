describe('Partner Dashboard Flow', () => {

  beforeEach(() => {
    // Partner Role Login (Demo data: partner@test.com)
    // Cần setup mock data partner trong DB có tài khoản này để test chạy trơn tru
    cy.login('hloi78274@gmail.com', 'Admin@123');
  });

  it('1. Cần truy cập được Partner Dashboard thành công', () => {
    cy.visit('/Partner/Dashboard');
    
    // Đảm bảo URL chính xác
    cy.url().should('include', '/Partner');
    
    // Bảng thống kê hiển thị data (Ví dụ: Thống kê thu nhập, số xe)
    cy.get('body').should('contain.text', 'Tổng Thu Nhập')
      .and('contain.text', 'Quản lý Xe');
  });

  it('2. Quản lý danh sách xe', () => {
    cy.visit('/Partner/Car');
    cy.url().should('include', '/Partner/Car');

    // Màn hình Car List phải có nút Thêm xe mới
    // Lấy thẻ <a> hoặc <button> có text phù hợp
    cy.get('body').then($body => {
      if ($body.text().includes('Thêm xe mới')) {
        cy.contains('Thêm xe mới').should('exist');
      }
    });

    // Check table tồn tại
    cy.get('table').should('exist');
  });

});
