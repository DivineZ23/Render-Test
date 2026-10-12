namespace ImperialEstates.Domain.Enums;

// Explicit values keep existing MongoDB documents compatible: legacy Owned (2)
// becomes Paid, while legacy Unavailable (3) becomes OnHold.
public enum PropertyStatus
{
    Available = 0,
    Booked = 1,
    Paid = 2,
    OnHold = 3,
    Auction = 4,
    Overdue = 5,
    Evictable = 6
}
public enum PropertyType
{
    // Legacy values are retained so existing MongoDB documents remain readable.
    Apartment, House, Villa, Office, Shop, Warehouse, Land, Other,
    Motel, TrevorsTrailer, JanitorApartment, LowEndApartment, LestersHouse,
    FranklinsHouse, MidEndApartment, TrevorsBeachHouse, MichaelsMansion,
    FranklinsMansion, HighEndApartment
}

public static class PropertyTypeMetadata
{
    public static int? PersonCapacity(this PropertyType type) => type switch
    {
        PropertyType.Motel => 1,
        PropertyType.TrevorsTrailer or PropertyType.JanitorApartment or PropertyType.LowEndApartment => 2,
        PropertyType.LestersHouse or PropertyType.FranklinsHouse => 3,
        PropertyType.MidEndApartment or PropertyType.TrevorsBeachHouse or PropertyType.MichaelsMansion
            or PropertyType.FranklinsMansion => 4,
        PropertyType.HighEndApartment => 5,
        _ => null
    };

    public static bool IsSupportedInterior(this PropertyType type) => type.PersonCapacity().HasValue;

    public static decimal? StateCost(this PropertyType type) => type switch
    {
        PropertyType.Motel => 500m,
        PropertyType.TrevorsTrailer => 1000m,
        PropertyType.JanitorApartment => 1500m,
        PropertyType.LowEndApartment => 2000m,
        PropertyType.LestersHouse => 3000m,
        PropertyType.FranklinsHouse => 4000m,
        PropertyType.MidEndApartment => 4500m,
        PropertyType.TrevorsBeachHouse => 5000m,
        PropertyType.MichaelsMansion or PropertyType.FranklinsMansion or PropertyType.HighEndApartment => 8000m,
        _ => null
    };

    public static int? StorageCapacity(this PropertyType type) => type switch
    {
        PropertyType.Motel => 1500,
        PropertyType.TrevorsTrailer => 2250,
        PropertyType.JanitorApartment => 3000,
        PropertyType.LowEndApartment => 3750,
        PropertyType.LestersHouse => 5250,
        PropertyType.FranklinsHouse => 6000,
        PropertyType.MidEndApartment => 7500,
        PropertyType.TrevorsBeachHouse => 9000,
        PropertyType.MichaelsMansion or PropertyType.FranklinsMansion or PropertyType.HighEndApartment => 15000,
        _ => null
    };
}
public enum TenantStatus { Active, Evicted, Ended }
// Explicit values preserve the roles already stored as numeric enum values in MongoDB.
public enum UserRole { Agent = 0, Manager = 1, Owner = 2, SeniorAgent = 3, StateManager = 4 }
public static class UserRolePermissions
{
    public static bool CanEvict(this UserRole role) =>
        role is UserRole.SeniorAgent or UserRole.Manager or UserRole.Owner;
}
public enum ApprovalStatus { Pending, Approved, Rejected }
public enum AccessStatus { Active, Pending, Revoked }
public enum EnquiryStatus { New, Contacted, ViewingScheduled, Booked, Closed, Rejected }
public enum BookingStatus { Active, Cancelled, Converted }
public enum RecruitmentStatus { Pending, Accepted, Rejected }
