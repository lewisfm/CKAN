using CKAN.NetKAN.Model;

namespace CKAN.NetKAN.Validators
{
    internal sealed class IsCkanModuleValidator : IValidator
    {
        public void Validate(Metadata metadata)
        {
            ReleaseDto.FromJson(metadata.AllJson.ToString());
        }
    }
}
