using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace RideMateService.Commons.Repository;

public partial class RideMateContext
{
    partial void OnModelCreatingPartial(ModelBuilder modelBuilder)
    {
        // ACTIVE_FLAG는 DB가 계산하는 생성 컬럼이다. INSERT·UPDATE에 값을 넣으면 MySQL이 거부하므로 저장에서 뺀다. [ADR-0010]
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            var activeFlag = entityType.FindProperty("ActiveFlag");
            if (activeFlag is null)
                continue;

            activeFlag.ValueGenerated = ValueGenerated.OnAddOrUpdate;
            activeFlag.SetBeforeSaveBehavior(PropertySaveBehavior.Ignore);
            activeFlag.SetAfterSaveBehavior(PropertySaveBehavior.Ignore);
        }
    }
}
