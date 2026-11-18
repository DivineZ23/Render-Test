export type PropertyStatus =
  'available' | 'booked' | 'auction' | 'paid' | 'overdue' | 'evictable' | 'onHold';
export type PropertyType =
  | 'motel'
  | 'trevorsTrailer'
  | 'janitorApartment'
  | 'lowEndApartment'
  | 'lestersHouse'
  | 'franklinsHouse'
  | 'midEndApartment'
  | 'trevorsBeachHouse'
  | 'michaelsMansion'
  | 'franklinsMansion'
  | 'highEndApartment'
  | 'apartment'
  | 'house'
  | 'villa'
  | 'office'
  | 'shop'
  | 'warehouse'
  | 'land'
  | 'other';

export interface Block {
  id: string;
  blockId: number;
  blockName: string;
  description?: string;
  address?: string;
  imageUrl?: string;
  numberOfProperties: number;
  totalCost: number;
  totalRent: number;
  totalProfit: number;
  isActive: boolean;
  createdAt: string;
  updatedAt: string;
}

export interface Property {
  id: string;
  propertyId: number;
  blockId: string;
  blockName: string;
  propertyName: string;
  description?: string;
  type: PropertyType;
  personCapacity?: number;
  stateCost?: number;
  storageCapacity?: number;
  storage?: string;
  rent: number;
  securityDeposit?: number;
  status: PropertyStatus;
  bedrooms?: number;
  bathrooms?: number;
  floor?: number;
  area?: number;
  furnishingStatus?: string;
  amenities: string[];
  images: string[];
  currentTenantId?: string;
  tenantName?: string;
  tenantCid?: number;
  tenantPhoneNumber?: string;
  tenantDiscordId?: string;
  tenantStartDate?: string;
  tenantExpectedEndDate?: string;
  tenantMonthlyRent?: number;
  tenantSecurityDeposit?: number;
  tenantEmergencyContact?: string;
  tenantNotes?: string;
  rentPaidThrough?: string;
  bookedByEnquiryId?: string;
  bookingCount: number;
  unavailableReason?: string;
  isFeatured: boolean;
  isActive: boolean;
  allowOccupiedBookings: boolean;
  createdAt: string;
  updatedAt: string;
}

export type PublicProperty = Omit<
  Property,
  | 'currentTenantId'
  | 'tenantName'
  | 'tenantCid'
  | 'tenantPhoneNumber'
  | 'tenantDiscordId'
  | 'tenantStartDate'
  | 'tenantExpectedEndDate'
  | 'tenantMonthlyRent'
  | 'tenantSecurityDeposit'
  | 'tenantEmergencyContact'
  | 'tenantNotes'
  | 'rentPaidThrough'
  | 'bookedByEnquiryId'
  | 'bookingCount'
  | 'unavailableReason'
  | 'isActive'
  | 'allowOccupiedBookings'
  | 'updatedAt'
>;

export interface PropertyQuery {
  page?: number;
  pageSize?: number;
  search?: string;
  blockId?: string;
  type?: PropertyType | '';
  status?: PropertyStatus | '';
  minRent?: number | null;
  maxRent?: number | null;
  bedrooms?: number | null;
  personCapacity?: number | null;
  storageCapacity?: number | null;
  furnishing?: string;
  amenities?: string[];
  sortBy?: string;
  sortDirection?: 'asc' | 'desc';
}

export interface UpsertPropertyRequest {
  propertyId: number;
  blockId: string;
  propertyName: string;
  description?: string | null;
  type: PropertyType;
  storage?: string | null;
  rent: number;
  securityDeposit?: number | null;
  bedrooms?: number | null;
  bathrooms?: number | null;
  floor?: number | null;
  area?: number | null;
  furnishingStatus?: string | null;
  amenities: string[];
  images: string[];
  isFeatured: boolean;
  isActive: boolean;
  allowOccupiedBookings: boolean;
}

export interface UpsertBlockRequest {
  blockId: number;
  blockName: string;
  description?: string | null;
  address?: string | null;
  imageUrl?: string | null;
  isActive: boolean;
}

export interface AssignTenantRequest {
  cid: number;
  fullName: string;
  phoneNumber: string;
  discordId: string;
  startDate: string;
  expectedEndDate?: string;
  monthlyRent: number;
  securityDeposit?: number | null;
  emergencyContact?: string;
  notes?: string;
}

export interface EvictTenantRequest {
  reason: string;
  storageImageUrls: string[];
}

export interface PropertyBooking {
  id: string;
  propertyId: string;
  cid: number;
  fullName: string;
  phoneNumber: string;
  discordId: string;
  monthlyRent: number;
  bookingAmount: number;
  notes?: string;
  status: 'active' | 'cancelled' | 'converted';
  createdByUserId?: string;
  createdByDisplayName?: string;
  createdAt: string;
}

export interface PropertyBookingGroup {
  images?: string[];
  propertyId: string;
  propertyNumber: number;
  propertyName: string;
  blockName: string;
  type: PropertyType;
  status: PropertyStatus;
  allowOccupiedBookings: boolean;
  bookingAnnouncementPending: boolean;
  bookingAnnouncementCreatedAt?: string;
  bookingAnnouncementPostedAt?: string;
  bookingAnnouncementPostedByDisplayName?: string;
  bookings: PropertyBooking[];
}

export interface BookingAnnouncementSummary {
  pendingCount: number;
}

export interface BookingAnnouncementState {
  propertyId: string;
  isPending: boolean;
  createdAt?: string;
  postedAt?: string;
  postedByDisplayName?: string;
}

export interface CreatePropertyBookingRequest {
  cid: number;
  fullName: string;
  phoneNumber: string;
  discordId: string;
  monthlyRent: number;
  bookingAmount: number;
  notes?: string;
}
