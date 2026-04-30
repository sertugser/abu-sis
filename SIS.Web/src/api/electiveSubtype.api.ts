import apiClient from './axios';
import type {
  ElectiveSubtypeDto,
  CreateElectiveSubtypeRequest,
  UpdateElectiveSubtypeRequest,
} from '../types/electiveSubtype';

const BASE = '/elective-subtypes';

export const electiveSubtypeApi = {
  list: (includeInactive = false) =>
    apiClient
      .get<ElectiveSubtypeDto[]>(BASE, { params: { includeInactive } })
      .then(r => r.data),

  create: (req: CreateElectiveSubtypeRequest) =>
    apiClient.post<ElectiveSubtypeDto>(BASE, req).then(r => r.data),

  update: (id: number, req: UpdateElectiveSubtypeRequest) =>
    apiClient.put<ElectiveSubtypeDto>(`${BASE}/${id}`, req).then(r => r.data),
};
