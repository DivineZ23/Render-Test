using ImperialEstates.Domain.Entities;
using Microsoft.Extensions.Options;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;

namespace ImperialEstates.Infrastructure.Persistence;

public sealed class MongoContext
{
    static MongoContext()
    {
        if (!BsonClassMap.IsClassMapRegistered(typeof(CommissionRecord)))
        {
            BsonClassMap.RegisterClassMap<CommissionRecord>(classMap =>
            {
                classMap.AutoMap();
                classMap.SetIgnoreExtraElements(true);
            });
        }
    }

    public MongoContext(IOptions<MongoOptions> options)
    {
        Client = new MongoClient(options.Value.ConnectionString);
        Database = Client.GetDatabase(options.Value.DatabaseName);
    }

    public IMongoClient Client { get; }
    public IMongoDatabase Database { get; }
    public IMongoCollection<Block> Blocks => Database.GetCollection<Block>("blocks");
    public IMongoCollection<Property> Properties => Database.GetCollection<Property>("properties");
    public IMongoCollection<PropertyBooking> PropertyBookings => Database.GetCollection<PropertyBooking>("property_bookings");
    public IMongoCollection<Tenant> Tenants => Database.GetCollection<Tenant>("tenants");
    public IMongoCollection<RentSyncSnapshot> RentSyncSnapshots => Database.GetCollection<RentSyncSnapshot>("rent_sync_snapshots");
    public IMongoCollection<User> Users => Database.GetCollection<User>("users");
    public IMongoCollection<Enquiry> Enquiries => Database.GetCollection<Enquiry>("enquiries");
    public IMongoCollection<RecruitmentApplication> RecruitmentApplications => Database.GetCollection<RecruitmentApplication>("recruitment_applications");
    public IMongoCollection<PropertyStatusHistory> StatusHistory => Database.GetCollection<PropertyStatusHistory>("property_status_history");
    public IMongoCollection<AuditLog> AuditLogs => Database.GetCollection<AuditLog>("audit_logs");
    public IMongoCollection<ApplicationSetting> Settings => Database.GetCollection<ApplicationSetting>("application_settings");
    public IMongoCollection<CommissionRecord> Commissions => Database.GetCollection<CommissionRecord>("commissions");
}
