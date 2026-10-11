import { inject, Injectable, signal } from '@angular/core';
import { map, Observable, switchMap, tap } from 'rxjs';
import { API_ENDPOINTS } from '../config/api-endpoints';
import { PagedResult } from '../models/api.models';
import {
  AssignTenantRequest,
  BookingAnnouncementState,
  BookingAnnouncementSummary,
  CreatePropertyBookingRequest,
  EvictTenantRequest,
  Property,
  PropertyBooking,
  PropertyBookingGroup,
  PropertyQuery,
  PropertyStatus,
  PublicProperty,
  UpsertPropertyRequest,
} from '../models/property.models';
import { StatusHistory } from '../models/management.models';
import { ApiService } from './api.service';

@Injectable({ providedIn: 'root' })
export class PropertyService {
  private readonly api = inject(ApiService);
  private readonly pendingBookingAnnouncementsState = signal(0);
  readonly pendingBookingAnnouncements = this.pendingBookingAnnouncementsState.asReadonly();
  available(query: PropertyQuery): Observable<PagedResult<PublicProperty>> {
    return this.api.read(API_ENDPOINTS.properties.available, query);
  }
  featured(): Observable<PublicProperty[]> {
    return this.api.read(API_ENDPOINTS.properties.featured);
  }
  publicDetails(id: string): Observable<PublicProperty> {
    return this.api.read(`${API_ENDPOINTS.properties.root}/${id}`);
  }
  all(query: PropertyQuery): Observable<PagedResult<Property>> {
    return this.api.read(API_ENDPOINTS.properties.root, query);
  }
  details(id: string): Observable<Property> {
    return this.api.get(`${API_ENDPOINTS.properties.root}/${id}/manage`);
  }
  create(body: UpsertPropertyRequest): Observable<Property> {
    return this.api.post(API_ENDPOINTS.properties.root, body);
  }
  update(id: string, body: UpsertPropertyRequest): Observable<Property> {
    return this.api.put(`${API_ENDPOINTS.properties.root}/${id}`, body);
  }
  changeStatus(
    id: string,
    status: PropertyStatus,
    reason?: string,
    enquiryId?: string,
  ): Observable<Property> {
    return this.api.patch(`${API_ENDPOINTS.properties.root}/${id}/status`, {
      status,
      reason,
      enquiryId,
    });
  }
  assignTenant(id: string, body: AssignTenantRequest): Observable<Property> {
    return this.api.post(`${API_ENDPOINTS.properties.root}/${id}/assign-tenant`, body);
  }
  updateTenant(id: string, body: AssignTenantRequest): Observable<Property> {
    return this.api.put(`${API_ENDPOINTS.properties.root}/${id}/tenant`, body);
  }
  bookings(id: string): Observable<PropertyBooking[]> {
    return this.api.get(`${API_ENDPOINTS.properties.root}/${id}/bookings`);
  }
  bookingGroups(): Observable<PropertyBookingGroup[]> {
    return this.api.read(`${API_ENDPOINTS.properties.root}/bookings`);
  }
  refreshBookingAnnouncementCount(): Observable<BookingAnnouncementSummary> {
    return this.api
      .get<BookingAnnouncementSummary>(
        `${API_ENDPOINTS.properties.root}/bookings/announcement-summary`,
      )
      .pipe(tap((summary) => this.pendingBookingAnnouncementsState.set(summary.pendingCount)));
  }
  setBookingAnnouncement(
    propertyId: string,
    isPosted: boolean,
  ): Observable<BookingAnnouncementState> {
    return this.api
      .patch<{ isPosted: boolean }, BookingAnnouncementState>(
        `${API_ENDPOINTS.properties.root}/${propertyId}/bookings/announcement`,
        { isPosted },
      )
      .pipe(switchMap((state) => this.refreshBookingAnnouncementCount().pipe(map(() => state))));
  }
  createBooking(id: string, body: CreatePropertyBookingRequest): Observable<PropertyBooking> {
    return this.api.post(`${API_ENDPOINTS.properties.root}/${id}/bookings`, body);
  }
  cancelBooking(propertyId: string, bookingId: string): Observable<void> {
    return this.api.delete(`${API_ENDPOINTS.properties.root}/${propertyId}/bookings/${bookingId}`);
  }
  closeAllBookings(propertyId: string): Observable<void> {
    return this.api.delete(`${API_ENDPOINTS.properties.root}/${propertyId}/bookings`);
  }
  evict(id: string, request: EvictTenantRequest): Observable<Property> {
    return this.api.post(`${API_ENDPOINTS.properties.root}/${id}/evict`, request);
  }
  history(id: string): Observable<StatusHistory[]> {
    return this.api.read(`${API_ENDPOINTS.properties.root}/${id}/history`);
  }
  delete(id: string): Observable<void> {
    return this.api.delete(`${API_ENDPOINTS.properties.root}/${id}`);
  }
}
