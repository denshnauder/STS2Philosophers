using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;

namespace STS2MinimalMod;

public sealed class KongziMuduoRitualStrengthPower : TemporaryStrengthPower
{
    public override AbstractModel OriginModel => ModelDb.Relic<KongziMuduo>();

    protected override bool IsVisibleInternal => false;
}

public sealed class KongziMuduoDiscourtesyStrengthPower : TemporaryStrengthPower
{
    public override AbstractModel OriginModel => ModelDb.Relic<KongziMuduo>();

    protected override bool IsPositive => false;

    protected override bool IsVisibleInternal => false;
}
