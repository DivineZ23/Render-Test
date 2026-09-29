using ImperialEstates.Domain.Entities;
using MongoDB.Bson;
using MongoDB.Driver;

namespace ImperialEstates.Infrastructure.Persistence;

public sealed class MongoIndexInitializer(MongoContext db)
{
    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        // One-way reset from the retired deposit-percentage/grade commission scheme.
        await db.Commissions.DeleteManyAsync(
            Builders<CommissionRecord>.Filter.Ne(x => x.SchemeVersion, CommissionRecord.CurrentSchemeVersion),
            cancellationToken);
        await db.Settings.DeleteManyAsync(x => x.Key == "commission.settings", cancellationToken);

        await db.Blocks.Indexes.CreateManyAsync([
            new CreateIndexModel<Block>(Builders<Block>.IndexKeys.Ascending(x => x.BlockId), new() { Unique = true, Name = "ux_block_id" }),
            new CreateIndexModel<Block>(Builders<Block>.IndexKeys.Ascending(x => x.BlockName), new() { Unique = true, Name = "ux_block_name" }),
            new CreateIndexModel<Block>(Builders<Block>.IndexKeys.Ascending(x => x.IsActive).Ascending(x => x.IsDeleted), new() { Name = "ix_block_active" })
        ], cancellationToken);
        await db.Properties.Indexes.CreateManyAsync([
            new CreateIndexModel<Property>(Builders<Property>.IndexKeys.Ascending(x => x.PropertyId), new() { Unique = true, Name = "ux_property_id" }),
            new CreateIndexModel<Property>(Builders<Property>.IndexKeys.Ascending(x => x.BlockId).Ascending(x => x.Status), new() { Name = "ix_property_block_status" }),
            new CreateIndexModel<Property>(Builders<Property>.IndexKeys.Ascending(x => x.Status).Ascending(x => x.IsActive).Ascending(x => x.IsDeleted), new() { Name = "ix_property_public" }),
            new CreateIndexModel<Property>(Builders<Property>.IndexKeys.Text(x => x.PropertyName).Text(x => x.Description), new() { Name = "ix_property_search" })
        ], cancellationToken);
        await db.PropertyBookings.Indexes.CreateManyAsync([
            new CreateIndexModel<PropertyBooking>(Builders<PropertyBooking>.IndexKeys.Ascending(x => x.PropertyId).Ascending(x => x.Status).Descending(x => x.CreatedAt), new() { Name = "ix_booking_property_status_date" }),
            new CreateIndexModel<PropertyBooking>(Builders<PropertyBooking>.IndexKeys.Ascending(x => x.Cid).Ascending(x => x.Status), new() { Name = "ix_booking_cid_status" })
        ], cancellationToken);
        await db.Users.Indexes.CreateManyAsync([
            new CreateIndexModel<User>(Builders<User>.IndexKeys.Ascending(x => x.DiscordUserId), new() { Unique = true, Name = "ux_user_discord" }),
            new CreateIndexModel<User>(Builders<User>.IndexKeys.Ascending(x => x.Cid), new CreateIndexOptions<User>
            {
                Unique = true,
                Name = "ux_user_cid",
                PartialFilterExpression = new BsonDocument("Cid", new BsonDocument("$type", "number"))
            }),
            new CreateIndexModel<User>(Builders<User>.IndexKeys.Ascending(x => x.ApprovalStatus).Ascending(x => x.AccessStatus).Ascending(x => x.Role), new() { Name = "ix_user_state" })
        ], cancellationToken);
        await db.Tenants.Indexes.CreateManyAsync([
            new CreateIndexModel<Tenant>(Builders<Tenant>.IndexKeys.Ascending(x => x.PropertyId).Ascending(x => x.Status), new() { Name = "ix_tenant_property_status" }),
            new CreateIndexModel<Tenant>(Builders<Tenant>.IndexKeys.Ascending(x => x.Cid).Descending(x => x.CreatedAt), new() { Name = "ix_tenant_cid_date" })
        ], cancellationToken);
        await db.Enquiries.Indexes.CreateOneAsync(new CreateIndexModel<Enquiry>(Builders<Enquiry>.IndexKeys.Ascending(x => x.PropertyId).Ascending(x => x.Status).Descending(x => x.CreatedAt), new() { Name = "ix_enquiry_property_status" }), cancellationToken: cancellationToken);
        await db.RecruitmentApplications.Indexes.CreateManyAsync([
            new CreateIndexModel<RecruitmentApplication>(Builders<RecruitmentApplication>.IndexKeys.Ascending(x => x.Status).Descending(x => x.CreatedAt), new() { Name = "ix_recruitment_status_date" }),
            new CreateIndexModel<RecruitmentApplication>(Builders<RecruitmentApplication>.IndexKeys.Ascending(x => x.CharacterCid).Ascending(x => x.DiscordId).Ascending(x => x.Status), new() { Name = "ix_recruitment_applicant_status" })
        ], cancellationToken);
        await db.StatusHistory.Indexes.CreateOneAsync(new CreateIndexModel<PropertyStatusHistory>(Builders<PropertyStatusHistory>.IndexKeys.Ascending(x => x.PropertyId).Descending(x => x.CreatedAt), new() { Name = "ix_history_property_date" }), cancellationToken: cancellationToken);
        await db.AuditLogs.Indexes.CreateManyAsync([
            new CreateIndexModel<AuditLog>(Builders<AuditLog>.IndexKeys.Descending(x => x.CreatedAt), new() { Name = "ix_audit_date" }),
            new CreateIndexModel<AuditLog>(Builders<AuditLog>.IndexKeys.Ascending(x => x.EntityType).Ascending(x => x.EntityId).Descending(x => x.CreatedAt), new() { Name = "ix_audit_entity_date" }),
            new CreateIndexModel<AuditLog>(Builders<AuditLog>.IndexKeys.Ascending(x => x.PerformedByUserId).Descending(x => x.CreatedAt), new() { Name = "ix_audit_actor_date" }),
            new CreateIndexModel<AuditLog>(Builders<AuditLog>.IndexKeys.Ascending(x => x.Action).Descending(x => x.CreatedAt), new() { Name = "ix_audit_action_date" })
        ], cancellationToken);
        await db.Settings.Indexes.CreateOneAsync(new CreateIndexModel<ApplicationSetting>(Builders<ApplicationSetting>.IndexKeys.Ascending(x => x.Key), new() { Name = "ux_setting_key", Unique = true }), cancellationToken: cancellationToken);
        await db.RentSyncSnapshots.Indexes.CreateOneAsync(new CreateIndexModel<RentSyncSnapshot>(Builders<RentSyncSnapshot>.IndexKeys.Descending(x => x.UpdatedAt), new() { Name = "ix_rent_sync_updated" }), cancellationToken: cancellationToken);
        await db.Commissions.Indexes.CreateManyAsync([
            new CreateIndexModel<CommissionRecord>(Builders<CommissionRecord>.IndexKeys.Ascending(x => x.SchemeVersion).Ascending(x => x.AgentUserId).Ascending(x => x.IsPaid).Descending(x => x.CreatedAt), new() { Name = "ix_commission_v2_agent_paid_date" }),
            new CreateIndexModel<CommissionRecord>(Builders<CommissionRecord>.IndexKeys.Ascending(x => x.SettlementId), new() { Name = "ix_commission_v2_settlement" })
        ], cancellationToken);
    }
}
