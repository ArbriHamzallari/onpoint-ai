import { api } from './client'
import type {
  LocationListResponse,
  LocationDetail,
  CreateLocationRequest,
  UpdateLocationRequest,
} from '../types'

export interface LocationFilters {
  search?: string
  includeInactive?: boolean
  page?: number
  pageSize?: number
}

export const locationsApi = {
  list: (filters: LocationFilters = {}) => {
    const params = new URLSearchParams()
    if (filters.search)                        params.set('search', filters.search)
    if (filters.includeInactive !== undefined) params.set('includeInactive', String(filters.includeInactive))
    if (filters.page)                          params.set('page', String(filters.page))
    if (filters.pageSize)                      params.set('pageSize', String(filters.pageSize))
    const qs = params.toString()
    return api.get<LocationListResponse>(`/api/locations${qs ? `?${qs}` : ''}`)
  },

  get: (id: string) => api.get<LocationDetail>(`/api/locations/${id}`),

  create: (body: CreateLocationRequest) =>
    api.post<LocationDetail>('/api/locations', body),

  update: (id: string, body: UpdateLocationRequest) =>
    api.put<LocationDetail>(`/api/locations/${id}`, body),

  delete: (id: string) => api.delete<void>(`/api/locations/${id}`),

  // Use as <img src={locationsApi.qrUrl(id)} /> — returns image/png directly
  qrUrl: (id: string) => `/api/locations/${id}/qr`,
}
