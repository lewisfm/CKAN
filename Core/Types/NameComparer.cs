using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace CKAN
{
    public class NameComparer : IEqualityComparer<ReleaseDto>
    {
        [ExcludeFromCodeCoverage]
        public bool Equals(ReleaseDto? x, ReleaseDto? y)
            => x?.identifier.Equals(y?.identifier)
                ?? (y == null);

        public int GetHashCode(ReleaseDto obj)
            => obj.identifier.GetHashCode();
    }
}
