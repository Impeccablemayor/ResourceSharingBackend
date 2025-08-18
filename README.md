# 📚 Academic Resource Sharing & Lending API

This is the backend API for the Academic Resource Sharing & Lending App, built with ASP.NET Core and integrated with Cloudinary for file uploads. It allows users to upload academic resources, view listings, borrow hardcover resources, and receive real-time notifications.

---

## 🚀 Base URL

> **Production:** `https://resourcesharingbackend.onrender.com`  

### 📤 Upload Resource

**POST** `/upload`  
🔐 **Requires Authentication**

Uploads a new academic resource. If the resource type is **Hardcover**, it may also include an image and physical location.

#### 🔸 Request (multipart/form-data):

| Field             | Type       | Required | Description                         |
|------------------|------------|----------|-------------------------------------|
| `Title`           | string     | ✅        | Resource title                      |
| `CourseCode`      | string     | ✅        | E.g., CSC301                        |
| `Author`          | string     | ✅        | Name of author                      |
| `Format`          | string     | ✅        | PDF, DOCX, etc.                     |
| `Department`      | string     | ✅        | E.g., Computer Science              |
| `Level`           | string     | ✅        | 100 - 500 level                     |
| `Type`            | string     | ❌        | "Softcopy" or "Hardcover"           |
| `Description`     | string     | ❌        | Brief details                       |
| `File`            | file       | ✅        | The main resource file              |
| `Image`           | file       | ❌        | Only for hardcover type             |
| `PhysicalLocation`| string     | ❌        | Where the hardcover is kept         |
| `MeetupLocation`  | string     | ❌        | Where borrower can collect it       |

#### ✅ Response:
```json
{
  "message": "Resource uploaded successfully!",
  "url": "https://res.cloudinary.com/.../resource.pdf"
}
📚 Get All Resources
GET /
🔓 Public

Fetches a list of all uploaded resources (softcopy and hardcover).

✅ Response:
json

[
  {
    "id": 1,
    "title": "Introduction to AI",
    "courseCode": "CSC501",
    "author": "Dr. Jane",
    "format": "PDF",
    "department": "CS",
    "level": "500",
    "type": "Softcopy",
    "description": "Fundamentals of AI",
    "fileUrl": "...",
    "uploadedAt": "2025-08-01T12:00:00Z",
    "imageUrl": null
  }
]
📘 Get Resource By ID
GET /{id}
🔐 Requires Authentication

Returns detailed information about a specific resource. For hardcover resources, physical and meetup locations are only shown to the uploader or approved borrower.

✅ Response:
json

{
  "id": 1,
  "title": "Intro to AI",
  "fileUrl": "...",
  "physicalLocation": "Library shelf 3A",
  "meetupLocation": "CSC Department",
  ...
}
🔔 Get Notifications
GET /notifications
🔐 Requires Authentication

Fetches all notifications for the logged-in user.

✅ Response:
json

[
  {
    "id": 10,
    "message": "Your resource 'Intro to AI' was uploaded successfully.",
    "isRead": false,
    "createdAt": "2025-08-04T12:00:00Z"
  }
]
📩 Request to Borrow Hardcover
POST /{resourceId}/borrow
🔐 Requires Authentication

Submits a borrow request for a hardcover resource.

✅ Response:
json

{
  "message": "Borrow request submitted."
}
💡 Only one pending request is allowed per resource per user.

✅ Approve Borrow Request
POST /borrow/{transactionId}/approve
🔐 Uploader Only

Approves a borrow request. Other pending requests will be automatically rejected.

✅ Response:
json

{
  "message": "Borrow request approved."
}

📜 View Borrow Requests for a Resource
GET /{resourceId}/borrow-requests
🔐 Uploader Only

Retrieves all borrow requests made for a specific resource.

✅ Response:
json

[
  {
    "id": 15,
    "resourceId": 1,
    "resourceTitle": "Intro to AI",
    "borrowerId": "f1234...",
    "borrowerName": "John Doe",
    "borrowerEmail": "john@example.com",
    "status": "Pending",
    "requestDate": "2025-08-02T10:00:00Z"
  }
]
