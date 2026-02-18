namespace CKAN
{
    public class Relationship
    {
        public Relationship(ReleaseDto             source,
                            RelationshipType       type,
                            RelationshipDescriptor descr)
        {
            Source     = source;
            Type       = type;
            Descriptor = descr;
        }

        public readonly ReleaseDto             Source;
        public readonly RelationshipType       Type;
        public readonly RelationshipDescriptor Descriptor;
    }
}
