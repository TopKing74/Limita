using Limita.Domain.Common;
using Limita.Domain.Enums;

namespace Limita.Domain.Entities;

public class BillProvider : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public BillCategory Category { get; set; } = BillCategory.Utilities;
    public bool IsActive { get; set; } = true;

    public ICollection<Bill> Bills { get; set; } = new List<Bill>();
}
