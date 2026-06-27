namespace NetIAM.Domain.Abstractions;

public abstract class EntityBase
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
}

public abstract class AuditedEntityBase : EntityBase
{
    public string? CreateBy { get; set; }

    public DateTimeOffset CreateTime { get; set; } = DateTimeOffset.UtcNow;

    public string? UpdateBy { get; set; }

    public DateTimeOffset UpdateTime { get; set; } = DateTimeOffset.UtcNow;

    public bool IsDeleted { get; set; }

    public string? Remark { get; set; }
}
