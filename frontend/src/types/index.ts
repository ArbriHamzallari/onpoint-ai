// ─── Auth ────────────────────────────────────────────────────────────────────

export interface LoginRequest {
  email: string
  password: string
}

export interface RegisterRequest {
  email: string
  password: string
  fullName: string
  businessName: string
  businessType: string
  timezone?: string
}

export interface AuthResponse {
  userId: string
  businessId: string
  accessToken: string
}

// ─── Dashboard ───────────────────────────────────────────────────────────────

export interface DashboardStats {
  activeIssues: number
  resolvedToday: number
  totalActiveSessions: number
  averageRating: number
}

// ─── Issues ──────────────────────────────────────────────────────────────────

export type IssueStatus = 'open' | 'assigned' | 'in_progress' | 'resolved' | 'cancelled'
export type IssuePriority = 'low' | 'medium' | 'high' | 'urgent'

// Matches IssueListItem record returned by GET /api/issues
export interface IssueSummary {
  issueId: string
  title: string
  description: string | null
  status: IssueStatus
  priority: IssuePriority
  locationName: string | null
  departmentId: string | null
  departmentName: string | null
  createdAt: string
  resolvedAt: string | null
}

export interface IssueListResponse {
  items: IssueSummary[]
  totalCount: number
  page: number
  pageSize: number
}

// Matches IssueDetailResponse record returned by GET /api/issues/{id}
export interface IssueDetail {
  issueId: string
  title: string
  description: string | null
  priority: IssuePriority
  status: IssueStatus
  locationId: string | null
  locationName: string | null
  departmentId: string | null
  departmentName: string | null
  assignedTo: string | null
  resolvedBy: string | null
  feedbackId: string
  feedbackRating: number
  feedbackComment: string | null
  createdAt: string
  updatedAt: string
  resolvedAt: string | null
}

// Matches response from POST .../start, POST .../resolve, PATCH .../assign
export interface IssueActionResponse {
  issueId: string
  status: IssueStatus
  updatedAt: string
}

// ─── Locations ───────────────────────────────────────────────────────────────

export type LocationType = 'room' | 'table' | 'public_area' | 'department' | 'service_point' | 'other'

// Matches LocationListItem record returned by GET /api/locations
export interface LocationSummary {
  id: string
  name: string
  label: string | null
  type: LocationType
  shortCode: string
  guestLink: string
  isActive: boolean
  createdAt: string
}

// Matches LocationDetailResponse record returned by GET /api/locations/{id}
// and POST /api/locations and PUT /api/locations/{id}
export interface LocationDetail {
  id: string
  name: string
  label: string | null
  type: LocationType
  shortCode: string
  guestLink: string
  isActive: boolean
  createdAt: string
  updatedAt: string
}

export interface LocationListResponse {
  items: LocationSummary[]
  totalCount: number
  page: number
  pageSize: number
}

export interface CreateLocationRequest {
  name: string
  label?: string
  type?: LocationType
}

export interface UpdateLocationRequest {
  name: string
  label?: string
  type?: LocationType
  isActive: boolean
}

// ─── Departments ─────────────────────────────────────────────────────────────

// Matches DepartmentResponse record returned by all department endpoints
export interface Department {
  id: string
  name: string
  description: string | null
  icon: string | null
  handlesCategories: string[]
  slaMinutes: number
  sortOrder: number
  isActive: boolean
  activeIssueCount: number
  createdAt: string
  updatedAt: string
}

export interface DepartmentListResponse {
  items: Department[]
  totalCount: number
}

export interface CreateDepartmentRequest {
  name: string
  description?: string
  icon?: string
  handlesCategories?: string[]
  slaMinutes?: number
}

export interface UpdateDepartmentRequest {
  name: string
  description?: string
  icon?: string
  handlesCategories?: string[]
  slaMinutes?: number
  sortOrder: number
  isActive: boolean
}

// ─── Feedback (Guest) ────────────────────────────────────────────────────────

export interface SubmitFeedbackRequest {
  rating: number
  comment?: string
  categoryHint?: string
  website: string   // honeypot — always send as empty string from real users
}

export interface FeedbackResponse {
  feedbackId: string
  issueId: string | null
  pointsEarned: number
  redirectUrl: string | null
}

export interface SessionResponse {
  sessionId: string
}
