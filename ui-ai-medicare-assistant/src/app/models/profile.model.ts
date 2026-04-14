export interface UserProfileResponse {
  profile: ProfileDto | null;
  isProfileComplete: boolean;
  /** MongoDB id for active FP Drugs prescription list */
  currentPrescriptionDocumentId?: string | null;
}

export interface ProfileDto {
  firstName: string;
  lastName: string;
  coverageYear: number;
  healthCondition: number;
  taxFilingStatus: string;
  magiTier: string;
  gender: string;
  tobaccoStatus: number;
  dateOfBirth: string;
  concierge: number;
  conciergeAmount: number | null;
  alternateEmail: string | null;
  alternateMobile: string | null;
  lifeExpectancy: number;
  addressLine1: string;
  addressLine2: string;
  street: string;
  city: string;
  state: string;
  zipCode: string;
  county: string;
  countyCode: string;
  latitude: number | null;
  longitude: number | null;
}

export interface LabelValuePair {
  label: string;
  value: number;
}
