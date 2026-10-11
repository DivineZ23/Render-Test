using ImperialEstates.Application.DTOs;
using ImperialEstates.Application.Interfaces;
using ImperialEstates.Domain.Entities;
using ImperialEstates.Domain.Enums;
using ImperialEstates.Domain.Exceptions;

namespace ImperialEstates.Application.Services;

public sealed class BlockService(IBlockRepository blocks, IPropertyRepository properties, IAuditRepository audits)
{
    public async Task<IReadOnlyList<BlockDto>> GetAllAsync(bool activeOnly, CancellationToken cancellationToken)
    {
        var blocksTask = blocks.GetAllAsync(activeOnly, cancellationToken);
        var propertiesTask = properties.GetPortfolioAsync(cancellationToken);
        await Task.WhenAll(blocksTask, propertiesTask);
        var financials = BuildFinancials(propertiesTask.Result);
        return blocksTask.Result.Select(value => FinancialDto(value, financials)).ToList();
    }

    public async Task<BlockDto> GetAsync(string id, CancellationToken cancellationToken)
    {
        var value = await GetEntityAsync(id, cancellationToken);
        return FinancialDto(value, BuildFinancials(await properties.GetPortfolioAsync(cancellationToken)));
    }

    public async Task<BlockDto> CreateAsync(UpsertBlockRequest request, string actorId, CancellationToken cancellationToken)
    {
        if (await blocks.GetByBusinessIdAsync(request.BlockId, cancellationToken) is not null ||
            await blocks.GetByNameAsync(request.BlockName, cancellationToken) is not null)
            throw new DomainRuleException("Block ID and name must be unique.", "BLOCK_NOT_UNIQUE");
        var value = new Block
        {
            BlockId = request.BlockId, BlockName = request.BlockName.Trim(), Description = request.Description?.Trim(),
            Address = request.Address?.Trim(), ImageUrl = request.ImageUrl, IsActive = request.IsActive, CreatedBy = actorId
        };
        await blocks.CreateAsync(value, cancellationToken);
        await audits.CreateAsync(NewAudit("block.created", value.Id, actorId), cancellationToken);
        return value.ToDto(0);
    }

    public async Task<BlockDto> UpdateAsync(string id, UpsertBlockRequest request, string actorId, CancellationToken cancellationToken)
    {
        var value = await GetEntityAsync(id, cancellationToken);
        var sameBusinessId = await blocks.GetByBusinessIdAsync(request.BlockId, cancellationToken);
        var sameName = await blocks.GetByNameAsync(request.BlockName, cancellationToken);
        if ((sameBusinessId is not null && sameBusinessId.Id != id) || (sameName is not null && sameName.Id != id))
            throw new DomainRuleException("Block ID and name must be unique.", "BLOCK_NOT_UNIQUE");
        value.BlockId = request.BlockId;
        value.BlockName = request.BlockName.Trim();
        value.Description = request.Description?.Trim();
        value.Address = request.Address?.Trim();
        value.ImageUrl = request.ImageUrl;
        value.IsActive = request.IsActive;
        value.UpdatedAt = DateTime.UtcNow;
        value.UpdatedBy = actorId;
        await blocks.UpdateAsync(value, cancellationToken);
        await audits.CreateAsync(NewAudit("block.updated", value.Id, actorId), cancellationToken);
        return FinancialDto(value, BuildFinancials(await properties.GetPortfolioAsync(cancellationToken)));
    }

    public async Task DeleteAsync(string id, string actorId, CancellationToken cancellationToken)
    {
        var value = await GetEntityAsync(id, cancellationToken);
        if (await properties.CountByBlockAsync(id, cancellationToken) > 0)
            throw new DomainRuleException("Reassign or deactivate this block's properties before deleting it.", "BLOCK_HAS_PROPERTIES");
        value.IsDeleted = true;
        value.IsActive = false;
        value.UpdatedAt = DateTime.UtcNow;
        value.UpdatedBy = actorId;
        await blocks.UpdateAsync(value, cancellationToken);
        await audits.CreateAsync(NewAudit("block.deleted", value.Id, actorId), cancellationToken);
    }

    private async Task<Block> GetEntityAsync(string id, CancellationToken cancellationToken) =>
        await blocks.GetByIdAsync(id, cancellationToken) ?? throw new KeyNotFoundException("Block not found.");

    private static AuditLog NewAudit(string action, string entityId, string actorId) =>
        new() { Action = action, EntityType = "block", EntityId = entityId, PerformedByUserId = actorId };

    private static IReadOnlyDictionary<string, BlockFinancials> BuildFinancials(IEnumerable<PropertyPortfolioItem> properties) =>
        properties
            .GroupBy(property => property.BlockId)
            .ToDictionary(
                group => group.Key,
                group => new BlockFinancials(
                    group.LongCount(),
                    group.Sum(property => property.Type.StateCost() ?? 0),
                    group.Sum(property => property.Rent)));

    private static BlockDto FinancialDto(Block block, IReadOnlyDictionary<string, BlockFinancials> financials)
    {
        var financialsForBlock = financials.GetValueOrDefault(block.Id);
        return block.ToDto(financialsForBlock?.Count ?? 0, financialsForBlock?.Cost ?? 0, financialsForBlock?.Rent ?? 0);
    }

    private sealed record BlockFinancials(long Count, decimal Cost, decimal Rent);
}
