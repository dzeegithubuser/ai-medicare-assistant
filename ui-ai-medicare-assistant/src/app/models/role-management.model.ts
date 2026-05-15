export interface FpgSummary {
  groupId: string;
  name: string;
  description?: string;
  createdAt: string;
}

export interface UserSummary {
  userId: string;
  email: string;
  firstName: string;
  lastName: string;
  phone: string;
  role: string;
  fpgId?: string;
  fpId?: string;
  mustChangePassword: boolean;
  createdAt: string;
}

export interface FpSummary {
  userId: string;
  email: string;
  firstName: string;
  lastName: string;
  phone: string;
  fpgId?: string;
  mustChangePassword: boolean;
  createdAt: string;
}

export interface EndUserSummary {
  userId: string;
  email: string;
  firstName: string;
  lastName: string;
  phone: string;
  fpId?: string;
  mustChangePassword: boolean;
  createdAt: string;
}

export interface RecommendationSummary {
  id: string;
  name: string;
  status: string;
  type: string;
  createdAt: string;
  updatedAt: string;
}

export interface RecommendationByUser {
  userId: string;
  email: string;
  firstName: string;
  lastName: string;
  recommendations: RecommendationSummary[];
}

export interface CreateFpgRequest {
  name: string;
  description?: string;
}

export interface CreateFpgAdminUserRequest {
  email: string;
  firstName: string;
  lastName: string;
  phone: string;
  password: string;
}

export interface CreateFpRequest {
  email: string;
  firstName: string;
  lastName: string;
  password: string;
  phone: string;
}

export interface UpdateFpRequest {
  firstName: string;
  lastName: string;
  phone: string;
}

export interface CreateEndUserRequest {
  email: string;
  firstName: string;
  lastName: string;
  phone: string;
  password: string;
}
