# CarRentalSystem - Release Backlog (Product Backlog)

## Template Legend
- **STORY / FEATURE / REQUEST** format: As a/an [User role] | I want to [Goal] | So that [reason]
- **Priority**: 1=Critical, 2=High, 3=Medium, 4=Low
- **State**: New | In Progress | Done | Removed

---

## PB01 - Quản lý Tin Nhắn (SMS/Notification Management)

| Backlog ID | Backlog | Story / Feature / Request | Story ID | Priority | Business Value | Sprint# | State | Note |
|---|---|---|---|---|---|---|---|---|
| PB01 | Quản lý Tin Nhắn | As a **User** (Admin/Partner) | Set the start time, end time, and content will be sent SMS messages | RQ04 | 1 | High | 1 | New | Remind customers about car maintenance schedule |
| PB01 | Quản lý Tin Nhắn | As a **User** (Admin/Partner) | Have the program which automatically send a message each day | RQ05 | 1 | High | 1 | New | Announce to customers who meet conditions in RQ04 and have not received messages |
| PB01 | Quản lý Tin Nhắn | As a **User** (Admin/Partner) | Send the message to all my customer | RQ06 | 1 | High | 1 | New | Send message again when there is an error |
| PB01 | Quản lý Tin Nhắn | As a **User** (Admin/Partner) | Review the history of messages sent to customers | RQ10 | 3 | Medium | 1 | New | Track message delivery status and program effectiveness |

---

## PB02 - Quản lý Thông Tin Khách Hàng (Customer Management)

| Backlog ID | Backlog | Story / Feature / Request | Story ID | Priority | Business Value | Sprint# | State | Note |
|---|---|---|---|---|---|---|---|---|
| PB02 | Quản lý Thông Tin Khách Hàng | As a **User** (Admin) | Print a list of customers coming to the birthday | RQ07 | 3 | High | 1 | New | Send birthday cards or birthday messages |
| PB02 | Quản lý Thông Tin Khách Hàng | As a **User** (Admin/Partner) | View my customer's information like SMS Template.xls | RQ02 | 2 | Medium | 1 | New | Manage customer information who rent cars |
| PB02 | Quản lý Thông Tin Khách Hàng | As a **User** (Admin/Partner) | Import my customer's information from Excel file (like SMS Template.xls) | RQ03 | 2 | High | 1 | New | Create list of customer information quickly |

---

## PB03 - Quản lý Xe (Vehicle Management)

| Backlog ID | Backlog | Story / Feature / Request | Story ID | Priority | Business Value | Sprint# | State | Note |
|---|---|---|---|---|---|---|---|---|
| PB03 | Quản lý Xe | As a **User** (Admin/Partner) | View my motorbike's information include: categories, colors, price, inventory | RQ08 | 3 | High | 1 | New | Manage motorbike information in the system |

---

## New Features (Mapped to CarRentalSystem)

| Backlog ID | Backlog | Story / Feature / Request | Story ID | Priority | Business Value | Sprint# | State | Note |
|---|---|---|---|---|---|---|---|---|
| PB04 | Báo Cáo & Thống Kê | As a **Admin** | View dashboard with KPIs (revenue, bookings, active vehicles) | RQ11 | 1 | High | 1 | New | System overview and business health check |
| PB05 | Quản lý Hợp Đồng | As a **Admin/Partner** | Generate and manage rental contracts (PDF export) | RQ12 | 1 | High | 1 | New | Digitize contract workflow |
| PB06 | Thanh Toán | As a **User** | Process online payment via payOS/VNPay | RQ13 | 1 | High | 1 | New | Multiple payment gateway support |
| PB07 | Đánh Giá & Phản Hồi | As a **Customer** | Rate and review completed bookings | RQ14 | 2 | Medium | 1 | New | Build trust and service quality tracking |
| PB08 | Thông Báo Push | As a **Customer** | Receive push notifications for booking status changes | RQ15 | 2 | Medium | 1 | New | Real-time booking updates |
| PB09 | Bảo Trì Xe | As a **Partner/Admin** | Schedule and track vehicle maintenance reminders | RQ16 | 2 | Medium | 1 | New | Preventive maintenance tracking |
| PB10 | Tích Hợp GPS | As a **Admin** | Track vehicle location in real-time | RQ17 | 3 | Medium | 1 | New | Fleet management and theft prevention |

---

## Sprint Planning Summary

### Sprint 1 - MVP (4 weeks)

| PBI | Priority | Story Points | Description |
|---|---|---|---|
| PB01-RQ04 | 1 | 3 | SMS scheduling (start/end time, content config) |
| PB01-RQ05 | 1 | 5 | Auto-send SMS daily based on conditions |
| PB01-RQ06 | 1 | 2 | Bulk resend SMS on error |
| PB02-RQ03 | 2 | 5 | Import customer data from Excel |
| PB02-RQ02 | 2 | 3 | View customer list with filters |
| PB02-RQ07 | 3 | 2 | Birthday customer report & print |
| PB01-RQ10 | 3 | 3 | SMS history & delivery status tracking |
| PB03-RQ08 | 3 | 3 | Vehicle information management (categories, colors, price, inventory) |
| PB04-RQ11 | 1 | 5 | Admin dashboard with KPIs |
| PB05-RQ12 | 1 | 8 | Contract generation and management |
| PB06-RQ13 | 1 | 5 | Online payment integration |
| PB07-RQ14 | 2 | 3 | Booking rating and review system |
| PB08-RQ15 | 2 | 5 | Push notification for booking status |
| PB09-RQ16 | 2 | 5 | Vehicle maintenance scheduling |
| PB10-RQ17 | 3 | 8 | GPS tracking integration |

**Total Sprint 1 Story Points: ~68**

---

## Product Goal
Deliver a fully functional car rental management system with customer management, SMS notification automation, vehicle inventory, and online payment in the first sprint.

## Definition of Done (DoD)
- Code compiles without errors
- Unit tests pass
- Code review approved
- Functionality tested on Development environment
- Documentation updated
- No critical/High severity bugs open

## Definition of Ready (DoR)
- User story has acceptance criteria
- Dependencies identified
- Design/UI mockups approved
- Technical approach documented
