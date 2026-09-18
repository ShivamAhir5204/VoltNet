# Application Flow (`flow.md`)

This document details the end-to-end operational, request-response, and user interaction flows for the newly implemented features in the **Station Owner** module of VoltNet.

---

## 1. Station Management Flows

### 1.1 Station Edit Flow

```
[Owner on /owner/stations]
       │
       ▼  Clicks "Edit" button
[GET: /owner/stations/Edit/{id}]
       │
       ├──► Verify: OwnerUserId == Current User ID
       │        ├── No  ──► [404 NotFound / 401 Unauthorized]
       │        └── Yes ──► Render EditStation.cshtml with prefilled model
       │
[Owner interacts with Edit form]
       │
       ├──► Optional: Click "Pick on Map"
       │        ├──► [GET: /owner/stations/PickLocation?returnTo=Edit&id={id}]
       │        ├──► Map opens with existing coords or owner geolocation
       │        ├──► Owner clicks marker position & clicks "Confirm"
       │        └──► Coordinates stored in sessionStorage; Redirects back to Edit view
       │
       ▼  Submits form (POST /owner/stations/Edit)
[POST: /owner/stations/Edit]
       │
       ├──► Check [ValidateAntiForgeryToken]
       ├──► Verify station ownership (OwnerUserId == Current User ID)
       ├──► Validate ModelState (required strings, valid times, valid coordinates)
       │        ├── Invalid ──► Re-render EditStation.cshtml with validation messages
       │        └── Valid   ──► Update fields:
       │                         - Name, Address, City, State, Lat, Lng, OpeningTime, ClosingTime
       │                         - If Status == "Rejected" -> Status = "PendingVerification"
       │                         - Save to database
       │                         - Set TempData["Success"]
       │                         - RedirectToAction("Index")
       ▼
[Owner returns to /owner/stations with success alert badge]
```

---

### 1.2 Station Delete Flow

```
[Owner on /owner/stations]
       │
       ▼  Clicks "Delete" button
[JavaScript Confirmation Prompt: "Are you sure you want to delete station '{name}'?"]
       │
       ├── Cancelled ──► No action taken
       │
       └── Confirmed ──► Submits POST form with AntiForgeryToken
                              │
                              ▼
               [POST: /owner/stations/Delete]
                              │
               ├──► Verify station exists & OwnerUserId == Current User ID
               │        └── False ──► Return 404 NotFound
               │
               ├──► Check active foreign constraints:
               │        `_context.StationManagers.AnyAsync(m => m.StationId == id)`
               │        │
               │        ├── Has Managers ──► Set TempData["Error"] = "Cannot delete station with assigned managers"
               │        │                   └── RedirectToAction("Index")
               │        │
               │        └── No Managers  ──► Remove Station from DB
               │                             Save changes
               │                             Set TempData["Success"]
               │                             RedirectToAction("Index")
                              ▼
               [Owner returns to /owner/stations with success/error alert]
```

---

## 2. Station Manager Management Flows

### 2.1 Station Manager Edit Flow

```
[Owner on /owner/station-managers]
       │
       ▼  Clicks "Edit" button
[GET: /owner/station-managers/Edit/{id}]
       │
       ├──► Verify: Manager.CreatedBy == Logged-in StationOwner ID
       │        ├── No  ──► [404 NotFound / 401 Unauthorized]
       │        └── Yes ──► Load manager & assignable active stations for this owner
       │                    Render EditStationManager.cshtml
       │
[Owner updates FullName, Phone, or Assigned Station]
       │
       ▼  Clicks "Save Changes"
[POST: /owner/station-managers/Edit]
       │
       ├──► Verify AntiForgeryToken
       ├──► Check ownership (`CreatedBy == ownerId`)
       ├──► Validate input fields:
       │        - Full Name not empty
       │        - Phone not empty, length == 10, digits only
       │        - Assigned Station belongs to this owner
       │        ├── Failed ──► Set ViewBag.Error & re-render view
       │        └── Passed ──► Update StationManager:
       │                         - FullName, Phone, StationId
       │                       Update UserMaster:
       │                         - Fullname, Mobile
       │                       Save changes to DB
       │                       Set TempData["Success"]
       │                       RedirectToAction("Index")
       ▼
[Owner returns to /owner/station-managers with updated info]
```

---

### 2.2 Station Manager Delete Flow

```
[Owner on /owner/station-managers]
       │
       ▼  Clicks "Delete" button
[JavaScript Confirmation: "Are you sure you want to permanently delete manager '{name}'?"]
       │
       ├── Cancelled ──► No action
       │
       └── Confirmed ──► Submits POST form
                              │
                              ▼
               [POST: /owner/station-managers/Delete]
                              │
               ├──► Verify AntiForgeryToken
               ├──► Verify manager exists & CreatedBy == ownerId
               │        └── False ──► Return 404 NotFound
               │
               ├──► Cascade deletion:
               │        1. _context.StationManagers.Remove(manager)
               │        2. if (manager.User != null) _context.UserMasters.Remove(manager.User)
               │        3. await _context.SaveChangesAsync()
               │
               ├──► Set TempData["Success"] = "Station Manager '{name}' deleted successfully."
               └──► RedirectToAction("Index")
                              ▼
               [Owner returns to /owner/station-managers list]
```

---

## 3. Mobile Table Interaction Flow (`.table-responsive-cards`)

```
[User opens portal page on Mobile screen (<= 768px)]
       │
       ▼
[CSS activates media query in custom.css]
       │
       ├── thead hidden (`display: none !important`)
       ├── Each <tr> rendered as a raised card with rounded borders and shadow
       ├── td.col-primary displays entity name + chevron button (.mobile-expand-btn)
       └── td.col-secondary and td.col-action initially collapsed (`display: none`)
       │
       ▼
[User taps .mobile-expand-btn on a row card]
       │
       ├── custom.js catches click event
       ├── Toggles `.expanded` class on the parent <tr>
       ├── Changes icon from `data-feather="chevron-down"` to `chevron-up`
       ├── `feather.replace()` re-renders SVG icons
       └── td.col-secondary & td.col-action become `display: flex !important`
           (Showing Address, City, State, Status, Opening Hours, and Action buttons)
       │
       ▼
[User interacts with Action buttons (Edit, View, Deactivate, Delete) directly from the card]
```
