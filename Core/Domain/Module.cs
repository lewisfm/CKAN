namespace CKAN
{
    /// <summary>
    /// A module, tracked by a specified repository, which may be associated
    /// with an arbitrary number of releases.
    /// </summary>
    public class Module
    {
        /// <summary>
        /// The unique local ID (surrogate key) of the module.
        /// </summary>
        public long Id { get; set; }
        
        /// <summary>
        /// The unique ID of the repository which tracks this module.
        /// </summary>
        public long RepoId { get; set; }
        
        /// <summary>
        /// The module's unique-per-repository textual identifier.
        /// </summary>
        public string Slug {  get; set; }

        /// <summary>
        /// The number of times which this module has been downloaded by any user.
        /// </summary>
        public long DownloadCount { get; set; } = 0;

        public Module(long repoId, long id, string slug)
        {
            RepoId = repoId;
            Id = id;
            Slug = slug;
        }
    }
}