import { api } from './client'
import type {
  DepartmentListResponse,
  Department,
  CreateDepartmentRequest,
  UpdateDepartmentRequest,
} from '../types'

export const departmentsApi = {
  list: () => api.get<DepartmentListResponse>('/api/departments'),

  get: (id: string) => api.get<Department>(`/api/departments/${id}`),

  create: (body: CreateDepartmentRequest) =>
    api.post<Department>('/api/departments', body),

  update: (id: string, body: UpdateDepartmentRequest) =>
    api.put<Department>(`/api/departments/${id}`, body),

  delete: (id: string) => api.delete<void>(`/api/departments/${id}`),
}
