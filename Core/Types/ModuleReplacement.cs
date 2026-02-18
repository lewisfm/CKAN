namespace CKAN
{
    public class ModuleReplacement
    {
        public ModuleReplacement(ReleaseDto toReplace, ReleaseDto replaceWith)
        {
            ToReplace   = toReplace;
            ReplaceWith = replaceWith;
        }

        public readonly ReleaseDto ToReplace;
        public readonly ReleaseDto ReplaceWith;
    }
}
