import { api } from './client'
import type {
  IssueListResponse,
  IssueDetail,
  IssueActionResponse,
  IssueStatus,
} from '../types'

export interface IssueFilters {
  status?: IssueStatus
  departmentId?: string
  locationId?: string
  page?: number
  pageSize?: number
}

export const issuesApi = {
  list: (filters: IssueFilters = {}) => {
    const params = new URLSearchParams()
    if (filters.status)       params.set('status', filters.status)
    if (filters.departmentId) params.set('departmentId', filters.departmentId)
    if (filters.locationId)   params.set('locationId', filters.locationId)
    if (filters.page)         params.set('page', String(filters.page))
    if (filters.pageSize)     params.set('pageSize', String(filters.pageSize))
    const qs = params.toString()
    return api.get<IssueListResponse>(`/api/issues${qs ? `?${qs}` : ''}`)
  },

  get: (id: string) => api.get<IssueDetail>(`/api/issues/${id}`),

  start: (id: string) =>
    api.post<IssueActionResponse>(`/api/issues/${id}/start`, {}),

  resolve: (id: string) =>
    api.post<IssueActionResponse>(`/api/issues/${id}/resolve`, {}),

  assign: (id: string, departmentId: string) =>
    api.patch<IssueActionResponse>(`/api/issues/${id}/assign`, { departmentId }),
}
