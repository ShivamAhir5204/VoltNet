# VoltNet — Project Specification

## 1. Project Definition

**VoltNet** is a web-based EV Charging Station Management System that connects customers, station owners, station managers, and administrators on a single platform.

New users register as **Customers**. A customer who wants to become a Station Owner can apply through the **Become Partner** page by submitting the required details. The application is reviewed by an Admin, who can approve or reject it. After approval, the customer's role changes to **Station Owner**.

A Station Owner must have at least one station and can create multiple stations and Station Managers. Station Managers handle the day-to-day operations of their assigned stations.

Customers can find charging stations, view charger availability, book charging slots, use charging sessions, make direct payments, and view invoices.

**There is no wallet system in VoltNet.**

---

# 2. Functional Requirements

### FR-01 — Authentication & Authorization

* User registration and login.
* Every newly registered user is assigned the Customer role.
* Role-based access control.
* Secure logout and password management.

### FR-02 — User Management

* Super Admin can create and manage Admin accounts.
* Admin can view/manage users according to permissions.
* Users can manage their own profiles.

### FR-03 — Become Partner

* Customer can access the Become Partner page.
* Customer can fill and submit the partner application form.
* Customer can view application status.
* Admin can review submitted applications.
* Admin can approve or reject applications.
* Admin can provide rejection/verification remarks.
* Approved Customer becomes Station Owner.

### FR-04 — Station Management

* Station Owner can create stations.
* Station Owner can edit and manage their stations.
* Station Owner can have multiple stations.
* A Station Owner must have at least one station.
* Station details include location, address, operating information, and status.

### FR-05 — Charger Management

* Add chargers to stations.
* Edit charger details.
* View charger availability and status.
* Manage charger operational status.
* Chargers belong to a specific station.

### FR-06 — Station Manager Management

* Station Owner can create Station Managers.
* Station Owner can assign managers to their stations.
* Station Manager can access only assigned stations.
* Station Manager can manage daily station operations.

### FR-07 — Customer & Vehicle Management

* Customers can manage their profiles.
* Customers can add and manage vehicles.
* Vehicle information can include number, brand, model, battery capacity, and connector type.

### FR-08 — Station Search & Map

* Customers can search charging stations.
* Display stations on a map.
* Show station and charger information.
* Show charger availability.
* Allow customers to select a station for booking.

### FR-09 — Booking Management

* Customer can select a station and charger.
* Customer can select an available time slot.
* System prevents overlapping bookings.
* Customer can view and cancel bookings according to system rules.
* Booking history is maintained.

### FR-10 — Charging Session

* Start and end charging sessions.
* Record charging start/end time.
* Record energy consumed.
* Calculate charging cost.
* Update charger status automatically.
* Charging history is maintained.

### FR-11 — Payment & Invoice

* Customer makes direct payment for charging.
* No wallet or wallet recharge functionality.
* Track payment status.
* Calculate applicable GST.
* Generate invoices.
* Customer can view/download invoice history.

### FR-12 — Maintenance

* Station Manager/Admin can report charger issues.
* Create and track maintenance tickets.
* Track issue description, priority, and status.
* Update charger status during maintenance.
* Maintain maintenance history.

### FR-13 — Reports & Analytics

* Dashboard with relevant KPIs.
* Daily, weekly, and monthly reports.
* Station-wise revenue reports.
* Booking and charging-session reports.
* Charger utilization reports.
* Maintenance reports.
* Station Owner can view reports for their own stations.
* Super Admin/Admin can view platform-level reports according to permissions.

### FR-14 — Notifications

The system can provide notifications for:

* Partner request status.
* Booking confirmation/cancellation.
* Payment confirmation.
* Invoice generation.
* Maintenance updates.

---

# 3. Non-Functional Requirements

### NFR-01 — Security

* Secure authentication.
* Passwords must be securely hashed.
* Role-based authorization must be enforced.
* Users must not access unauthorized data.
* Server-side validation must be implemented.
* Sensitive information must not be hardcoded.

### NFR-02 — Performance

* Pages should load quickly.
* Database queries should be optimized.
* Large records should support pagination.
* Reports should be generated efficiently.

### NFR-03 — Reliability

* System should handle invalid input safely.
* Errors should be handled without crashing the application.
* Booking and payment operations should maintain consistent data.
* Important system activities should be logged.

### NFR-04 — Usability

* Clean and simple user interface.
* Responsive design.
* Role-specific navigation and dashboards.
* Clear validation and error messages.
* Search, filtering, and pagination where required.

### NFR-05 — Maintainability

* Use clean and modular code.
* Separate business logic from UI.
* Use reusable services/components.
* Follow consistent coding and naming conventions.

### NFR-06 — Scalability

* System should support multiple Station Owners.
* Each Station Owner can manage multiple stations.
* Each station can contain multiple chargers.
* System should support increasing numbers of customers and bookings.

### NFR-07 — Data Integrity

* Prevent duplicate/overlapping bookings.
* Maintain correct payment and invoice records.
* Maintain proper relationships between owners, stations, managers, chargers, bookings, and sessions.
* Enforce role and ownership restrictions on server side.

---

# 4. Roles Description

| Role                | Description                                                                                           |
| ------------------- | ----------------------------------------------------------------------------------------------------- |
| **Super Admin**     | Highest-level user who controls the complete VoltNet platform and creates/manages Admin accounts.     |
| **Admin**           | Handles platform operations and verifies Customer requests to become Station Owners.                  |
| **Station Owner**   | Approved partner who owns one or more EV charging stations and manages stations and Station Managers. |
| **Station Manager** | Manages day-to-day operations of stations assigned by a Station Owner.                                |
| **Customer**        | End user who searches stations, books chargers, charges vehicles, makes payments, and views invoices. |

---

# 5. Roles Works

## Super Admin

* Login to Super Admin Dashboard.
* Create Admin accounts.
* Manage Admin accounts.
* View all users.
* View all Station Owners.
* View all stations and chargers.
* View overall bookings and charging sessions.
* View platform-wide reports and analytics.
* Monitor overall system activities.

**Access:** Complete platform.

---

## Admin

* Login to Admin Dashboard.
* View Customers.
* View Become Partner requests.
* Review partner application details.
* Approve or reject partner requests.
* Add verification/rejection remarks.
* Approved Customer becomes Station Owner.
* Monitor stations, chargers, bookings, payments, maintenance, and reports according to permissions.

**Access:** Platform operations assigned by Super Admin.

---

## Station Owner

* Login to Station Owner Dashboard.
* Manage own profile.
* Create the first station.
* Create additional stations.
* Manage own stations.
* Add/manage chargers.
* Create Station Managers.
* Assign managers to stations.
* View bookings and charging sessions of own stations.
* View revenue and performance reports.
* View maintenance information.

**Access:** Own stations and their related data only.

---

## Station Manager

* Login to Station Manager Dashboard.
* View assigned stations.
* Monitor charger status.
* Monitor bookings.
* Monitor active charging sessions.
* Handle station-level operations.
* Report charger issues.
* Create/track maintenance requests.
* View station-level reports.

**Access:** Assigned stations only.

---

## Customer

* Register and login.
* Manage profile.
* Add/manage vehicles.
* Search charging stations.
* View station and charger details.
* Check charger availability.
* Book charging slots.
* Start/end charging sessions.
* View charging history.
* Make direct payments.
* View/download invoices.
* Apply to become a Station Owner through Become Partner.
* Track partner application status.

**Access:** Own customer data and publicly available station information.
