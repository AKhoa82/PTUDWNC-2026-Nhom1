using System.ComponentModel.DataAnnotations;

namespace CulinaryBlog.Domain.Common;

public abstract class ConcurrentEntity : SoftDeletableEntity
{
    [Timestamp]
    public uint RowVersion { get; set; }
}
